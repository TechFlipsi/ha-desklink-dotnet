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
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HaDeskLink;

/// <summary>
/// Music Assistant window — full MA control inside HA DeskLink (WinForms).
/// Layout: header (status) + sidebar (player list) + main area
/// (search bar, results, queue) + bottom bar (now playing + player controls).
/// Dark theme matching the v5 design (#1A1A2E / #16213E / #0F3460).
/// Requires MA config (MaHost/MaPort/MaToken) — shows setup hint if missing.
/// </summary>
public class MusicWindow : Form
{
    // ═══ Dark Theme Farben (wie Linux MusicWindow) ═══
    private static readonly Color BgDark = Color.FromArgb(0x1A, 0x1A, 0x2E);       // #1A1A2E
    private static readonly Color PanelBg = Color.FromArgb(0x16, 0x21, 0x3E);      // #16213E
    private static readonly Color Accent = Color.FromArgb(0x0F, 0x34, 0x60);       // #0F3460
    private static readonly Color AccentLight = Color.FromArgb(0x1A, 0x52, 0x76); // #1A5276
    private static readonly Color Fg = Color.White;
    private static readonly Color FgMuted = Color.FromArgb(0x8C, 0x8C, 0xA0);     // #8C8CA0
    private static readonly Color DangerRed = Color.FromArgb(0xE1, 0x4C, 0x3C);   // #E14C3C

    private readonly Config _config;
    private ListBox _playersList = null!;
    private TextBox _searchBox = null!;
    private ListBox _resultsList = null!;
    private Label _resultsHeader = null!;
    private ListBox _queueList = null!;
    private Label _nowPlayingTitle = null!;
    private Label _nowPlayingArtist = null!;
    private Label _statusText = null!;
    private Button _btnPlayPause = null!;
    private Button _btnShuffle = null!;
    private Button _btnRepeat = null!;
    private TrackBar _volumeSlider = null!;
    private Label _volumeLabel = null!;
    private Label _queueHeader = null!;

    private readonly List<MaMediaItem> _results = new();
    private readonly List<MaQueueItem> _queueItems = new();
    private List<MaPlayer> _players = new();

    private MaPlayer? _selectedPlayer;
    private MaQueue? _selectedQueue;
    private bool _suppressVolumeEvent;
    private bool _shuttingDown;

    private static MusicWindow? _instance;

    public MusicWindow(Config config)
    {
        _config = config;
        Text = "HA DeskLink – " + Localization.Get("music_window_title", "Music Assistant");
        Size = new Size(1080, 700);
        MinimumSize = new Size(820, 540);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = BgDark;
        ForeColor = Fg;

        InitializeComponents();
        _ = InitializeAsync();
    }

    // ═══════════════════════════════════════════════════════
    // INITIALISIERUNG
    // ═══════════════════════════════════════════════════════

