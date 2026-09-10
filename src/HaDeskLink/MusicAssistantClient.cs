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
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace HaDeskLink;

// ============================================================
// Music Assistant WebSocket API client.
// Protocol: ws://<host>:<port>/ws — first message is ServerInfo,
// then client sends {"command":"auth","args":{"token":...}}.
// Commands: {message_id, command, args}; replies carry the same
// message_id plus "result" or "error_code"/"details".
// After auth the server pushes events: {event, data}.
// Works regardless of where MA runs (HA add-on, TrueNAS/Docker, standalone) —
// user supplies host + port + long-lived token from the MA web UI.
// ============================================================

public class MaServerInfo
{
    [JsonPropertyName("server_id")] public string? ServerId { get; set; }
    [JsonPropertyName("server_version")] public string? ServerVersion { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("base_url")] public string? BaseUrl { get; set; }
    [JsonPropertyName("homeassistant_addon")] public bool IsHaAddon { get; set; }
    [JsonPropertyName("onboard_done")] public bool OnboardDone { get; set; }
}

public class MaPlayer
{
    [JsonPropertyName("player_id")] public string PlayerId { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("state")] public string State { get; set; } = "idle";
    [JsonPropertyName("available")] public bool Available { get; set; }
    [JsonPropertyName("active_queue_id")] public string? ActiveQueueId { get; set; }
    [JsonPropertyName("group_identifier")] public string? GroupIdentifier { get; set; }
    [JsonPropertyName("volume_level")] public int? VolumeLevel { get; set; }
    [JsonPropertyName("volume_muted")] public bool? VolumeMuted { get; set; }
    [JsonPropertyName("current_media_title")] public string? CurrentMediaTitle { get; set; }
    [JsonPropertyName("current_media_artist")] public string? CurrentMediaArtist { get; set; }
    [JsonPropertyName("elapsed_time")] public double? ElapsedTime { get; set; }
    [JsonPropertyName("powered")] public bool? Powered { get; set; }
}

public class MaItemArtist
{
    [JsonPropertyName("uri")] public string? Uri { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
}

public class MaQueueItem
{
    [JsonPropertyName("queue_item_id")] public string QueueItemId { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("artists")] public List<MaItemArtist>? Artists { get; set; }
    [JsonPropertyName("album")] public MaMediaItem? Album { get; set; }
    [JsonPropertyName("duration")] public int? Duration { get; set; }
    [JsonPropertyName("image")] public string? Image { get; set; }
    [JsonPropertyName("uri")] public string? Uri { get; set; }
}

public class MaQueue
{
    [JsonPropertyName("queue_id")] public string QueueId { get; set; } = "";
    [JsonPropertyName("player_id")] public string PlayerId { get; set; } = "";
    [JsonPropertyName("state")] public string State { get; set; } = "idle";
    [JsonPropertyName("current_item")] public MaQueueItem? CurrentItem { get; set; }
    [JsonPropertyName("shuffle_enabled")] public bool ShuffleEnabled { get; set; }
    [JsonPropertyName("repeat_mode")] public string RepeatMode { get; set; } = "off";
    [JsonPropertyName("radio_mode")] public bool RadioMode { get; set; }
    [JsonPropertyName("index")] public int? Index { get; set; }
}

public class MaMediaItem
{
    [JsonPropertyName("uri")] public string Uri { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("media_type")] public string MediaType { get; set; } = "";
    [JsonPropertyName("artists")] public List<MaItemArtist>? Artists { get; set; }
    [JsonPropertyName("album")] public MaMediaItem? Album { get; set; }
    [JsonPropertyName("duration")] public int? Duration { get; set; }
    [JsonPropertyName("item_id")] public string? ItemId { get; set; }
    [JsonPropertyName("provider")] public string? Provider { get; set; }
    [JsonPropertyName("metadata")] public MaMediaMetadata? Metadata { get; set; }

    /// <summary>Best available image URL (thumb preferred, else first image path).</summary>
    public string? Image => Metadata?.Images?
        .FirstOrDefault(i => i.Type == "thumb")?.Url
        ?? Metadata?.Images?.FirstOrDefault()?.Url;
}

/// <summary>MA metadata.images entry (type: thumb|fanart|...; path/url optional).</summary>
public class MaImage
{
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("path")] public string? Path { get; set; }
    [JsonPropertyName("url")] public string? Url { get; set; }
}

public class MaMediaMetadata
{
    [JsonPropertyName("images")] public List<MaImage>? Images { get; set; }
}

