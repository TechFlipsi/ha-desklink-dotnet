using System;
using System.Windows.Forms;

namespace HaDeskLink;

/// <summary>
/// Main application entry point.
/// System tray app with sensor reporting and command handling.
/// </summary>
static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var config = Config.Load();
        var configDir = Config.GetConfigDir();

        // Check for setup mode
        if (args.Length > 0 && args[0] == "--setup" ||
            !File.Exists(Path.Combine(configDir, "registration.json")))
        {
            // TODO: Show setup wizard
            Console.WriteLine("Setup mode - TODO: implement setup wizard");
            return;
        }

        // Create API client and connect
        var api = new HaApiClient(configDir, config.VerifySsl);
        if (!api.LoadRegistration())
        {
            Console.WriteLine("No saved registration. Run with --setup first.");
            return;
        }

        // Create tray icon
        using var tray = new NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Information,
            Text = "HA DeskLink",
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip()
        };

        tray.ContextMenuStrip.Items.Add("Dashboard", null, (s, e) =>
        {
            // TODO: Open WebView2 dashboard
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(config.HaUrl)
            { UseShellExecute = true });
        });
        tray.ContextMenuStrip.Items.Add("-");
        tray.ContextMenuStrip.Items.Add("Quit", null, (s, e) => Application.Exit());

        // Start sensor loop
        var cts = new System.Threading.CancellationTokenSource();
        var sensorTask = System.Threading.Tasks.Task.Run(async () =>
        {
            // Register sensors first
            var sensors = SensorManager.CollectAll();
            foreach (var sensor in sensors)
            {
                try { await api.RegisterSensorAsync(sensor); }
                catch { /* already registered */ }
            }

            // Send location
            try { await api.SendLocationAsync(); } catch { }

            // Loop
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    var current = SensorManager.CollectAll();
                    await api.UpdateSensorStatesAsync(current);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Sensor update failed: {ex.Message}");
                }
                await System.Threading.Tasks.Task.Delay(config.SensorInterval * 1000, cts.Token);
            }
        }, cts.Token);

        Application.Run();
        cts.Cancel();
    }
}