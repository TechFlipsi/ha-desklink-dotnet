#nullable enable
using System;
using System.Diagnostics;
using System.IO;
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
    private readonly CancellationTokenSource _cts = new();
    private NotifyIcon? _trayIcon;

    public DeskLinkApp(Config config)
    {
        _config = config;
        _api = new HaApiClient(Config.GetConfigDir(), config.VerifySsl);
    }

    public void Run()
    {
        if (!_api.LoadRegistration())
        {
            MessageBox.Show("Keine gespeicherte Verbindung. Bitte App neu starten.",
                "HA DeskLink", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

        // Start webhook server for commands + notifications
        try
        {
            _webhookServer = new WebhookServer(_config.HaToken);
            _webhookServer.SetTrayIcon(_trayIcon);
            _webhookServer.Start();

            // Register push URL so HA can send notifications to this PC
            var localIp = GetLocalIpAddress();
            if (!string.IsNullOrEmpty(localIp))
            {
                var pushUrl = $"http://{localIp}:{_webhookServer.Port}/webhook/";
                _ = _api.RegisterPushUrlAsync(pushUrl);
            }
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
                    _trayIcon?.ShowBalloonTip(5000, "Update verf\u00fcgbar",
                        "Neue Version wird heruntergeladen und installiert...", ToolTipIcon.Info);
                    await AutoUpdate(updateUrl);
                }
            }
            catch { }
        });

        if (_config.Autostart) Autostart.Enable();
        else Autostart.Disable();

        Application.Run();

        _cts.Cancel();
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
        }
        catch { }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _api.UpdateSensorStatesAsync(_sensors!.CollectAll());
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

        menu.Items.Add("Dashboard", null, (s, e) =>
        {
            if (!string.IsNullOrEmpty(_config.HaUrl))
                DashboardWindow.Open(_config.HaUrl);
        });

        menu.Items.Add("Sensoren aktualisieren", null, async (s, e) =>
        {
            try
            {
                if (_sensors != null)
                    await _api.UpdateSensorStatesAsync(_sensors.CollectAll());
            }
            catch { }
        });

        menu.Items.Add("Nach Update suchen", null, async (s, e) =>
        {
            try
            {
                var channel = _config.UpdateChannel;
                var updateUrl = await _api.CheckForUpdateAsync(includePrerelease: channel == "prerelease");
                if (updateUrl != null)
                {
                    var result = MessageBox.Show(
                        "Eine neue Version ist verf\u00fcgbar!\n\nJetzt herunterladen und installieren?\n(Die App wird danach neu gestartet)",
                        "Update verf\u00fcgbar", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result == DialogResult.Yes)
                        await AutoUpdate(updateUrl);
                }
                else
                    MessageBox.Show("HA DeskLink ist auf dem neuesten Stand.", "Update",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch
            {
                MessageBox.Show("Update-Pr\u00fcfung fehlgeschlagen.", "Fehler",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        });

        menu.Items.Add("Einstellungen", null, (s, e) =>
            SettingsWindow.Open(_config, Reconnect));

        menu.Items.Add("Log \u00f6ffnen", null, (s, e) =>
        {
            var log = Program.LogFile();
            if (File.Exists(log))
                Process.Start(new ProcessStartInfo(log) { UseShellExecute = true });
            else
                MessageBox.Show("Kein Fehler-Log vorhanden.", "Log", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });

        menu.Items.Add("-");
        menu.Items.Add("Beenden", null, (s, e) => Application.Exit());

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

            // Download installer
            _trayIcon?.ShowBalloonTip(3000, "Update", "Lade Update herunter...", ToolTipIcon.Info);
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "HA-DeskLink");
            var bytes = await client.GetByteArrayAsync(downloadUrl);
            await File.WriteAllBytesAsync(installerPath, bytes);

            // Verify file was downloaded
            if (!File.Exists(installerPath) || new FileInfo(installerPath).Length < 1000000)
            {
                MessageBox.Show("Download fehlgeschlagen \u2013 Datei zu klein.", "Fehler",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Launch installer and exit
            _trayIcon?.ShowBalloonTip(3000, "Update", "Installiere Update... App wird geschlossen.", ToolTipIcon.Info);
            var psi = new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS",
                UseShellExecute = true,
                Verb = "runas" // Run as admin
            };
            Process.Start(psi);

            // Give it a moment then exit
            await Task.Delay(2000);
            Application.Exit();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Update fehlgeschlagen: {ex.Message}\n\nBitte manuell von GitHub herunterladen.",
                "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string GetLocalIpAddress()
    {
        try
        {
            foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;
                foreach (var ip in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        return ip.Address.ToString();
                }
            }
        }
        catch { }
        return "";
    }

    private static string GetVersion()
    {
        try
        {
            var vfile = Path.Combine(AppContext.BaseDirectory, "VERSION");
            if (File.Exists(vfile)) return File.ReadAllText(vfile).Trim();
        }
        catch { }
        return "2.0.3";
    }
}