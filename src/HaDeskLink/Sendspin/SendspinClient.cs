// HA DeskLink - Home Assistant Companion App
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
#nullable enable
using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace HaDeskLink.Sendspin;

/// <summary>
/// Sendspin player client: connects to a Music Assistant Sendspin server
/// (ws://host:8927/sendspin), runs the Noise KKpsk2 handshake (client =
/// responder), exchanges hellos, activates the player@v1 role, keeps the clock
/// in sync, and plays timestamped PCM audio chunks through NAudio.
/// </summary>
public class SendspinClient : IDisposable
{
    // ─── Wire constants ───────────────────────────────────────────────
    // Binary audio chunk header (new spec revision): type(1) + timestamp_us(8)
    // + send_ahead(4) = 13 bytes, rest = encoded audio.
    private const int BinaryHeaderSize = 13;
    private const byte BinaryTypeAudioChunk = 4;

    // Lead applied to play-time estimates before clock sync converges (µs).
    private const long UnsyncedPlayLeadUs = 500_000;

    // Minimum jitter buffer ahead of the DAC before chunks go to NAudio.
    private const int MinBufferMs = 100;

    // Time-sync burst strategy: 8 exchanges back-to-back every 10 s.
    private const int TimeBurstCount = 8;
    private static readonly TimeSpan TimeBurstInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan TimeBurstGap = TimeSpan.FromMilliseconds(20);

    // ─── State ───────────────────────────────────────────────────────
    private readonly string _host;
    private readonly int _port;
    private readonly string _playerName;
    private readonly Action<string> _log;

    private ClientWebSocket? _ws;
    private Noise.Transport? _transport;
    private SendspinTimeFilter _timeFilter = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private Thread? _readerThread;
    private Timer? _schedulerTimer;
    private CancellationTokenSource? _cts;
    private volatile bool _disposed;

    // Audio pipeline
    private readonly object _audioLock = new();
    private volatile bool _playerRoleActive;
    private volatile bool _streamActive;
    private PcmFormat? _pcmFormat;
    private IWavePlayer? _waveOut;
    private BufferedWaveProvider? _buffer;
    private readonly SortedDictionary<long, byte[]> _scheduleQueue = new();
    private long _outputDelayUs;
    private int _volume = 100;
    private bool _muted;
    private string? _serverName;

    /// <summary>
    /// Output delay in milliseconds subtracted from each chunk's local play
    /// time (compensates external speakers/amplifiers; 0 = at the timestamp).
    /// </summary>
    public int OutputDelayMs
    {
        get => (int)(_outputDelayUs / 1000);
        set => _outputDelayUs = Math.Clamp(value, 0, 5000) * 1000L;
    }

    /// <summary>Latest UI-relevant state (volume/mute/availability/sync).</summary>
    public sealed class PlayerState
    {
        public bool Connected { get; init; }
        public bool TimeSynced { get; init; }
        public bool Streaming { get; init; }
        public int Volume { get; init; }
        public bool Muted { get; init; }
        public long SyncErrorUs { get; init; }
        public string? ServerName { get; init; }
        public string? TrackInfo { get; init; }
    }

    /// <summary>Fired (thread-pool) whenever UI-relevant state changes.</summary>
    public event Action<PlayerState>? StateChanged;

    private volatile PlayerState _lastState = new()
    {
        Connected = false, TimeSynced = false, Streaming = false,
        Volume = 100, Muted = false, SyncErrorUs = 0,
    };

    public PlayerState CurrentState => _lastState;

    /// <param name="host">MA server host.</param>
    /// <param name="port">Sendspin port (default 8927).</param>
    /// <param name="playerName">Friendly player name shown in MA.</param>
    /// <param name="log">Optional log sink.</param>
    public SendspinClient(string host, int port, string playerName, Action<string>? log = null)
    {
        _host = host;
        _port = port;
        _playerName = string.IsNullOrWhiteSpace(playerName) ? "HA DeskLink PC" : playerName;
        _log = log ?? (_ => { });
    }

    // ─── Connection lifecycle ─────────────────────────────────────────

    /// <summary>Connect, handshake, and run the session until cancelled/disposed.</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var (priv, pub, clientId) = SendspinIdentityStore.LoadOrGenerate();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var ct = _cts.Token;