    private void InitializeComponents()
    {
        // ─── Header (oben): Titel + Status ───
        var header = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Accent, Padding = new Padding(16, 0, 16, 0) };
        var title = new Label
        {
            Text = "🎵 " + Localization.Get("music_window_title", "Music Assistant"),
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            ForeColor = Fg,
            AutoSize = false,
            Dock = DockStyle.Left,
            Width = 300,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _statusText = new Label
        {
            Text = "",
            Font = new Font("Segoe UI", 9f),
            ForeColor = FgMuted,
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        header.Controls.Add(_statusText);
        header.Controls.Add(title);

        // ─── Sidebar (links): Player-Liste ───
        var sidebar = new Panel { Dock = DockStyle.Left, Width = 230, BackColor = PanelBg, Padding = new Padding(8) };
        var sidebarLabel = new Label
        {
            Text = Localization.Get("music_players", "Player"),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = FgMuted,
            Dock = DockStyle.Top,
            Height = 28,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _playersList = new ListBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackColor = PanelBg,
            ForeColor = Fg,
            Font = new Font("Segoe UI", 10f),
            ItemHeight = 34,
            DrawMode = DrawMode.OwnerDrawFixed,
            IntegralHeight = false,
        };
        _playersList.DrawItem += PlayersList_DrawItem;
        _playersList.SelectedIndexChanged += OnPlayerSelected;
        sidebar.Controls.Add(_playersList);
        sidebar.Controls.Add(sidebarLabel);

        // ─── Main area: Suche + Ergebnisse ───
        var main = new Panel { Dock = DockStyle.Fill, BackColor = BgDark, Padding = new Padding(12) };

        // Suchleiste (oben im Main-Bereich)
        var searchPanel = new Panel { Dock = DockStyle.Top, Height = 44 };
        _searchBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10f),
            BackColor = Accent,
            ForeColor = Fg,
            BorderStyle = BorderStyle.FixedSingle,
        };
        _searchBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; OnSearchClick(); } };
        var btnSearch = MakeFlatButton("🔍", 44, 44, AccentLight, (s, e) => OnSearchClick());
        btnSearch.Dock = DockStyle.Right;
        searchPanel.Controls.Add(_searchBox);
        searchPanel.Controls.Add(btnSearch);

        // Split: Ergebnisse (links, fill) + Queue (rechts, 340px)
        var resultsPanel = new Panel { Dock = DockStyle.Fill, BackColor = PanelBg, Padding = new Padding(8) };
        _resultsHeader = new Label
        {
            Text = Localization.Get("music_results", "Ergebnisse"),
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = Fg,
            Dock = DockStyle.Top,
            Height = 30,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _resultsList = new ListBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackColor = PanelBg,
            ForeColor = Fg,
            Font = new Font("Segoe UI", 10f),
            ItemHeight = 34,
            DrawMode = DrawMode.OwnerDrawFixed,
            IntegralHeight = false,
        };
        _resultsList.DrawItem += ResultsList_DrawItem;
        _resultsList.DoubleClick += (s, e) => OnPlayResult();
        resultsPanel.Controls.Add(_resultsList);
        resultsPanel.Controls.Add(_resultsHeader);

        var queuePanel = new Panel { Dock = DockStyle.Right, Width = 340, BackColor = PanelBg, Padding = new Padding(8) };
        _queueHeader = new Label
        {
            Text = Localization.Get("music_queue", "Warteschlange"),
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = Fg,
            Dock = DockStyle.Top,
            Height = 30,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _queueList = new ListBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackColor = PanelBg,
            ForeColor = Fg,
            Font = new Font("Segoe UI", 9.5f),
            ItemHeight = 30,
            DrawMode = DrawMode.OwnerDrawFixed,
            IntegralHeight = false,
        };
        _queueList.DrawItem += QueueList_DrawItem;
        _queueList.DoubleClick += (s, e) => OnPlayQueueIndex();
        queuePanel.Controls.Add(_queueList);
        queuePanel.Controls.Add(_queueHeader);

        // ─── Now-Playing Bar (unten) ───
        var bottomBar = BuildNowPlayingBar();
        bottomBar.Dock = DockStyle.Bottom;

        // Main-Layout: Dock-Reihenfolge wichtig (Fill zuerst, dann Right)
        main.Controls.Add(resultsPanel);
        main.Controls.Add(queuePanel);
        main.Controls.Add(searchPanel);

        // Form-Layout: Fill zuerst, dann Left, dann Top/Bottom
        Controls.Add(main);
        Controls.Add(sidebar);
        Controls.Add(bottomBar);
        Controls.Add(header);
    }

    /// <summary>Now-Playing bar: Titel/Artist, Prev/PlayPause/Next, Shuffle/Repeat, Volume-Slider.</summary>
    private Panel BuildNowPlayingBar()
    {
        var bar = new Panel { Dock = DockStyle.Bottom, Height = 64, BackColor = Accent, Padding = new Padding(14, 0, 14, 0) };

        // Titel + Artist (links, füllt)
        var titlePanel = new Panel { Dock = DockStyle.Left, Width = 340 };
        _nowPlayingTitle = new Label
        {
            Text = "—",
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = Fg,
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 28,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _nowPlayingArtist = new Label
        {
            Text = Localization.Get("music_nothing_playing", "Nichts abgespielt"),
            Font = new Font("Segoe UI", 9f),
            ForeColor = FgMuted,
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        titlePanel.Controls.Add(_nowPlayingArtist);
        titlePanel.Controls.Add(_nowPlayingTitle);

        // Player-Buttons (zentriert)
        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.None,
            Padding = new Padding(0, 12, 0, 0),
            WrapContents = false,
        };
        var btnPrev = MakeFlatButton("⏮", 44, 40, PanelBg, (s, e) => _ = DoQueueCommand(c => c.PreviousAsync(RequireQueueId())));
        _btnPlayPause = MakeFlatButton("▶", 48, 40, AccentLight, (s, e) => _ = DoQueueCommand(c => c.PlayPauseAsync(RequireQueueId())));
        var btnNext = MakeFlatButton("⏭", 44, 40, PanelBg, (s, e) => _ = DoQueueCommand(c => c.NextAsync(RequireQueueId())));
        btnPrev.Font = new Font("Segoe UI", 12f);
        _btnPlayPause.Font = new Font("Segoe UI", 13f);
        btnNext.Font = new Font("Segoe UI", 12f);
        _btnShuffle = MakeFlatButton("🔀", 44, 40, PanelBg, (s, e) => _ = ToggleShuffle());
        _btnRepeat = MakeFlatButton("🔁", 44, 40, PanelBg, (s, e) => _ = ToggleRepeat());
        btnPanel.Controls.Add(btnPrev);
        btnPanel.Controls.Add(_btnPlayPause);
        btnPanel.Controls.Add(btnNext);
        btnPanel.Controls.Add(_btnShuffle);
        btnPanel.Controls.Add(_btnRepeat);

        // Volume (rechts)
        var volPanel = new Panel { Dock = DockStyle.Right, Width = 220 };
        _volumeSlider = new TrackBar
        {
            Minimum = 0,
            Maximum = 100,
            TickFrequency = 10,
            Width = 150,
            Anchor = AnchorStyles.Left,
            Location = new Point(0, 22),
            AutoSize = false,
        };
        _volumeSlider.ValueChanged += OnVolumeChanged;
        _volumeLabel = new Label
        {
            Text = "—",
            Font = new Font("Segoe UI", 9f),
            ForeColor = Fg,
            AutoSize = false,
            Width = 60,
            Location = new Point(155, 26),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        volPanel.Controls.Add(_volumeSlider);
        volPanel.Controls.Add(_volumeLabel);

        bar.Controls.Add(btnPanel);
        bar.Controls.Add(titlePanel);
        bar.Controls.Add(volPanel);
        return bar;
    }

    private static Button MakeFlatButton(string text, int w, int h, Color back, EventHandler onClick)
    {
        var btn = new Button
        {
            Text = text,
            Width = w,
            Height = h,
            BackColor = back,
            ForeColor = Fg,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10f),
            Margin = new Padding(2),
            TabStop = false,
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += onClick;
        return btn;
    }

    // ═══════════════════════════════════════════════════════
    // VERBINDUNG + EVENTS (Thread-Marshalling: MaManager fired aus WS-Thread)
    // ═══════════════════════════════════════════════════════

    private async Task InitializeAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_config.MaHost))
            {
                SetStatus("❌ " + Localization.Get("music_not_configured",
                    "MA nicht konfiguriert (Einstellungen → Music Assistant)"), DangerRed);
                return;
            }
            SetStatus($"⏳ {Localization.Get("music_connecting", "Verbinde mit")} {_config.MaHost}:{_config.MaPort}...", FgMuted);
            var client = await MaManager.Instance.GetClientAsync();
            SetStatus($"✓ {client.ServerInfo?.Name ?? "MA"} (v{client.ServerInfo?.ServerVersion})", FgMuted);
            MaManager.Instance.StateChanged += OnMaStateChanged;
            RefreshPlayers();
            RefreshState();
            _ = LoadQueueItemsAsync();
        }
        catch (Exception ex)
        {
            SetStatus("❌ " + string.Format(Localization.Get("music_connect_failed", "Verbindung fehlgeschlagen: {0}"), ex.Message), DangerRed);
        }
    }

    private void OnMaStateChanged()
    {
        // Event kommt aus dem WebSocket-Thread → auf UI-Thread marshaln
        if (_shuttingDown || IsDisposed) return;
        try
        {
            BeginInvoke(new Action(() =>
            {
                try
                {
                    _selectedPlayer = MaManager.Instance.SelectedPlayer;
                    _selectedQueue = MaManager.Instance.SelectedQueue;
                    RefreshPlayers();
                    RefreshState();
                    _ = LoadQueueItemsAsync();
                }
                catch { }
            }));
        }
        catch (InvalidOperationException) { /* Form already closed */ }
    }

    // ═══════════════════════════════════════════════════════
    // UI-REFRESH
    // ═══════════════════════════════════════════════════════

    private void RefreshPlayers()
    {
        _players = MaManager.Instance.Players;
        var selected = _selectedPlayer?.PlayerId;
        _playersList.BeginUpdate();
        _playersList.Items.Clear();
        foreach (var p in _players)
        {
            var icon = p.State switch
            {
                "playing" => "▶",
                "paused" => "⏸",
                _ => "⏹"
            };
            _playersList.Items.Add($"{icon}  {p.Name}");
            if (p.PlayerId == selected) _playersList.SelectedIndex = _playersList.Items.Count - 1;
        }
        _playersList.EndUpdate();

        // Volume-Slider synchronisieren
        if (_selectedPlayer?.VolumeLevel is int vol)
        {
            _suppressVolumeEvent = true;
            try
            {
                _volumeSlider.Value = Math.Clamp(vol, 0, 100);
                _volumeLabel.Text = $"{vol}%";
            }
            finally { _suppressVolumeEvent = false; }
        }
    }

    private void RefreshState()
    {
        var queue = _selectedQueue;
        var current = queue?.CurrentItem;
        if (current != null)
        {
            _nowPlayingTitle.Text = current.Name;
            var artists = current.Artists != null ? string.Join(", ", current.Artists.Select(a => a.Name)) : "";
            _nowPlayingArtist.Text = artists;
            _btnPlayPause.Text = queue?.State == "playing" ? "⏸" : "▶";
        }
        else
        {
            _nowPlayingTitle.Text = _selectedPlayer != null
                ? Localization.Get("music_nothing_playing", "Nichts abgespielt")
                : Localization.Get("music_no_player_selected", "Kein Player gewählt");
            _nowPlayingArtist.Text = _selectedPlayer?.Name ?? "";
            _btnPlayPause.Text = "▶";
        }

        // Shuffle/Repeat-Anzeige (Aktiv = AccentLight)
        _btnShuffle.BackColor = queue?.ShuffleEnabled == true ? AccentLight : PanelBg;
        _btnRepeat.BackColor = queue != null && queue.RepeatMode != "off" ? AccentLight : PanelBg;
        _btnRepeat.Text = queue?.RepeatMode == "one" ? "🔂" : "🔁";
    }

    private async Task LoadQueueItemsAsync()
    {
        if (_selectedPlayer == null) { _queueItems.Clear(); RebindQueue(); return; }
        try
        {
            var client = await MaManager.Instance.GetClientAsync();
            var queueId = _selectedPlayer.ActiveQueueId ?? _selectedPlayer.PlayerId;
            var items = await client.GetQueueItemsAsync(queueId, 200);
            if (_shuttingDown || IsDisposed) return;
            BeginInvoke(new Action(() =>
            {
                _queueItems.Clear();
                _queueItems.AddRange(items);
                RebindQueue();
            }));
        }
        catch (Exception ex)
        {
            if (_shuttingDown || IsDisposed) return;
            try
            {
                BeginInvoke(new Action(() =>
                {
                    _queueItems.Clear();
                    RebindQueue();
                    _queueHeader.Text = "❌ " + ex.Message;
                }));
            }
            catch (InvalidOperationException) { }
        }
    }

    private void RebindQueue()
    {
        _queueList.BeginUpdate();
        _queueList.Items.Clear();
        foreach (var it in _queueItems)
        {
            var artist = it.Artists is { Count: > 0 } ? " – " + string.Join(", ", it.Artists.Select(x => x.Name)) : "";
            _queueList.Items.Add($"{it.Name}{artist}");
        }
        if (_queueItems.Count == 0)
            _queueList.Items.Add(Localization.Get("music_queue_empty", "Leer"));
        _queueList.EndUpdate();
    }

    // ═══════════════════════════════════════════════════════
    // SUCHE
    // ═══════════════════════════════════════════════════════

    private void OnSearchClick()
    {
        var q = _searchBox.Text?.Trim();
        if (!string.IsNullOrEmpty(q)) _ = DoSearchAsync(q);
    }

    private async Task DoSearchAsync(string query)
    {
        SetStatus("🔍 " + Localization.Get("music_searching", "Suche..."), FgMuted);
        try
        {
            var client = await MaManager.Instance.GetClientAsync();
            var result = await client.SearchAsync(query, 15);
            if (_shuttingDown || IsDisposed) return;
            BeginInvoke(new Action(() =>
            {
                _results.Clear();
                AddSection(Localization.Get("music_tracks", "Tracks"), result.Tracks, _results);
                AddSection(Localization.Get("music_albums", "Alben"), result.Albums, _results);
                AddSection(Localization.Get("music_artists", "Artists"), result.Artists, _results);
                AddSection(Localization.Get("music_playlists", "Playlists"), result.Playlists, _results);
                AddSection(Localization.Get("music_radio", "Radio"), result.Radio, _results);
                _resultsHeader.Text = string.Format(Localization.Get("music_results_count", "Ergebnisse: {0}"), _results.Count);
                SetStatus(_results.Count == 0
                    ? Localization.Get("music_no_results", "Keine Treffer")
                    : $"✓ {query}", FgMuted);
            }));
        }
        catch (Exception ex)
        {
            SetStatus("❌ " + string.Format(Localization.Get("music_search_failed", "Suchfehler: {0}"), ex.Message), DangerRed);
        }
    }

    /// <summary>Fügt eine Sektion (Tracks/Alben/...) in die flache Ergebnisliste ein — Sektion-Header als Dummy-Items.</summary>
    private static void AddSection(string header, List<MaMediaItem>? items, List<MaMediaItem> target)
    {
        if (items == null || items.Count == 0) return;
        target.Add(new MaMediaItem { Name = $"── {header} ──", Uri = "", MediaType = "__section__" });
        target.AddRange(items);
    }

    private void OnPlayResult()
    {
        var idx = _resultsList.SelectedIndex;
        if (idx < 0 || idx >= _results.Count) return;
        var item = _results[idx];
        if (item.MediaType == "__section__") return;
        _ = PlayMediaItem(item);
    }

    private async Task PlayMediaItem(MaMediaItem item)
    {
        try
        {
            var client = await MaManager.Instance.GetClientAsync();
            var playerId = _selectedPlayer?.PlayerId ?? _players.FirstOrDefault()?.PlayerId;
            if (playerId == null)
            {
                SetStatus("❌ " + Localization.Get("music_no_player_selected", "Kein Player gewählt"), DangerRed);
                return;
            }
            var queueId = _selectedPlayer?.ActiveQueueId ?? playerId;
            var isRadio = item.MediaType == "radio";
            if (isRadio)
                await client.PlayMediaRadioAsync(queueId, item.Uri);
            else
                await client.PlayMediaAsync(queueId, item.Uri, "play");
            SetStatus($"▶ {item.Name}", FgMuted);
        }
        catch (Exception ex)
        {
            SetStatus("❌ " + ex.Message, DangerRed);
        }
    }

    // ═══════════════════════════════════════════════════════
    // PLAYER / QUEUE AKTIONEN
    // ═══════════════════════════════════════════════════════

    private async void OnPlayerSelected(object? sender, EventArgs e)
    {
        var idx = _playersList.SelectedIndex;
        if (idx < 0 || idx >= _players.Count) return;
        var player = _players[idx];
        try
        {
            await MaManager.Instance.SelectPlayerAsync(player.PlayerId);
            _selectedPlayer = MaManager.Instance.SelectedPlayer;
            _selectedQueue = MaManager.Instance.SelectedQueue;
            RefreshState();
            RefreshPlayers();
            _ = LoadQueueItemsAsync();
        }
        catch (Exception ex)
        {
            SetStatus("❌ " + ex.Message, DangerRed);
        }
    }

    private void OnPlayQueueIndex()
    {
        var idx = _queueList.SelectedIndex;
        if (idx < 0 || idx >= _queueItems.Count) return;
        var queueId = RequireQueueId();
        _ = DoQueueCommand(c => c.PlayIndexAsync(queueId, idx));
    }

    private async Task DoQueueCommand(Func<MusicAssistantClient, Task> action)
    {
        try
        {
            var client = await MaManager.Instance.GetClientAsync();
            await action(client);
        }
        catch (Exception ex)
        {
            SetStatus("❌ " + ex.Message, DangerRed);
        }
    }

    private string RequireQueueId()
    {
        var q = _selectedQueue?.QueueId;
        if (!string.IsNullOrEmpty(q)) return q;
        return _selectedPlayer?.ActiveQueueId ?? _selectedPlayer?.PlayerId
            ?? throw new MaException(Localization.Get("music_no_player_selected", "Kein Player gewählt"), 0);
    }

    private async Task ToggleShuffle()
    {
        try
        {
            var client = await MaManager.Instance.GetClientAsync();
            var queueId = RequireQueueId();
            var newState = !(_selectedQueue?.ShuffleEnabled ?? false);
            await client.SetShuffleAsync(queueId, newState);
        }
        catch (Exception ex) { SetStatus("❌ " + ex.Message, DangerRed); }
    }

    private async Task ToggleRepeat()
    {
        try
        {
            var client = await MaManager.Instance.GetClientAsync();
            var queueId = RequireQueueId();
            var mode = _selectedQueue?.RepeatMode switch
            {
                "off" => "all",
                "all" => "one",
                _ => "off"
            };
            await client.SetRepeatAsync(queueId, mode);
        }
        catch (Exception ex) { SetStatus("❌ " + ex.Message, DangerRed); }
    }

    private void OnVolumeChanged(object? sender, EventArgs e)
    {
        if (_suppressVolumeEvent) return;
        var vol = _volumeSlider.Value;
        _volumeLabel.Text = $"{vol}%";
        if (_selectedPlayer == null) return;
        var playerId = _selectedPlayer.PlayerId;
        _ = Task.Run(async () =>
        {
            try
            {
                var client = await MaManager.Instance.GetClientAsync();
                await client.SetVolumeAsync(playerId, vol);
            }
            catch { }
        });
    }

    // ═══════════════════════════════════════════════════════
    // OWNER-DRAW (dunkle Listen ohne weiße Auswahl-Highlights)
    // ═══════════════════════════════════════════════════════

    private void PlayersList_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        e.DrawBackground();
        var selected = (e.State & DrawItemState.Selected) != 0;
        using var bg = new SolidBrush(selected ? Accent : PanelBg);
        e.Graphics.FillRectangle(bg, e.Bounds);
        TextRenderer.DrawText(e.Graphics, _playersList.Items[e.Index].ToString(),
            e.Font, e.Bounds, selected ? Fg : FgMuted, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        e.DrawFocusRectangle();
    }

    private void ResultsList_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _results.Count) return;
        var item = _results[e.Index];
        e.DrawBackground();
        var selected = (e.State & DrawItemState.Selected) != 0;
        using var bg = new SolidBrush(selected ? Accent : PanelBg);
        e.Graphics.FillRectangle(bg, e.Bounds);

        var isSection = item.MediaType == "__section__";
        var sub = !isSection && item.MediaType == "track" && item.Artists is { Count: > 0 }
            ? " – " + string.Join(", ", item.Artists.Select(a => a.Name))
            : "";
        var color = isSection ? FgMuted : Fg;
        var font = isSection
            ? new Font("Segoe UI", 9f, FontStyle.Bold)
            : new Font("Segoe UI", 9.5f);
        TextRenderer.DrawText(e.Graphics, $"{item.Name}{sub}", font, e.Bounds, color,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        if (!isSection) font.Dispose();
        e.DrawFocusRectangle();
    }

    private void QueueList_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _queueItems.Count) return;
        var item = _queueItems[e.Index];
        e.DrawBackground();
        var selected = (e.State & DrawItemState.Selected) != 0;
        var isCurrent = _selectedQueue?.CurrentItem?.QueueItemId == item.QueueItemId;
        using var bg = new SolidBrush(selected ? Accent : PanelBg);
        e.Graphics.FillRectangle(bg, e.Bounds);

        var prefix = isCurrent ? "♪ " : "";
        var artist = item.Artists is { Count: > 0 } ? " – " + string.Join(", ", item.Artists.Select(x => x.Name)) : "";
        using var font = new Font("Segoe UI", 9f, isCurrent ? FontStyle.Bold : FontStyle.Regular);
        TextRenderer.DrawText(e.Graphics, $"{prefix}{item.Name}{artist}", font, e.Bounds,
            isCurrent ? Fg : FgMuted, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        e.DrawFocusRectangle();
    }

    private void SetStatus(string text, Color color)
    {
        if (_shuttingDown || IsDisposed) return;
        try { BeginInvoke(new Action(() => { _statusText.Text = text; _statusText.ForeColor = color; })); }
        catch (InvalidOperationException) { }
    }

    // ═══════════════════════════════════════════════════════
    // ÖFFNEN / SCHLIESSEN (Singleton wie SettingsWindow)
    // ═══════════════════════════════════════════════════════

    public static void Open(Config config)
    {
        if (_instance != null && !_instance.IsDisposed)
        {
            _instance.Activate();
            return;
        }
        _instance = new MusicWindow(config);
        _instance.Show();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _shuttingDown = true;
        MaManager.Instance.StateChanged -= OnMaStateChanged;
        _instance = null;
        base.OnFormClosed(e);
    }
}