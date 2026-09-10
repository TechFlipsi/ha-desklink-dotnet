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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HaDeskLink;

public class DeskLinkApp
{
    public static DeskLinkApp? Instance { get; private set; }
    private readonly Config _config;
    private readonly HaApiClient _api;
    internal SensorManager? _sensors;
    private WebhookServer? _webhookServer;
    private readonly Dictionary<string, object> _lastSensorStates = new();
    private readonly CancellationTokenSource _cts = new();
    private NotifyIcon? _trayIcon;
    private QuickActionHandler? _quickActionHandler;
    private QuickActionHandler? _dashboardHotkey;
    private QuickActionHandler? _settingsHotkey;
    internal HaWebSocketClient? _wsClient;
    private MqttClient? _mqttClient;
    private MediaPlayer? _mediaPlayer;
    private System.Threading.Timer? _mediaTimer;
    internal WidgetManager? _widgetManager;
    internal HaDeskLink.Sendspin.SendspinManager? _sendspinManager;

    public DeskLinkApp(Config config)
    {
        Instance = this;
        _config = config;
        _api = new HaApiClient(Config.GetConfigDir(), config.VerifySsl);
    }

    public void Run()
    {
        // Clean up any stale update pending marker
        try { File.Delete(Path.Combine(Config.GetConfigDir(), ".update_pending")); } catch { }

        // Load language
        Localization.LoadLanguage(_config.Language);

        if (!_api.LoadRegistration())
        {
            MessageBox.Show(Localization.Get("no_connection"),
                Localization.Get("no_connection_title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Initialize sensor manager
        try
        {
            _sensors = new SensorManager();
        }
        catch (Exception ex)
        {
            File.WriteAllText(Program.LogFile(), $"[SensorManager] Failed to initialize sensor manager: {ex}");
            _sensors = null;
        }

        // Setup tray FIRST (needed for notifications)
        SetupTray();

        // Capture UI thread context for cross-thread marshaling (notification toasts)
        NotificationHandler.UiContext = SynchronizationContext.Current;

        // Check if token is available (encryption/migration may fail)
        if (string.IsNullOrEmpty(_config.HaToken))
        {
            _trayIcon?.ShowBalloonTip(10000, "HA DeskLink – Fehler",
                "Token konnte nicht geladen werden. Bitte App neu einrichten.", ToolTipIcon.Error);
            return;
        }

        // Start WebSocket connection for push notifications
        var webhookId = _api.GetWebhookId();
        var wsClient = new HaWebSocketClient(_config.HaUrl, _config.HaToken, webhookId, _trayIcon,
            cmd => CommandHandler.Execute(cmd), verifySsl: _config.VerifySsl);
        _wsClient = wsClient;

        // ── Desktop-Widgets (WorkerW-Schicht) ─────────────────────────
        // Ein geteilter WebSocket-Kanal → Fan-out an alle Widgets.
        // subscribe_entities liefert Push-Events; zusätzlich aktualisiert
        // das SensorInterval-Polling (unten) als Fallback.
        try
        {
            _widgetManager = new WidgetManager(_config, _api);
            wsClient.EntityStateChanged += (entityId, state, unit) =>
                _widgetManager?.OnEntityStateChanged(entityId, state, unit);
            _widgetManager.StartAll();
        }
        catch (Exception ex)
        {
            File.AppendAllText(Program.LogFile(), $"[Widgets] Init error: {ex}\n");
            _widgetManager = null;
        }

        try
        {
            _webhookServer = new WebhookServer(_config.HaToken, bindAddress: _config.WebhookBindAddress);
            _webhookServer.SetTrayIcon(_trayIcon);
            _webhookServer.Start();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DeskLinkApp] Failed to start WebhookServer: {ex.Message}");
        }

        // Start sensor loop
        if (_sensors != null)
            Task.Run(() => SensorLoop(_cts.Token), _cts.Token);

        // ── MQTT smart routing ──────────────────────────────────────
        if (_config.MqttEnabled && !string.IsNullOrEmpty(_config.MqttBroker) && _config.MqttPort > 0)
        {
            var configDir = Config.GetConfigDir();
            var password = string.IsNullOrEmpty(_config.MqttPasswordEncrypted) ? _config.MqttPassword : _config.MqttPassword;
            _mqttClient = new MqttClient(_config.MqttBroker, _config.MqttPort,
                string.IsNullOrEmpty(_config.MqttUsername) ? null : _config.MqttUsername,
                string.IsNullOrEmpty(password) ? null : password,
                _config.MqttUseSsl, configDir, GetVersion(),
                onCommandReceived: cmd =>
                {
                    try { CommandHandler.Execute(cmd); }
                    catch (Exception ex) { File.AppendAllText(Program.LogFile(), $"[MQTT Cmd] Error: {ex}\n"); }
                },
                onConnectedCallback: () =>
                {
                    if (_sensors != null)
                    {
                        Task.Run(async () =>
                        {
                            try { await _mqttClient.PublishDiscoveryAsync(_sensors.CollectAll()); }
                            catch (Exception ex) { File.AppendAllText(Program.LogFile(), $"[MQTT] Discovery error: {ex}\n"); }
                            try { await _mqttClient.PublishSensorStatesAsync(_sensors.CollectAll()); }
                            catch (Exception ex) { File.AppendAllText(Program.LogFile(), $"[MQTT] State error: {ex}\n"); }
                        });
                    }
                });
            Task.Run(() => MqttConnectAsync(_cts.Token), _cts.Token);
        }

        // ── Sendspin streaming client (Music Assistant player role) ──
        try
        {
            _sendspinManager = new HaDeskLink.Sendspin.SendspinManager(_config,
                msg => File.AppendAllText(Program.LogFile(), $"[Sendspin] {msg}\n"));
            _sendspinManager.StateChanged += state =>
            {
                try { File.AppendAllText(Program.LogFile(),
                    $"[Sendspin] state: connected={state.Connected} synced={state.TimeSynced} " +
                    $"streaming={state.Streaming} err={state.SyncErrorUs}us\n"); } catch { }
            };
            _sendspinManager.Start();
        }
        catch (Exception ex)
        {
            File.AppendAllText(Program.LogFile(), $"[Sendspin] Failed to start manager: {ex}\n");
        }

        // ── Media player state polling via MQTT ─────────────────────
        try
        {
            _mediaPlayer = new MediaPlayer();
            _mediaTimer = new System.Threading.Timer(async _ =>
            {
                try
                {
                    if (_mqttClient?.IsConnected == true)
                    {
                        var mediaState = _mediaPlayer.GetCurrentMediaState();
                        var attrs = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            title = mediaState.Title,
                            artist = mediaState.Artist,
                            album = mediaState.Album,
                            source = mediaState.Source
                        });
                        await _mqttClient.PublishMediaStateAsync(mediaState.State, attrs);
                    }
                }
                catch { }
            }, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }
        catch { }

        // Check for updates and auto-install
        var channel = _config.UpdateChannel;
        Task.Run(async () =>
        {
            try
            {
                var updateUrl = await _api.CheckForUpdateAsync(includePrerelease: channel == "prerelease");
                if (updateUrl != null)
                {
                    _trayIcon?.ShowBalloonTip(5000, Localization.Get("tray_update_available"),
                        Localization.Get("tray_update_downloading"), ToolTipIcon.Info);
                    await AutoUpdate(updateUrl);
                }
            }
            catch { }
        });

        // Periodic update check every 2 hours
        Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromHours(2), _cts.Token);
                }
                catch { break; }
                try
                {
                    var updateUrl = await _api.CheckForUpdateAsync(includePrerelease: _config.UpdateChannel == "prerelease");
                    if (updateUrl != null)
                    {
                        _trayIcon?.ShowBalloonTip(5000, Localization.Get("tray_update_available"),
                            Localization.Get("tray_update_downloading"), ToolTipIcon.Info);
                        await AutoUpdate(updateUrl);
                    }
                }
                catch { }
            }
        });

        // Start WebSocket connection in background
        Task.Run(async () =>
        {
            try { await wsClient.ConnectAsync(); }
            catch { }
        });

        if (_config.Autostart) Autostart.Enable();
        else Autostart.Disable();

        // Quick Actions - register global hotkey
        try
        {
            _quickActionHandler = new QuickActionHandler(() =>
            {
                var qa = LoadQuickActions();
                QuickActionWindow.ShowActions(qa, _api);
            },
            _config.HotkeyModifiers, _config.HotkeyKey);
            _quickActionHandler.Start();
        }
        catch { }

        // Dashboard hotkey
        try
        {
            if (!string.IsNullOrEmpty(_config.HotkeyDashboardKey) && _config.HotkeyDashboardModifiers != "none")
            {
                _dashboardHotkey = new QuickActionHandler(() => DashboardWindow.Open(_config.HaUrl),
                    _config.HotkeyDashboardModifiers, _config.HotkeyDashboardKey);
                _dashboardHotkey.Start();
            }
        }
        catch { }

        // Settings hotkey
        try
        {
            if (!string.IsNullOrEmpty(_config.HotkeySettingsKey) && _config.HotkeySettingsModifiers != "none")
            {
                _settingsHotkey = new QuickActionHandler(() => SettingsWindow.Open(_config, Reconnect, _api),
                    _config.HotkeySettingsModifiers, _config.HotkeySettingsKey);
                _settingsHotkey.Start();
            }
        }
        catch { }

        Application.Run();

        // Cancel all background tasks first
        _cts.Cancel();
        _sendspinManager?.Dispose();
        _mediaTimer?.Dispose();
        _mediaPlayer?.Dispose();

        // Send pc_status = "off" before shutting down
        try
        {
            var pcOff = new SensorData("pc_status", "PC Status", "off",
                deviceClass: "connectivity", icon: "mdi:desktop-classic")
            {
                SensorKind = SensorType.BinarySensor,
                EntityCategory = null
            };
            _api.UpdateSensorStatesAsync(new List<SensorData> { pcOff }).GetAwaiter().GetResult();
        }
        catch { }

        // MQTT: publish pc_status OFF + disconnect
        try
        {
            if (_mqttClient?.IsConnected == true)
            {
                var pcOff = new SensorData("pc_status", "PC Status", "off",
                    deviceClass: "connectivity", icon: "mdi:desktop-classic")
                {
                    SensorKind = SensorType.BinarySensor
                };
                _mqttClient.PublishSensorStateAsync(pcOff).GetAwaiter().GetResult();
                _mqttClient.DisconnectAsync().GetAwaiter().GetResult();
                _mqttClient.Dispose();
            }
        }
        catch { }
        _quickActionHandler?.Dispose();
        _dashboardHotkey?.Dispose();
        _settingsHotkey?.Dispose();
        _widgetManager?.Dispose();
        wsClient.Dispose();
        _webhookServer?.Dispose();
        _sensors?.Dispose();
        _trayIcon?.Dispose();
        Instance = null;
    }

    private async Task SensorLoop(CancellationToken ct)
    {
        // Widget-Entity-Polling als Fallback/Start (WebSocket-Push hat Vorrang).
        // Fragt alle Widget-Entities per REST ab und füttert den Fan-out.
        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (_widgetManager != null)
                    {
                        var ids = new List<string>();
                        foreach (var w in WidgetConfigStore.Parse(_config))
                        {
                            if (!string.IsNullOrEmpty(w.EntityId)) ids.Add(w.EntityId);
                            ids.AddRange(w.Entities.Select(e => e.EntityId));
                        }
                        foreach (var id in ids.Distinct())
                        {
                            try
                            {
                                var st = await _api.GetEntityStateAsync(id);
                                if (st != null)
                                    _widgetManager.OnEntityStateChanged(id, st.Value.State, st.Value.Unit);
                            }
                            catch { }
                        }
                    }
                }
                catch { }
                // Fallback-Intervall: SensorInterval (min. 10s, gedämpft auf max. 30s-Zyklus)
                var interval = Math.Clamp(_config.SensorInterval, 10, 30);
                try { await Task.Delay(TimeSpan.FromSeconds(interval), ct); }
                catch { break; }
            }
        }, ct);

        try
        {
            var initial = _sensors!.CollectAll();
            foreach (var sensor in initial)
            {
                try { await _api.RegisterSensorAsync(sensor); }
                catch (Exception ex) { File.AppendAllText(Program.LogFile(), $"[SensorLoop] Registration error: {ex}\n"); }
            }
            await _api.UpdateSensorStatesAsync(initial);
            await _api.SendLocationAsync();
            await _api.UpdateRegistrationAsync();
        }
        catch (Exception ex) { File.AppendAllText(Program.LogFile(), $"[SensorLoop] Initial setup error: {ex}\n"); }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var allSensors = _sensors!.CollectAll();
                var changed = new List<SensorData>();
                foreach (var s in allSensors)
                {
                    var key = s.UniqueId;
                    if (!_lastSensorStates.TryGetValue(key, out var lastState) || !Equals(lastState, s.State))
                    {
                        changed.Add(s);
                        _lastSensorStates[key] = s.State;
                    }
                }
                if (changed.Count > 0)
                {
                    // Always send via webhook (keeps mobile_app registration intact)
                    await _api.UpdateSensorStatesAsync(changed);

                    // Smart routing: also publish via MQTT if connected
                    if (_mqttClient?.IsConnected == true)
                    {
                        try { await _mqttClient.PublishSensorStatesAsync(changed); }
                        catch (Exception ex) { File.AppendAllText(Program.LogFile(), $"[MQTT Sensor] Publish error: {ex}\n"); }
                    }
                }
            }
            catch (Exception ex) { File.AppendAllText(Program.LogFile(), $"[SensorLoop] Update error: {ex}\n"); }
            try
            {
                await Task.Delay(_config.SensorInterval * 1000, ct);
            }
            catch (OperationCanceledException) { break; }
        }
    }

    private void SetupTray()
    {
        System.Drawing.Icon? appIcon = null;
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico");
            if (File.Exists(iconPath))
                appIcon = new System.Drawing.Icon(iconPath);
        }
        catch { }

        _trayIcon = new NotifyIcon
        {
            Icon = appIcon ?? System.Drawing.SystemIcons.Information,
            Text = "HA DeskLink",
            Visible = true,
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add($"HA DeskLink v{GetVersion()}", null, (s, e) => { })!.Enabled = false;
        menu.Items.Add("-");

        menu.Items.Add(Localization.Get("tray_dashboard", "Dashboard"), null, (s, e) =>
        {
            if (!string.IsNullOrEmpty(_config.HaUrl))
                DashboardWindow.Open(_config.HaUrl);
        });
        menu.Items.Add(Localization.Get("quickactions_title", "Quick Actions") + $" ({FormatHotkey(_config.HotkeyModifiers, _config.HotkeyKey)})", null, (s, e) =>
        {
            try
            {
                var qa = LoadQuickActions();
                QuickActionWindow.ShowActions(qa, _api);
            }
            catch { }
        });

        menu.Items.Add(Localization.Get("tray_sensors_update"), null, async (s, e) =>
        {
            try
            {
                if (_sensors != null)
                    await _api.UpdateSensorStatesAsync(_sensors.CollectAll());
            }
            catch { }
        });

        menu.Items.Add("🎵 " + Localization.Get("ma_settings", "Music Assistant"), null, (s, e) =>
        {
            try
            {
                var cfg = Config.Load();
                if (string.IsNullOrWhiteSpace(cfg.MaHost))
                {
                    // MA nicht konfiguriert → Settings öffnen (dort ist die MA-Sektion)
                    SettingsWindow.Open(_config, Reconnect, _api);
                    return;
                }
                MusicWindow.Open(_config);
            }
            catch { }
        });

        menu.Items.Add(Localization.Get("tray_check_update"), null, async (s, e) =>
        {
            try
            {
                var channel = _config.UpdateChannel;
                var updateUrl = await _api.CheckForUpdateAsync(includePrerelease: channel == "prerelease");
                if (updateUrl != null)
                {
                    var result = MessageBox.Show(
                        Localization.Get("update_available_msg"),
                        Localization.Get("update_available_title"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result == DialogResult.Yes)
                        await AutoUpdate(updateUrl);
                }
                else
                    MessageBox.Show(Localization.Get("update_uptodate"),
                        Localization.Get("update_uptodate_title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch
            {
                MessageBox.Show(Localization.Get("update_check_failed"),
                    Localization.Get("update_check_failed_title"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        });

        menu.Items.Add(Localization.Get("tray_settings"), null, (s, e) =>
            SettingsWindow.Open(_config, Reconnect, _api));

        menu.Items.Add(Localization.Get("tray_open_log"), null, (s, e) =>
        {
            var log = Program.LogFile();
            if (File.Exists(log))
                Process.Start(new ProcessStartInfo(log) { UseShellExecute = true });
            else
                MessageBox.Show(Localization.Get("no_log"),
                    Localization.Get("no_log_title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        });

        menu.Items.Add(Localization.Get("tray_discord"), null, (s, e) =>
        {
            Process.Start(new ProcessStartInfo("https://discord.com/invite/zHPhQ7EaqH") { UseShellExecute = true });
        });

        menu.Items.Add("-");
        menu.Items.Add(Localization.Get("tray_exit"), null, (s, e) => Application.Exit());

        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (s, e) =>
        {
            if (!string.IsNullOrEmpty(_config.HaUrl))
                DashboardWindow.Open(_config.HaUrl);
        };
    }

    private async void Reconnect()
    {
        try
        {
            await _api.RegisterAsync(_config.HaUrl, _config.HaToken);
            if (_sensors != null)
            {
                var sensors = _sensors.CollectAll();
                foreach (var sensor in sensors)
                {
                    try { await _api.RegisterSensorAsync(sensor); }
                    catch (Exception ex) { File.AppendAllText(Program.LogFile(), $"[Reconnect] Sensor registration error: {ex}\n"); }
                }
            }
        }
        catch (Exception ex) { File.AppendAllText(Program.LogFile(), $"[Reconnect] Error: {ex}\n"); }
    }

    // ── MQTT Smart Routing ────────────────────────────────────────

    /// <summary>
    /// Connect to MQTT, publish discovery on connect, and handle reconnect with state republish.
    /// Runs in a loop that monitors connection state and republishes on reconnect.
    /// </summary>
    private async Task MqttConnectAsync(CancellationToken ct)
    {
        try
        {
            await _mqttClient.ConnectAsync();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            File.AppendAllText(Program.LogFile(), $"[MQTT] Connect error: {ex}\n");
        }
    }

    private async Task AutoUpdate(string downloadUrl)
    {
        try
        {
            // Write pending file to prevent update loops
            var pendingFile = Path.Combine(Config.GetConfigDir(), ".update_pending");
            await File.WriteAllTextAsync(pendingFile, GetVersion());

            var tempDir = Path.Combine(Path.GetTempPath(), "HA_DeskLink_Update");
            Directory.CreateDirectory(tempDir);
            var installerPath = Path.Combine(tempDir, "HA_DeskLink_Setup.exe");

            _trayIcon?.ShowBalloonTip(3000, "Update", Localization.Get("tray_update_downloading_short"), ToolTipIcon.Info);
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "HA-DeskLink");
            var bytes = await client.GetByteArrayAsync(downloadUrl);
            await File.WriteAllBytesAsync(installerPath, bytes);

            if (!File.Exists(installerPath) || new FileInfo(installerPath).Length < 1000000)
            {
                // Download failed or suspiciously small — abort
                try { File.Delete(pendingFile); } catch { }
                MessageBox.Show(Localization.Get("update_download_failed"),
                    Localization.Get("update_check_failed_title"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _trayIcon?.ShowBalloonTip(3000, "Update", Localization.Get("tray_update_installing"), ToolTipIcon.Info);
            var psi = new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS",
                UseShellExecute = true,
                Verb = "runas"
            };
            Process.Start(psi);

            await Task.Delay(2000);
            Application.Exit();
        }
        catch (Exception ex)
        {
            // Clean up pending file on error
            try { File.Delete(Path.Combine(Config.GetConfigDir(), ".update_pending")); } catch { }
            MessageBox.Show(Localization.Get("update_failed", ex.Message),
                Localization.Get("update_failed_title"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string GetVersion()
    {
        var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
        var fallbackVersion = assemblyVersion != null
            ? $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}"
            : "4.4.2";

        try
        {
            var vfile = Path.Combine(AppContext.BaseDirectory, "VERSION");
            if (File.Exists(vfile))
            {
                var fileVer = File.ReadAllText(vfile).Trim();
                if (!string.IsNullOrEmpty(fileVer) && fileVer.Length >= 5)
                    return fileVer;
            }
        }
        catch { }

        return fallbackVersion;
    }

    /// <summary>
    /// Format hotkey modifiers + key into human-readable string like "Ctrl+Shift+H"
    /// </summary>
    private static string FormatHotkey(string modifiers, string key)
    {
        var modStr = modifiers switch
        {
            "ctrl_shift" => "Ctrl+Shift",
            "ctrl_alt" => "Ctrl+Alt",
            "ctrl" => "Ctrl",
            "alt" => "Alt",
            "shift" => "Shift",
            "none" => "",
            _ => "Ctrl+Shift"
        };
        return string.IsNullOrEmpty(modStr) ? key : $"{modStr}+{key}";
    }

    private List<QuickAction> LoadQuickActions()
    {
        var result = new List<QuickAction>();
        try
        {
            var json = _config.QuickActions;
            var arr = System.Text.Json.JsonDocument.Parse(json).RootElement;
            foreach (var item in arr.EnumerateArray())
            {
                var entityId = item.TryGetProperty("entityId", out var eid) ? eid.GetString() ?? "" : "";
                var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? entityId : entityId;
                if (!string.IsNullOrEmpty(entityId))
                    result.Add(new QuickAction(entityId, name));
            }
        }
        catch { }
        return result;
    }

    public async Task UploadScreenshotAsync(string filePath)
    {
        try { await _api.UploadScreenshotAsync(filePath); }
        catch { }
    }

    /// <summary>
    /// Re-register all sensors with Home Assistant.
    /// Call from Settings to fix missing sensors after an update.
    /// </summary>
    public static void ReRegisterSensors()
    {
        var app = Instance;
        if (app != null && app._sensors != null)
        {
            var sensors = app._sensors.CollectAll();
            foreach (var sensor in sensors)
            {
                try { app._api.RegisterSensorAsync(sensor).Wait(); }
                catch { }
            }
            try { app._api.UpdateSensorStatesAsync(sensors).Wait(); }
            catch { }
        }
    }
}