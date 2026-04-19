using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HaDeskLink;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var config = Config.Load();
        var configDir = Config.GetConfigDir();

        // Setup mode if no registration exists
        if (!File.Exists(Path.Combine(configDir, "registration.json")))
        {
            using var wizard = new SetupWizard();
            if (wizard.ShowDialog() == DialogResult.OK)
            {
                config.HaUrl = wizard.HaUrl;
                config.HaToken = wizard.HaToken;
                config.VerifySsl = wizard.VerifySsl;
                config.Save();
            }
            else
            {
                return; // User cancelled setup
            }
        }

        // Run the tray app
        var app = new DeskLinkApp(config);
        app.Run();
    }
}

/// <summary>
/// Main application: System tray, sensor loop, command server.
/// </summary>
public class DeskLinkApp
{
    private readonly Config _config;
    private readonly HaApiClient _api;
    private readonly SensorManager _sensors;
    private readonly WebhookServer _webhookServer;
    private readonly CancellationTokenSource _cts = new();
    private NotifyIcon? _trayIcon;

    public DeskLinkApp(Config config)
    {
        _config = config;
        var configDir = Config.GetConfigDir();
        _api = new HaApiClient(configDir, config.VerifySsl);
        _sensors = new SensorManager();
        _webhookServer = new WebhookServer(config.HaToken);
    }

    public void Run()
    {
        // Connect to HA
        if (!_api.LoadRegistration())
        {
            MessageBox.Show("Keine gespeicherte Verbindung. Bitte App neu starten.",
                "HA DeskLink", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Start webhook server for commands
        try { _webhookServer.Start(); }
        catch { /* commands from HA unavailable */ }

        // Start sensor loop
        Task.Run(() => SensorLoop(_cts.Token));

        // Setup tray icon
        SetupTray();

        // Apply autostart
        if (_config.Autostart) Autostart.Enable();
        else Autostart.Disable();

        // Run message loop
        Application.Run();

        // Cleanup
        _cts.Cancel();
        _webhookServer.Dispose();
        _sensors.Dispose();
        _trayIcon?.Dispose();
    }

    private async void SensorLoop(CancellationToken ct)
    {
        // Register sensors with real values
        try
        {
            var initial = _sensors.CollectAll();
            foreach (var sensor in initial)
            {
                try { await _api.RegisterSensorAsync(sensor); }
                catch { /* already registered */ }
            }
            await _api.UpdateSensorStatesAsync(initial);
            await _api.SendLocationAsync();
        }
        catch { }

        // Update loop
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var sensors = _sensors.CollectAll();
                await _api.UpdateSensorStatesAsync(sensors);
            }
            catch { }

            await Task.Delay(_config.SensorInterval * 1000, ct);
        }
    }

    private void SetupTray()
    {
        _trayIcon = new NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Information,
            Text = "HA DeskLink",
            Visible = true,
        };

        var menu = new ContextMenuStrip();

        menu.Items.Add("Verbunden", null, (s, e) => { })!.Enabled = false;
        menu.Items.Add("-");

        menu.Items.Add("Dashboard", null, (s, e) =>
        {
            if (!string.IsNullOrEmpty(_config.HaUrl))
                DashboardWindow.Open(_config.HaUrl, _config.HaToken);
        });

        menu.Items.Add("Sensoren aktualisieren", null, async (s, e) =>
        {
            try
            {
                var sensors = _sensors.CollectAll();
                await _api.UpdateSensorStatesAsync(sensors);
            }
            catch { }
        });

        menu.Items.Add("Einstellungen", null, (s, e) =>
        {
            SettingsWindow.Open(_config, Reconnect);
        });

        menu.Items.Add("-");

        var autostartItem = menu.Items.Add("Autostart", null, (s, e) =>
        {
            if (Autostart.IsEnabled()) Autostart.Disable();
            else Autostart.Enable();
        }) as ToolStripMenuItem;
        if (autostartItem != null) autostartItem.Checked = Autostart.IsEnabled();

        menu.Items.Add("-");
        menu.Items.Add("Beenden", null, (s, e) => Application.Exit());

        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (s, e) =>
        {
            if (!string.IsNullOrEmpty(_config.HaUrl))
                DashboardWindow.Open(_config.HaUrl, _config.HaToken);
        };
    }

    private async void Reconnect()
    {
        try
        {
            await _api.RegisterAsync(_config.HaUrl, _config.HaToken);
            // Re-register sensors
            var sensors = _sensors.CollectAll();
            foreach (var sensor in sensors)
            {
                try { await _api.RegisterSensorAsync(sensor); }
                catch { }
            }
        }
        catch { }
    }
}