// music/search returns {artists: [...], albums: [...], tracks: [...], playlists: [...], radio: [...], ...}
// Each array element is a full MediaItem (item_id/provider/name/uri/...).
public class MaSearchResult
{
    [JsonPropertyName("artists")] public List<MaMediaItem>? Artists { get; set; }
    [JsonPropertyName("albums")] public List<MaMediaItem>? Albums { get; set; }
    [JsonPropertyName("tracks")] public List<MaMediaItem>? Tracks { get; set; }
    [JsonPropertyName("playlists")] public List<MaMediaItem>? Playlists { get; set; }
    [JsonPropertyName("radio")] public List<MaMediaItem>? Radio { get; set; }

    public int TotalHits => (Artists?.Count ?? 0) + (Albums?.Count ?? 0) +
                            (Tracks?.Count ?? 0) + (Playlists?.Count ?? 0) +
                            (Radio?.Count ?? 0);
}

/// <summary>Thrown when MA replies with error_code to a command.</summary>
public class MaException : Exception
{
    public int ErrorCode { get; }
    public MaException(string message, int errorCode) : base(message) { ErrorCode = errorCode; }
}

public class MusicAssistantClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _wsUrl;
    private readonly string _token;
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private int _messageId;
    private volatile bool _disposed;
    private bool _wasConnected;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private readonly Dictionary<int, TaskCompletionSource<JsonElement>> _pending = new();
    private readonly object _pendingLock = new();

    /// <summary>Server info received on connect (null until connected).</summary>
    public MaServerInfo? ServerInfo { get; private set; }
    /// <summary>True when authenticated and receiving events.</summary>
    public bool IsConnected { get; private set; }

    /// <summary>Player state changed.</summary>
    public event Action<MaPlayer>? PlayerUpdated;
    /// <summary>Queue state changed.</summary>
    public event Action<MaQueue>? QueueUpdated;
    /// <summary>Connection lost (auto-reconnect is caller's responsibility).
    /// Only fires after a fully established connection — NOT on Dispose()
    /// or a failed ConnectAsync, so the manager won't start zombie
    /// reconnect timers for intentionally closed clients.</summary>
    public event Action? Disconnected;

    public MusicAssistantClient(string host, int port, string token)
    {
        _wsUrl = $"ws://{host}:{port}/ws";
        _token = token;
    }

    public MusicAssistantClient(Uri wsUri, string token)
    {
        _wsUrl = wsUri.ToString();
        _token = token;
    }

    /// <summary>
    /// Connect + authenticate. Returns server info and starts the receive loop
    /// that dispatches push events (player/queue updates) to the events above.
    /// </summary>
    public async Task<MaServerInfo> ConnectAsync(CancellationToken? externalCt = null)
    {
        _cts?.Cancel();
        _cts?.Dispose(); // don't leak the previous CTS on repeated connects
        _cts = new CancellationTokenSource();
        var ct = externalCt ?? _cts.Token;

        var ws = new ClientWebSocket();
        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        connectCts.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            await ws.ConnectAsync(new Uri(_wsUrl), connectCts.Token).ConfigureAwait(false);
        }
        catch
        {
            ws.Dispose(); // never leave a half-open socket behind
            throw;
        }
        _ws = ws;

        try
        {
            // 1) ServerInfo arrives unsolicited as the very first message
            var first = await ReceiveMessageAsync(ct).ConfigureAwait(false);
            if (first.ValueKind != JsonValueKind.Object || !first.TryGetProperty("server_id", out _))
                throw new MaException("Expected ServerInfo as first message, got: " + first, 0);
            ServerInfo = first.Deserialize<MaServerInfo>(JsonOpts);

            // 2) start background receive loop BEFORE any command so the auth reply
            //    is routed to CommandAsync's pending task
            _ = Task.Run(() => ReceiveLoopAsync(_cts.Token), CancellationToken.None);

            // 3) authenticate with the long-lived token (throws MaException on bad token)
            await CommandAsync("auth", new Dictionary<string, object?> { ["token"] = _token }, ct).ConfigureAwait(false);
            _wasConnected = true;
            IsConnected = true;

            return ServerInfo!;
        }
        catch
        {
            // On any failure (bad token, ServerInfo timeout, ...) tear the socket
            // and the receive loop down — otherwise the loop would keep running
            // on the open socket forever (thread + handle leak per attempt).
            CleanupConnection();
            throw;
        }
    }

    public async Task<JsonElement> CommandAsync(string command, Dictionary<string, object?>? args = null, CancellationToken? ct = null)
    {
        var ws = _ws ?? throw new MaException("Not connected", 0);
        var token = ct ?? CancellationToken.None;
        var mid = Interlocked.Increment(ref _messageId);
        var payload = new Dictionary<string, object?>
        {
            ["message_id"] = mid.ToString(),
            ["command"] = command,
            ["args"] = args ?? new Dictionary<string, object?>()
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOpts);

        // Register the pending entry BEFORE sending: the receive loop is already
        // running, so a fast server reply could otherwise arrive before this
        // command's TCS is registered — the reply would be dropped and the
        // command would hang until its 30s timeout.
        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_pendingLock) { _pending[mid] = tcs; }
        try
        {
            await _sendLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                await ws.SendAsync(bytes, WebSocketMessageType.Text, true, token).ConfigureAwait(false);
            }
            finally
            {
                // Dispose-safe release: Dispose() may run concurrently with an
                // in-flight command (app shutdown), which disposes the semaphore.
                try { _sendLock.Release(); } catch (ObjectDisposedException) { }
            }
        }
        catch
        {
            lock (_pendingLock) { _pending.Remove(mid); }
            throw;
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));
            using var reg = timeoutCts.Token.Register(
                () => tcs.TrySetException(new TimeoutException($"MA command '{command}' timed out")));
            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            lock (_pendingLock) { _pending.Remove(mid); }
        }
    }

    // ---------- convenience wrappers ----------

    public async Task<List<MaPlayer>> GetAllPlayersAsync(CancellationToken? ct = null)
    {
        var res = await CommandAsync("players/all", null, ct).ConfigureAwait(false);
        return DeserializeList<MaPlayer>(res);
    }

    public async Task<MaQueue?> GetActiveQueueAsync(string playerId, CancellationToken? ct = null)
    {
        var res = await CommandAsync("player_queues/get_active_queue",
            new Dictionary<string, object?> { ["player_id"] = playerId }, ct).ConfigureAwait(false);
        if (res.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        return res.Deserialize<MaQueue>(JsonOpts);
    }

    public async Task<List<MaQueueItem>> GetQueueItemsAsync(string queueId, int limit = 100, int offset = 0, CancellationToken? ct = null)
    {
        var res = await CommandAsync("player_queues/items", new Dictionary<string, object?>
        {
            ["queue_id"] = queueId, ["limit"] = limit, ["offset"] = offset
        }, ct).ConfigureAwait(false);
        return DeserializeList<MaQueueItem>(res);
    }

    public Task PlayPauseAsync(string queueId, CancellationToken? ct = null) =>
        CommandAsync("player_queues/play_pause", new Dictionary<string, object?> { ["queue_id"] = queueId }, ct);

    public Task NextAsync(string queueId, CancellationToken? ct = null) =>
        CommandAsync("player_queues/next", new Dictionary<string, object?> { ["queue_id"] = queueId }, ct);

    public Task PreviousAsync(string queueId, CancellationToken? ct = null) =>
        CommandAsync("player_queues/previous", new Dictionary<string, object?> { ["queue_id"] = queueId }, ct);

    public Task StopAsync(string queueId, CancellationToken? ct = null) =>
        CommandAsync("player_queues/stop", new Dictionary<string, object?> { ["queue_id"] = queueId }, ct);

    public Task SetVolumeAsync(string playerId, int volumePercent, CancellationToken? ct = null) =>
        CommandAsync("players/cmd/volume_set", new Dictionary<string, object?>
        {
            ["player_id"] = playerId, ["volume_level"] = Math.Clamp(volumePercent, 0, 100)
        }, ct);

    public Task SetMutedAsync(string playerId, bool muted, CancellationToken? ct = null) =>
        CommandAsync("players/cmd/volume_mute", new Dictionary<string, object?>
        {
            ["player_id"] = playerId, ["muted"] = muted
        }, ct);

    /// <summary>Play a media URI on the given queue. enqueue: play|replace|next|replace_next|add.</summary>
    public Task PlayMediaAsync(string queueId, string uri, string enqueue = "play", CancellationToken? ct = null) =>
        CommandAsync("player_queues/play_media", new Dictionary<string, object?>
        {
            ["queue_id"] = queueId, ["media"] = uri, ["enqueue"] = enqueue
        }, ct);

    /// <summary>Start radio mode for a media URI (endless mix / radio playlist).</summary>
    public Task PlayMediaRadioAsync(string queueId, string uri, CancellationToken? ct = null) =>
        CommandAsync("player_queues/play_media", new Dictionary<string, object?>
        {
            ["queue_id"] = queueId, ["media"] = uri, ["radio_mode"] = true
        }, ct);

    public Task SetShuffleAsync(string queueId, bool enabled, CancellationToken? ct = null) =>
        CommandAsync("player_queues/shuffle", new Dictionary<string, object?>
        { ["queue_id"] = queueId, ["shuffle_enabled"] = enabled }, ct);

    public Task SetRepeatAsync(string queueId, string mode, CancellationToken? ct = null) =>
        CommandAsync("player_queues/repeat", new Dictionary<string, object?>
        { ["queue_id"] = queueId, ["repeat_mode"] = mode }, ct);

    public Task MoveQueueItemAsync(string queueId, string queueItemId, int insertPosition, CancellationToken? ct = null) =>
        CommandAsync("player_queues/move_item", new Dictionary<string, object?>
        { ["queue_id"] = queueId, ["queue_item_id"] = queueItemId, ["insert_position"] = insertPosition }, ct);

    public Task DeleteQueueItemAsync(string queueId, string queueItemId, CancellationToken? ct = null) =>
        CommandAsync("player_queues/delete_item", new Dictionary<string, object?>
        { ["queue_id"] = queueId, ["queue_item_id"] = queueItemId }, ct);

    public Task ClearQueueAsync(string queueId, CancellationToken? ct = null) =>
        CommandAsync("player_queues/clear", new Dictionary<string, object?> { ["queue_id"] = queueId }, ct);

    public Task PlayIndexAsync(string queueId, int index, CancellationToken? ct = null) =>
        CommandAsync("player_queues/play_index", new Dictionary<string, object?>
        { ["queue_id"] = queueId, ["index"] = index }, ct);

    public Task TransferQueueAsync(string sourcePlayerId, string targetPlayerId, bool autoPlay = true, CancellationToken? ct = null) =>
        CommandAsync("player_queues/transfer", new Dictionary<string, object?>
        { ["source_player_id"] = sourcePlayerId, ["target_player_id"] = targetPlayerId, ["auto_play"] = autoPlay }, ct);

    /// <summary>Global search across library + all providers.</summary>
    public async Task<MaSearchResult> SearchAsync(string query, int limit = 25, CancellationToken? ct = null)
    {
        var res = await CommandAsync("music/search", new Dictionary<string, object?>
        {
            ["search_query"] = query, ["limit"] = limit
        }, ct).ConfigureAwait(false);
        return res.Deserialize<MaSearchResult>(JsonOpts) ?? new MaSearchResult();
    }

    /// <summary>Browse library hierarchy (no uri = root listing).</summary>
    public async Task<List<MaMediaItem>> BrowseAsync(string? uri = null, int limit = 100, CancellationToken? ct = null)
    {
        var args = new Dictionary<string, object?> { ["limit"] = limit };
        if (!string.IsNullOrEmpty(uri)) args["uri"] = uri;
        var res = await CommandAsync("music/browse", args, ct).ConfigureAwait(false);
        return DeserializeList<MaMediaItem>(res);
    }

    /// <summary>List favorite items (library items marked with a heart).</summary>
    public async Task<List<MaMediaItem>> GetFavoritesAsync(int limit = 100, CancellationToken? ct = null)
    {
        // MA 2.x: favorites are queried per media type via library_items with favorite=true
        var result = new List<MaMediaItem>();
        foreach (var type in new[] { "tracks", "artists", "albums", "playlists", "radios" })
        {
            var res = await CommandAsync($"music/{type}/library_items",
                new Dictionary<string, object?> { ["favorite"] = true, ["limit"] = limit }, ct).ConfigureAwait(false);
            result.AddRange(DeserializeList<MaMediaItem>(res));
        }
        return result;
    }

    public Task AddFavoriteAsync(string itemUri, CancellationToken? ct = null) =>
        CommandAsync("music/favorites/add_item", new Dictionary<string, object?> { ["item_uri"] = itemUri }, ct);

    public Task RemoveFavoriteAsync(string itemUri, CancellationToken? ct = null) =>
        CommandAsync("music/favorites/remove_item", new Dictionary<string, object?> { ["item_uri"] = itemUri }, ct);

    public Task SetSleepTimerAsync(string playerId, int minutes, CancellationToken? ct = null) =>
        CommandAsync("players/sleep_timer/set", new Dictionary<string, object?>
        { ["player_id"] = playerId, ["duration"] = minutes * 60 }, ct);

    public Task ClearSleepTimerAsync(string playerId, CancellationToken? ct = null) =>
        CommandAsync("players/sleep_timer/clear", new Dictionary<string, object?> { ["player_id"] = playerId }, ct);

    // ---------- internals ----------

    /// <summary>Tear the socket + receive loop down without firing Disconnected.</summary>
    private void CleanupConnection()
    {
        IsConnected = false;
        try { _cts?.Cancel(); } catch { }
        try { _ws?.Dispose(); } catch { }
    }

    private async Task<JsonElement> ReceiveMessageAsync(CancellationToken ct)
    {
        var ws = _ws ?? throw new MaException("Not connected", 0);
        var buffer = new byte[256 * 1024];
        // Collect raw bytes and decode ONCE at the end: decoding per chunk can
        // split a multi-byte UTF-8 character across chunk boundaries and
        // produce an invalid string / unparseable JSON.
        using var ms = new MemoryStream();
        while (true)
        {
            var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
                throw new MaException("MA websocket closed during handshake", 0);
            ms.Write(buffer, 0, result.Count);
            if (result.EndOfMessage) break;
        }
        using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(ms.ToArray()));
        return doc.RootElement.Clone();
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[512 * 1024];
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var ws = _ws;
                if (ws == null || ws.State != WebSocketState.Open) break;
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                        throw new OperationCanceledException("MA websocket closed by server");
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                HandleMessage(Encoding.UTF8.GetString(ms.ToArray()));
            }
        }
        catch (OperationCanceledException) { /* normal shutdown */ }
        catch (ObjectDisposedException) { /* disposed during shutdown */ }
        catch (Exception) { /* connection lost */ }
        finally
        {
            IsConnected = false;
            // Only signal a real connection loss — never an intentional
            // Dispose() or a failed ConnectAsync (that would make MaManager
            // spin up reconnect timers for a deliberately closed client).
            if (!_disposed && _wasConnected) Disconnected?.Invoke();
        }
    }

    /// <summary>Route one complete message (command reply or push event).
    /// A malformed message is skipped without killing the receive loop.</summary>
    private void HandleMessage(string json)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException) { return; } // broken message: skip, keep connection alive
        using (doc)
        {
            var root = doc.RootElement;
            if (root.TryGetProperty("message_id", out var midEl) &&
                int.TryParse(midEl.ToString(), out var mid))
            {
                TaskCompletionSource<JsonElement>? tcs;
                lock (_pendingLock) { _pending.TryGetValue(mid, out tcs); }
                if (tcs == null) return; // reply for an already timed-out command
                if (root.TryGetProperty("error_code", out var err))
                {
                    var code = err.ValueKind == JsonValueKind.Number ? err.GetInt32() : 0;
                    var details = root.TryGetProperty("details", out var det) ? det.GetString() ?? "unknown error" : "unknown error";
                    tcs.TrySetException(new MaException(details, code));
                }
                else
                {
                    var res = root.TryGetProperty("result", out var r) ? r.Clone()
                        : JsonSerializer.SerializeToElement(new object());
                    tcs.TrySetResult(res);
                }
            }
            else if (root.TryGetProperty("event", out var evtEl))
            {
                DispatchEvent(evtEl.GetString() ?? "", root);
            }
        }
    }

    private void DispatchEvent(string evtName, JsonElement root)
    {
        try
        {
            var data = root.TryGetProperty("data", out var d) ? d : root;
            switch (evtName)
            {
                case "player_updated":
                    var p = data.Deserialize<MaPlayer>(JsonOpts);
                    if (p != null) PlayerUpdated?.Invoke(p);
                    break;
                case "queue_updated":
                    var q = data.Deserialize<MaQueue>(JsonOpts);
                    if (q != null) QueueUpdated?.Invoke(q);
                    break;
            }
        }
        catch (JsonException) { }
    }

    private static List<T> DeserializeList<T>(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
            return element.Deserialize<List<T>>(JsonOpts) ?? new List<T>();
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty("items", out var items) &&
            items.ValueKind == JsonValueKind.Array)
            return items.Deserialize<List<T>>(JsonOpts) ?? new List<T>();
        return new List<T>();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _cts?.Cancel(); } catch { }
        try { _cts?.Dispose(); } catch { }
        try { _ws?.Dispose(); } catch { }
        try { _sendLock.Dispose(); } catch { }
    }
}