        _ws = new ClientWebSocket();
        var uri = new Uri($"ws://{_host}:{_port}/sendspin");
        _log($"Connecting to {uri}… (client_id={clientId[..8]}…)");
        await _ws.ConnectAsync(uri, ct);
        _log("WebSocket connected");

        // ── Noise handshake (server = initiator; we are the responder) ──
        var handshake = await NoiseHandshake.RunClientAsync(
            priv, pub,
            SendTextAsync, ReceiveTextAsync,
            expectedServerId: null, longTermPsk: null).ConfigureAwait(false);
        _transport = handshake.Transport;
        _log($"Noise KKpsk2 handshake OK (server_id={handshake.ServerId[..8]}…, " +
             $"psk={(handshake.UsedSentinelPsk ? "sentinel" : "long-term")})");

        // ── server/hello → client/hello → server/activate ──
        var hello = await ReceiveJsonAsync(ct);
        if (hello is null || hello.Value.GetProperty("type").GetString() != "server/hello")
            throw new InvalidOperationException("expected server/hello");
        _serverName = hello.Value.GetProperty("payload").GetProperty("name").GetString();
        _log($"server/hello from '{_serverName}'");

        await SendJsonAsync(BuildClientHello(), ct).ConfigureAwait(false);

        var activate = await ReceiveJsonAsync(ct);
        if (activate is null || activate.Value.GetProperty("type").GetString() != "server/activate")
            throw new InvalidOperationException("expected server/activate");
        ApplyActivation(activate.Value);
        _log("server/activate received");

        // ── Initial client/state — available only once the clock converged ──
        _playerRoleActive = true;
        await SendJsonAsync(BuildClientState(available: _timeFilter.IsSynchronized), ct)
            .ConfigureAwait(false);

        // ── Steady-state: reader thread + scheduler timer + time bursts ──
        _readerThread = new Thread(ReaderLoop) { IsBackground = true, Name = "sendspin-reader" };
        _readerThread.Start();
        _schedulerTimer = new Timer(_ => SchedulerTick(), null, 20, 20);

        PublishState();

