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
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HaDeskLink;

public class DeskLinkApp
{
    private readonly Config _config;
    private readonly HaApiClient _api;
    private SensorManager? _sensors;
    private WebhookServer? _webhookServer;
    private readonly Dictionary<string, object> _lastSensorStates = new();
    private readonly CancellationTokenSource _cts = new();
    private NotifyIcon? _trayIcon;

    public DeskLinkApp(Config config)
    {
        _config = config;
        _api = new HaApiClient(Config.GetConfigDir(), config.VerifySsl);
    }

    public void Run()
    {
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
            File.WriteAllText(Program.LogFile(), $"[SensorManager] LibreHardwareMonitor failed: {ex}");
            _sensors = null;
        }

        // Setup tray FIRST (needed for notifications)
        SetupTray();

        // Start WebSocket connection for push notifications
        var webhookId = _api.GetWebhookId();
        var wsClient = new HaWebSocketClient(_config.HaUrl, _config.HaToken, webhookId, _trayIcon,
            cmd => CommandHandler.Execute(cmd));

        try
        {
            _webhookServer = new WebhookServer(_config.HaToken);
            _webhookServer.SetTrayIcon(_trayIcon);
            _webhookServer.Start();
        }
        catch { }

        // Start sensor loop
        if (_sensors != null)
            Task.Run(() => SensorLoop(_cts.Token));

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

        Application.Run();

        _cts.Cancel();
        wsClient.Dispose();
        _webhookServer?.Dispose();
        _sensors?.Dispose();
        _trayIcon?.Dispose();
    }

    private async void SensorLoop(CancellationToken ct)
    {
        try
        {
            var initial = _sensors!.CollectAll();
            foreach (var sensor in initial)
            {
                try { await _api.RegisterSensorAsync(sensor); }
                catch { }
            }
            await _api.UpdateSensorStatesAsync(initial);
            await _api.SendLocationAsync();
            await _api.UpdateRegistrationAsync();
        }
        catch { }

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
                    await _api.UpdateSensorStatesAsync(changed);
            }
            catch { }
            await Task.Delay(_config.SensorInterval * 1000, ct);
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

        menu.Items.Add(Localization.Get("tray_sensors_update"), null, async (s, e) =>
        {
            try
            {
                if (_sensors != null)
                    await _api.UpdateSensorStatesAsync(_sensors.CollectAll());
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
            Process.Start(new ProcessStartInfo("https://discord.gg/HnCZY54U7") { UseShellExecute = true });
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
                    catch { }
                }
            }
        }
        catch { }
    }

    private async Task AutoUpdate(string downloadUrl)
    {
        try
        {
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
            MessageBox.Show(Localization.Get("update_failed", ex.Message),
                Localization.Get("update_failed_title"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string GetVersion()
    {
        try
        {
            var vfile = Path.Combine(AppContext.BaseDirectory, "VERSION");
            if (File.Exists(vfile)) return File.ReadAllText(vfile).Trim();
        }
        catch { }
        return "2.2.0";
    }
}