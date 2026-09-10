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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HaDeskLink;

/// <summary>
/// Singleton-style manager for the Music Assistant connection.
/// Owns the MusicAssistantClient lifecycle (connect, auto-reconnect),
/// caches the latest player/queue states for the Music UI, and exposes
/// a thread-safe snapshot API. Created lazily on first Music-Tab open;
/// lives as long as the app so events keep updating the cache.
/// </summary>
public class MaManager : IDisposable
{
    private static MaManager? _instance;
    private static readonly object _instanceLock = new();
    private bool _disposed;

    private MusicAssistantClient? _client;
    private readonly List<MaPlayer> _playersList = new();
    private readonly object _playersLock = new();
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private Timer? _reconnectTimer;

    public static MaManager Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_instanceLock) { _instance ??= new MaManager(); }
            }
            return _instance;
        }
    }

    /// <summary>Latest known players (cached between events). Thread-safe snapshot.</summary>
    public List<MaPlayer> Players
    {
        get { lock (_playersLock) { return _playersList.ToList(); } }
    }

    private MaPlayer? _selectedPlayer;
    private MaQueue? _selectedQueue;

    /// <summary>Current player/queue snapshot of the selected player. Thread-safe.</summary>
    public MaPlayer? SelectedPlayer { get { lock (_playersLock) { return _selectedPlayer; } } }
    public MaQueue? SelectedQueue { get { lock (_playersLock) { return _selectedQueue; } } }

    /// <summary>Fired on any state change (called from WS thread — marshal to UI!).</summary>
    public event Action? StateChanged;
    /// <summary>Fired when connection state changes.</summary>
    public event Action<bool>? ConnectionChanged;

    public bool IsConnected => _client?.IsConnected ?? false;

    private MaManager() { }

    /// <summary>
    /// Ensure a connected client (reconnects if needed). Safe to call repeatedly.
    /// Returns the client or throws on failure (caller shows error).
    /// </summary>
    public async Task<MusicAssistantClient> GetClientAsync()
    {
        if (_disposed) throw new MaException("MA nicht konfiguriert", 0);
        var config = Config.Load();
        if (string.IsNullOrWhiteSpace(config.MaHost))
            throw new MaException("MA nicht konfiguriert", 0);

        var existing = _client;
        if (existing != null && existing.IsConnected) return existing;

        await _connectLock.WaitAsync(_cts.Token).ConfigureAwait(false);
        try
        {
            if (_disposed || _cts.IsCancellationRequested) throw new MaException("MA Manager disposed", 0);

            if (_client != null && _client.IsConnected) return _client;

            // Dispose the old client OUTSIDE the events: the client fires
            // Disconnected from its receive loop, and we only react to that
            // when the connection really dropped (not on our own dispose).
            _client?.Dispose();
            var client = new MusicAssistantClient(config.MaHost, config.MaPort, config.MaToken);
            client.PlayerUpdated += OnPlayerUpdated;
            client.QueueUpdated += OnQueueUpdated;
            client.Disconnected += OnDisconnected;
            await client.ConnectAsync(_cts.Token).ConfigureAwait(false);
            _client = client;

            // Initial full sync
            var players = await client.GetAllPlayersAsync(_cts.Token).ConfigureAwait(false);
            lock (_playersLock)
            {
                _playersList.Clear();
                _playersList.AddRange(players);
            }
            RaiseStateChanged();
            return client;
        }
        finally
        {
            try { _connectLock.Release(); } catch (SemaphoreFullException) { }
        }
    }

    /// <summary>Quick connection test for the settings UI. Throws on failure.</summary>
    public static async Task<MaServerInfo> TestConnectionAsync(string host, int port, string token)
    {
        var test = new MusicAssistantClient(host, port, token);
        try { return await test.ConnectAsync().ConfigureAwait(false); }
        finally { test.Dispose(); }
    }

    /// <summary>Select a player (loads its queue too).</summary>
    public async Task SelectPlayerAsync(string playerId)
    {
        var client = await GetClientAsync().ConfigureAwait(false);
        lock (_playersLock) { _selectedPlayer = _playersList.FirstOrDefault(p => p.PlayerId == playerId); }
        var selectedPlayerId = playerId; // capture — selection may change while loading
        MaQueue? queue = null;
        try
        {
            queue = await client.GetActiveQueueAsync(playerId, _cts.Token).ConfigureAwait(false);
        }
        catch { queue = null; }
        lock (_playersLock)
        {
            if (_selectedPlayer?.PlayerId == selectedPlayerId)
                _selectedQueue = queue;
        }
        RaiseStateChanged();
    }

    private void OnPlayerUpdated(MaPlayer player)
    {
        lock (_playersLock)
        {
            var idx = _playersList.FindIndex(p => p.PlayerId == player.PlayerId);
            if (idx >= 0) _playersList[idx] = player;
            else _playersList.Add(player);

            if (_selectedPlayer?.PlayerId == player.PlayerId)
            {
                _selectedPlayer = player;
                var playerId = player.PlayerId;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var client = _client;
                        if (client == null) return;
                        var q = await client.GetActiveQueueAsync(playerId, _cts.Token).ConfigureAwait(false);
                        lock (_playersLock)
                        {
                            if (_selectedPlayer?.PlayerId == playerId)
                                _selectedQueue = q;
                        }
                        RaiseStateChanged();
                    }
                    catch { }
                });
            }
        }
        RaiseStateChanged();
    }

    private void OnQueueUpdated(MaQueue queue)
    {
        bool relevant;
        lock (_playersLock)
        {
            relevant = _selectedPlayer != null && queue.PlayerId == _selectedPlayer.PlayerId;
            if (relevant) _selectedQueue = queue;
        }
        if (relevant) RaiseStateChanged();
    }

    private void OnDisconnected()
    {
        ConnectionChanged?.Invoke(false);
        StartReconnectTimer();
    }

    private void StartReconnectTimer()
    {
        var timer = _reconnectTimer;
        if (timer != null) return; // already reconnecting

        lock (_instanceLock)
        {
            if (_disposed) return;
            if (_reconnectTimer != null) return;
            _reconnectTimer = new Timer(ReconnectTick, null, 5000, 15000);
        }
    }

    private async void ReconnectTick(object? state)
    {
        if (_disposed) return;
        try
        {
            if (_client != null && _client.IsConnected)
            {
                // Another path (e.g. user action) already reconnected — stop the timer.
                StopReconnectTimer();
                ConnectionChanged?.Invoke(true);
                return;
            }
            await GetClientAsync().ConfigureAwait(false);
            StopReconnectTimer();
            ConnectionChanged?.Invoke(true);
        }
        catch { /* retry next tick */ }
    }

    private void StopReconnectTimer()
    {
        Timer? timer;
        lock (_instanceLock)
        {
            timer = _reconnectTimer;
            _reconnectTimer = null;
        }
        timer?.Dispose();
    }

    private void RaiseStateChanged()
    {
        StateChanged?.Invoke();
    }

    public void Dispose()
    {
        if (_disposed) return;
        lock (_instanceLock)
        {
            if (_disposed) return;
            _disposed = true;
        }
        _cts.Cancel();
        Timer? timer;
        lock (_instanceLock)
        {
            timer = _reconnectTimer;
            _reconnectTimer = null;
        }
        timer?.Dispose();
        _client?.Dispose();
        _connectLock.Dispose();
    }
}