        var lastBurst = DateTime.UtcNow - TimeBurstInterval; // burst immediately
        while (!ct.IsCancellationRequested)
        {
            // client/state available=true once synchronized (spec requirement).
            if (_timeFilter.IsSynchronized && !_lastState.TimeSynced)
            {
                try { await SendJsonAsync(BuildClientState(available: true), ct); }
                catch { /* transient — retried next round */ }
                PublishState();
            }

            if (DateTime.UtcNow - lastBurst >= TimeBurstInterval)
            {
                lastBurst = DateTime.UtcNow;
                await SendTimeBurstAsync(ct).ConfigureAwait(false);
            }

            try { await Task.Delay(500, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task SendTimeBurstAsync(CancellationToken ct)
    {
        // 8 client/time exchanges back-to-back; each server/time reply feeds
        // the Kalman filter (weighted optimally, not lowest-error-wins).
        for (var i = 0; i < TimeBurstCount && !ct.IsCancellationRequested; i++)
        {
            try
            {
                await SendJsonAsync(JsonSerializer.Serialize(new
                {
                    payload = new { client_transmitted = NowUs() },
                    type = "client/time"
                }), ct);
            }
            catch { break; }
            if (i + 1 < TimeBurstCount)
            {
                try { await Task.Delay(TimeBurstGap, ct); }
                catch (OperationCanceledException) { return; }
            }
        }
    }

    // ─── Reader loop ──────────────────────────────────────────────────

    private void ReaderLoop()
    {
        var ct = _cts?.Token ?? CancellationToken.None;
        var reassemblyBuf = new List<byte>();
        int reassemblyType = -1;

        try
        {
            while (!ct.IsCancellationRequested && _ws?.State == WebSocketState.Open)
            {
                var plaintext = ReceiveTransportFrame(ct).GetAwaiter().GetResult();
                if (plaintext.Length == 0) continue;

                byte type = plaintext[0];
                byte[] payload;

                if (type == NoiseHandshake.MsgTypeFragmentMore)
                {
                    if (reassemblyType < 0)
                    {
                        if (plaintext.Length < 2)
                            throw new InvalidOperationException("fragment start missing orig_type");
                        reassemblyType = plaintext[1];
                        reassemblyBuf.AddRange(plaintext[2..]);
                    }
                    else
                    {
                        reassemblyBuf.AddRange(plaintext[1..]);
                    }
                    continue;
                }
                if (type == NoiseHandshake.MsgTypeFragmentEnd)
                {
                    if (reassemblyType < 0)
                        throw new InvalidOperationException("fragment-end without in-flight message");
                    reassemblyBuf.AddRange(plaintext[1..]);
                    var reassembled = new byte[reassemblyBuf.Count + 1];
                    reassembled[0] = (byte)reassemblyType;
                    reassemblyBuf.CopyTo(reassembled, 1);
                    reassemblyBuf.Clear();
                    reassemblyType = -1;
                    type = reassembled[0];
                    payload = reassembled;
                }
                else
                {
                    if (reassemblyType >= 0)
                        throw new InvalidOperationException("non-fragment frame during reassembly");
                    payload = plaintext;
                }

                switch (type)
                {
                    case NoiseHandshake.MsgTypeJsonBody:
                        HandleJson(Encoding.UTF8.GetString(payload, 1, payload.Length - 1));
                        break;
                    case BinaryTypeAudioChunk:
                        HandleAudioChunk(payload);
                        break;
                    default:
                        // artwork/visualizer/source roles not implemented — ignore
                        break;
                }
            }
        }
        catch (OperationCanceledException) { /* shutdown */ }
        catch (Exception ex)
        {
            if (!ct.IsCancellationRequested)
                _log($"Reader loop ended: {ex.Message}");
        }
        finally
        {
            StopAudio();
            PublishState();
        }
    }

    /// <summary>Receive one complete WebSocket message and decrypt it.</summary>
    private async Task<byte[]> ReceiveTransportFrame(CancellationToken ct)
    {
        var ws = _ws ?? throw new InvalidOperationException("not connected");
        var transport = _transport ?? throw new InvalidOperationException("no transport");

        using var ms = new MemoryStream();
        var chunk = new byte[16 * 1024];
        while (true)
        {
            var result = await ws.ReceiveAsync(new ArraySegment<byte>(chunk), ct);
            if (result.MessageType == WebSocketMessageType.Close)
                throw new OperationCanceledException("WebSocket closed by server");
            ms.Write(chunk, 0, result.Count);
            if (result.EndOfMessage) break;
        }
        var ciphertext = ms.ToArray();
        if (ciphertext.Length == 0) return Array.Empty<byte>();

        var plainBuffer = new byte[NoiseHandshake.MaxTransportPlaintext];
        var read = transport.ReadMessage(ciphertext, plainBuffer);
        var plain = new byte[read];
        Buffer.BlockCopy(plainBuffer, 0, plain, 0, read);
        return plain;
    }

    // ─── Message handlers ─────────────────────────────────────────────

    private void HandleJson(string json)
    {
        string type;
        JsonElement payload;
        using (var doc = JsonDocument.Parse(json))
        {
            var root = doc.RootElement;
            type = root.GetProperty("type").GetString() ?? "";
            payload = root.GetProperty("payload").Clone();
        }

        switch (type)
        {
            case "server/time":
                HandleServerTime(payload);
                break;
            case "server/activate":
                ApplyActivation(payload);
                break;
            case "stream/start":
                HandleStreamStart(payload);
                break;
            case "stream/end":
                HandleStreamEnd(payload);
                break;
            case "stream/clear":
                HandleStreamClear(payload);
                break;
            case "server/command":
                HandleServerCommand(payload);
                break;
            case "server/state":
            case "group/update":
                PublishState(trackInfo: ExtractTrackInfo(type, payload));
                break;
            case "noise/handshake":
                // In-band re-handshake — only used for pairing promotion,
                // which Phase E (Sentinel unpaired access) does not need.
                _log("Received in-band re-handshake request; ignoring (no pairing yet)");
                break;
            default:
                _log($"Unhandled server message type: {type}");
                break;
        }
    }

    private static string? ExtractTrackInfo(string type, JsonElement payload)
    {
        try
        {
            if (type == "group/update")
            {
                if (payload.TryGetProperty("playback_state", out var pb))
                    return pb.GetString();
                return null;
            }
            if (payload.TryGetProperty("metadata", out var md) && md.ValueKind == JsonValueKind.Object)
            {
                var parts = new List<string>();
                if (md.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String)
                    parts.Add(t.GetString() ?? "");
                if (md.TryGetProperty("artist", out var a) && a.ValueKind == JsonValueKind.String)
                    parts.Add(a.GetString() ?? "");
                return parts.Count > 0 ? string.Join(" — ", parts) : null;
            }
        }
        catch { }
        return null;
    }

    private void HandleServerTime(JsonElement payload)
    {
        var clientTransmitted = payload.GetProperty("client_transmitted").GetInt64();
        var serverReceived = payload.GetProperty("server_received").GetInt64();
        var serverTransmitted = payload.GetProperty("server_transmitted").GetInt64();
        var nowUs = NowUs();

        var offset = ((serverReceived - clientTransmitted) + (serverTransmitted - nowUs)) / 2.0;
        var delay = ((nowUs - clientTransmitted) - (serverTransmitted - serverReceived)) / 2.0;

        var wasSynced = _timeFilter.IsSynchronized;
        _timeFilter.Update((long)Math.Round(offset), (long)Math.Round(delay), nowUs);
        if (!wasSynced && _timeFilter.IsSynchronized)
        {
            _log($"Clock sync converged (error ~{_timeFilter.Error} µs)");
            PublishState();
        }
    }

    private void ApplyActivation(JsonElement payload)
    {
        if (payload.TryGetProperty("active_roles", out var roles) && roles.ValueKind == JsonValueKind.Array)
        {
            _playerRoleActive = false;
            foreach (var r in roles.EnumerateArray())
            {
                if (r.GetString() == "player@v1")
                    _playerRoleActive = true;
            }
        }
        PublishState();
    }

    private void HandleStreamStart(JsonElement payload)
    {
        if (!payload.TryGetProperty("player", out var player))
        {
            _log("stream/start without player payload");
            return;
        }
        var codec = player.GetProperty("codec").GetString();
        if (codec != "pcm")
        {
            _log($"Unsupported codec '{codec}' (Phase E: PCM only)");
            lock (_audioLock) { _streamActive = false; }
            return;
        }

        var sampleRate = player.GetProperty("sample_rate").GetInt32();
        var channels = player.GetProperty("channels").GetInt32();
        var bitDepth = player.GetProperty("bit_depth").GetInt32();

        lock (_audioLock)
        {
            var formatChanged = _pcmFormat is null
                || _pcmFormat.SampleRate != sampleRate
                || _pcmFormat.Channels != channels
                || _pcmFormat.BitDepth != bitDepth;
            _pcmFormat = new PcmFormat(sampleRate, channels, bitDepth);
            _streamActive = true;
            if (formatChanged)
                EnsureAudioOutput();
        }
        _log($"stream/start: pcm {sampleRate}Hz/{bitDepth}bit/{channels}ch");
        PublishState();
    }

    private void HandleStreamEnd(JsonElement payload)
    {
        lock (_audioLock) { _streamActive = false; }
        _log("stream/end");
        PublishState();
    }

    private void HandleStreamClear(JsonElement payload)
    {
        lock (_audioLock)
        {
            _buffer?.ClearBuffer();
            _scheduleQueue.Clear();
        }
        _log("stream/clear");
    }

    private void HandleServerCommand(JsonElement payload)
    {
        if (!payload.TryGetProperty("player", out var player)) return;
        var command = player.GetProperty("command").GetString();
        switch (command)
        {
            case "volume":
                _volume = Math.Clamp(player.GetProperty("volume").GetInt32(), 0, 100);
                ApplyVolumeToOutput();
                _ = SendPlayerStateAsync();
                PublishState();
                break;
            case "mute":
                _muted = player.GetProperty("mute").GetBoolean();
                ApplyVolumeToOutput();
                _ = SendPlayerStateAsync();
                PublishState();
                break;
            default:
                _log($"server/command player '{command}' ignored");
                break;
        }
    }

    // ─── Audio scheduling & output ─────────────────────────────────────

    private void HandleAudioChunk(byte[] frame)
    {
        if (frame.Length < BinaryHeaderSize) return;

        // byte0=4, bytes1-8 int64-BE timestamp µs (server clock, first-sample
        // play time), bytes9-12 uint32-BE send_ahead, rest = PCM audio.
        long timestampUs = BinaryPrimitives.ReadInt64BigEndian(frame.AsSpan(1));
        var audio = frame[BinaryHeaderSize..];
        if (audio.Length == 0) return;

        lock (_audioLock)
        {
            // Only accept audio while the server activated our player role.
            if (!_playerRoleActive || !_streamActive || _pcmFormat is null) return;

            // Local play time for the first sample: convert the server
            // timestamp through the time filter, then subtract output delay
            // (DAC/backend latency) so audio exits the port on schedule.
            long playUs = ComputePlayTimeUs(timestampUs);

            long nowUs = NowUs();
            if (playUs <= nowUs)
            {
                // Late chunk — drop (spec: never play stale audio).
                return;
            }
            _scheduleQueue.Add(playUs, audio);
            // send_ahead (bytes 9-12) carries no scheduling meaning — the
            // spec reserves it for arrival-delay measurement only.
        }
    }

    private long ComputePlayTimeUs(long serverTimestampUs)
    {
        if (_timeFilter.IsSynchronized)
        {
            long clientTime = _timeFilter.ComputeClientTime(serverTimestampUs);
            return clientTime - _outputDelayUs;
        }
        return NowUs() + UnsyncedPlayLeadUs - _outputDelayUs;
    }

    private void EnsureAudioOutput()
    {
        if (_waveOut is not null) return;
        var format = _pcmFormat!;
        var waveFormat = new WaveFormat(format.SampleRate, format.BitDepth, format.Channels);
        _buffer = new BufferedWaveProvider(waveFormat)
        {
            BufferDuration = TimeSpan.FromSeconds(10),
            DiscardOnBufferOverflow = true,
        };
        try
        {
            _waveOut = new WasapiOut(AudioClientShareMode.Shared, 100);
        }
        catch
        {
            _waveOut = new WaveOutEvent();
        }
        _waveOut.Init(_buffer);
        _waveOut.Play();
        ApplyVolumeToOutput();
        _log($"Audio output started ({_waveOut.GetType().Name}, " +
             $"{format.SampleRate}Hz/{format.BitDepth}/{format.Channels})");
    }

    private void StopAudio()
    {
        lock (_audioLock)
        {
            try { _waveOut?.Stop(); } catch { }
            try { _waveOut?.Dispose(); } catch { }
            _waveOut = null;
            _buffer = null;
            _scheduleQueue.Clear();
        }
    }

    private void ApplyVolumeToOutput()
    {
        lock (_audioLock)
        {
            if (_waveOut is null) return;
            var vol = _muted ? 0f : _volume / 100f;
            try { _waveOut.Volume = vol; } catch { }
        }
    }

    /// <summary>Watchdog draining the schedule queue into the NAudio buffer.</summary>
    private void SchedulerTick()
    {
        lock (_audioLock)
        {
            if (!_streamActive || _buffer is null) return;
            long nowUs = NowUs();
            // Drain everything due within the jitter-buffer window (oldest first).
            long horizonUs = nowUs + MinBufferMs * 1000L;
            var due = new List<KeyValuePair<long, byte[]>>();
            foreach (var kv in _scheduleQueue)
            {
                if (kv.Key > horizonUs) break;
                due.Add(kv);
            }
            foreach (var kv in due)
            {
                _scheduleQueue.Remove(kv.Key);
                _buffer.AddSamples(kv.Value, 0, kv.Value.Length);
            }
            // Drop chunks that fell behind while we were starved (late = never).
            while (_scheduleQueue.Count > 0)
            {
                var first = _scheduleQueue.Keys.First();
                if (first >= nowUs) break;
                _scheduleQueue.Remove(first);
            }
        }
    }

    // ─── Outbound messages ─────────────────────────────────────────────

    /// <summary>
    /// client/hello — RAW JSON with the exact verified wire format.
    /// Alias key 'player@v1_support' (NEVER 'player_support'), and
    /// supported_pair_methods as an OBJECT ({}).
    /// </summary>
    private string BuildClientHello() => JsonSerializer.Serialize(new Dictionary<string, object?>
    {
        ["payload"] = new Dictionary<string, object?>
        {
            ["name"] = _playerName,
            ["supported_roles"] = new[] { "player@v1" },
            ["trust_level"] = "none",
            ["device_info"] = new Dictionary<string, string?>
            {
                ["product_name"] = "HA DeskLink",
                ["manufacturer"] = "HA DeskLink",
                ["software_version"] = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "5.0.5",
            },
            ["player@v1_support"] = new Dictionary<string, object?>
            {
                ["supported_formats"] = new object[]
                {
                    new Dictionary<string, object?>
                    {
                        ["codec"] = "pcm", ["sample_rate"] = 48000,
                        ["bit_depth"] = 16, ["channels"] = 2,
                    },
                },
                ["buffer_capacity"] = 4194304,
                ["supported_commands"] = new[] { "volume", "mute" },
            },
            ["supported_pair_methods"] = new Dictionary<string, object?>(),
            ["unpaired_access"] = new Dictionary<string, object?> { ["enabled"] = true },
        },
        ["type"] = "client/hello",
    });

    private string BuildClientState(bool available) => JsonSerializer.Serialize(new
    {
        payload = new Dictionary<string, object?>
        {
            ["available"] = available,
            ["player"] = new Dictionary<string, object?>
            {
                ["volume"] = _volume,
                ["muted"] = _muted,
                ["static_delay_ms"] = 0,
                ["required_lead_time_ms"] = 250,
                ["min_buffer_ms"] = 250,
                ["supported_commands"] = new[] { "volume", "mute" },
            },
        },
        type = "client/state",
    });

    private async Task SendPlayerStateAsync()
    {
        if (_transport is null) return;
        try
        {
            await SendJsonAsync(BuildClientState(available: _timeFilter.IsSynchronized),
                _cts?.Token ?? CancellationToken.None);
        }
        catch { }
    }

    private async Task SendJsonAsync(string json, CancellationToken ct)
    {
        var transport = _transport ?? throw new InvalidOperationException("not connected");
        var plaintext = new byte[Encoding.UTF8.GetByteCount(json) + 1];
        plaintext[0] = NoiseHandshake.MsgTypeJsonBody;
        Encoding.UTF8.GetBytes(json, plaintext.AsSpan(1));

        var cipherBuffer = new byte[plaintext.Length + 128];
        await _sendLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var written = transport.WriteMessage(plaintext, cipherBuffer);
            var ciphertext = new byte[written];
            Buffer.BlockCopy(cipherBuffer, 0, ciphertext, 0, written);
            await _ws!.SendAsync(new ArraySegment<byte>(ciphertext),
                WebSocketMessageType.Binary, true, ct);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task SendTextAsync(string text)
    {
        await _sendLock.WaitAsync(CancellationToken.None);
        try
        {
            await _ws!.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(text)),
                WebSocketMessageType.Text, true, CancellationToken.None);
        }
        finally { _sendLock.Release(); }
    }

    private async Task<string> ReceiveTextAsync()
    {
        var ct = _cts?.Token ?? CancellationToken.None;
        var chunk = new byte[16 * 1024];
        using var ms = new MemoryStream();
        while (true)
        {
            var result = await _ws!.ReceiveAsync(new ArraySegment<byte>(chunk), ct);
            if (result.MessageType == WebSocketMessageType.Close)
                throw new OperationCanceledException("WebSocket closed during handshake");
            ms.Write(chunk, 0, result.Count);
            if (result.EndOfMessage) break;
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private async Task<JsonElement?> ReceiveJsonAsync(CancellationToken ct)
    {
        var frame = await ReceiveTransportFrame(ct);
        if (frame.Length == 0 || frame[0] != NoiseHandshake.MsgTypeJsonBody)
            return null;
        using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(frame, 1, frame.Length - 1));
        return doc.RootElement.Clone();
    }

    // ─── Utilities ─────────────────────────────────────────────────────

    private static long NowUs()
    {
        // Monotonic clock in the microsecond domain (matches the Python
        // reference's RawMonotonicClock / loop.time() * 1e6).
        return Stopwatch.GetTimestamp() * 1_000_000 / Stopwatch.Frequency;
    }

    private void PublishState(string? trackInfo = null)
    {
        var state = new PlayerState
        {
            Connected = _ws?.State == WebSocketState.Open,
            TimeSynced = _timeFilter.IsSynchronized,
            Streaming = _streamActive,
            Volume = _volume,
            Muted = _muted,
            SyncErrorUs = _timeFilter.Error,
            ServerName = _serverName,
            TrackInfo = trackInfo ?? _lastState.TrackInfo,
        };
        _lastState = state;
        StateChanged?.Invoke(state);
    }

    private sealed record PcmFormat(int SampleRate, int Channels, int BitDepth);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _cts?.Cancel(); } catch { }
        _schedulerTimer?.Dispose();
        StopAudio();
        try { _ws?.Dispose(); } catch { }
        _sendLock.Dispose();
    }
}