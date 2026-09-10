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
using System.Threading;
using System.Threading.Tasks;

namespace HaDeskLink.Sendspin;

/// <summary>
/// Owns the Sendspin client lifecycle: start/stop/reconnect, config-driven
/// host+name resolution, and UI state events. The Sendspin port derives from
/// the Music Assistant settings section (sendspin runs on the MA host).
/// </summary>
public sealed class SendspinManager : IDisposable
{
    private const int SendspinPort = 8927;

    private readonly Config _config;
    private readonly Action<string> _log;
    private CancellationTokenSource? _cts;
    private Task? _runTask;
    private SendspinClient? _client;
    private volatile bool _running;

    /// <summary>Latest player state snapshot for the UI.</summary>
    public event Action<SendspinClient.PlayerState>? StateChanged;

    /// <summary>Fires once when a background connection attempt failed.</summary>
    public event Action<string>? ConnectionFailed;

    public bool IsRunning => _running;
    public string PlayerName => string.IsNullOrWhiteSpace(_config.SendspinPlayerName)
        ? "HA DeskLink PC"
        : _config.SendspinPlayerName;

    public SendspinManager(Config config, Action<string>? log = null)
    {
        _config = config;
        _log = log ?? (_ => { });
    }

    /// <summary>Start the client if enabled in config and an MA host is set.</summary>
    public void Start()
    {
        if (_running) return;
        if (!_config.SendspinEnabled)
        {
            _log("Sendspin disabled in settings — not starting");
            return;
        }
        var host = _config.MaHost?.Trim();
        if (string.IsNullOrEmpty(host))
        {
            _log("Sendspin enabled but no MA host configured — not starting");
            return;
        }

        _running = true;
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        _runTask = Task.Run(() => RunWithReconnectAsync(host, ct), ct);
    }

    private async Task RunWithReconnectAsync(string host, CancellationToken ct)
    {
        const int reconnectDelayMs = 5000;
        var attempt = 0;
        while (!ct.IsCancellationRequested)
        {
            attempt++;
            var client = new SendspinClient(host, SendspinPort, PlayerName, _log);
            _client = client;
            client.StateChanged += s => StateChanged?.Invoke(s);
            try
            {
                await client.RunAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _log($"Sendspin connection failed (attempt {attempt}): {ex.Message}");
                ConnectionFailed?.Invoke(ex.Message);
            }
            finally
            {
                client.Dispose();
                if (ReferenceEquals(_client, client)) _client = null;
            }

            if (ct.IsCancellationRequested) break;
            // Reconnect backoff
            try { await Task.Delay(reconnectDelayMs, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>Stop the client and cancel reconnects.</summary>
    public void Stop()
    {
        if (!_running) return;
        _running = false;
        try { _cts?.Cancel(); } catch { }
        try { _runTask?.Wait(2000); } catch { }
        _client?.Dispose();
        _client = null;
        _cts?.Dispose();
        _cts = null;
        _log("Sendspin stopped");
    }

    /// <summary>Apply new settings (name changes apply on next reconnect).</summary>
    public void Restart()
    {
        Stop();
        Start();
    }

    public void Dispose() => Stop();
}