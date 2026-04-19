#nullable enable
using System;
using System.Diagnostics;
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
            else return;
        }

        new DeskLinkApp(config).Run();
    }
}

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
        _api = new HaApiClient(Config.GetConfigDir(), config.VerifySsl);
        _sensors = new SensorManager();
        _webhookServer = new WebhookServer(config.HaToken);
    }

    public void Run()
    {
        if (!_api.LoadRegistration())
        {
            MessageBox.Show("Keine gespeicherte Verbindung. Bitte App neu starten.",
                "HA DeskLink", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Start command webhook server
        try { _webhookServer.Start(); }
        catch { }

        // Start sensor loop
        Task.Run(() => SensorLoop(_cts.Token));

        // Check for updates on startup
        Task.Run(async () =>
        {
            var updateUrl = await _api.CheckForUpdateAsync();
            if (updateUrl != null)
            {
                _trayIcon?.ShowBalloonTip(5000, "Update verf\u00fcgbar",
                    "Neue Version von HA DeskLink verf\u00fcgbar! Klicke auf 'Nach Update suchen'.",
                    ToolTipIcon.Info);
            }
        });

        SetupTray();

        if (_config.Autostart) Autostart.Enable();
        else Autostart.Disable();

        Application.Run();

        _cts.Cancel();
        _webhookServer.Dispose();
        _sensors.Dispose();
        _trayIcon?.Dispose();
    }

    private async void SensorLoop(CancellationToken ct)
    {
        // Register sensors + buttons
        try
        {
            var initial = _sensors.CollectAll();
            foreach (var sensor in initial)
            {
                try { await _api.RegisterSensorAsync(sensor); }
                catch { }
            }
            await _api.RegisterCommandButtonsAsync();
            await _api.UpdateSensorStatesAsync(initial);
            await _api.SendLocationAsync();
        }
        catch { }

        // Update loop
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _api.UpdateSensorStatesAsync(_sensors.CollectAll());
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
                DashboardWindow.Open(_config.HaUrl);
        });

        menu.Items.Add("Sensoren aktualisieren", null, async (s, e) =>
        {
            try { await _api.UpdateSensorStatesAsync(_sensors.CollectAll()); }
            catch { }
        });

        menu.Items.Add("Nach Update suchen", null, async (s, e) =>
        {
            try
            {
                var updateUrl = await _api.CheckForUpdateAsync();
                if (updateUrl != null)
                {
                    var result = MessageBox.Show(
                        "Neue Version verf\u00fcgbar! Jetzt herunterladen?",
                        "Update", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result == DialogResult.Yes)
                    {
                        Process.Start(new ProcessStartInfo(updateUrl) { UseShellExecute = true });
                    }
                }
                else
                {
                    MessageBox.Show("HA DeskLink ist auf dem neuesten Stand.", "Update",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch
            {
                MessageBox.Show("Update-Pr\u00fcfung fehlgeschlagen.", "Fehler",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        });

        menu.Items.Add("Einstellungen", null, (s, e) =>
            SettingsWindow.Open(_config, Reconnect));

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
            var sensors = _sensors.CollectAll();
            foreach (var sensor in sensors)
            {
                try { await _api.RegisterSensorAsync(sensor); }
                catch { }
            }
            await _api.RegisterCommandButtonsAsync();
        }
        catch { }
    }
}