
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

        // Initialize toast notification support
        ToastNotificationManagerCompat.Initialize();

        // Global exception handler - log errors instead of silent crash
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            var msg = e.ExceptionObject.ToString();
            File.WriteAllText(LogFile(), $"[CRASH] {DateTime.Now}\n{msg}");
            MessageBox.Show($"Schwerer Fehler:\n{msg}", "HA DeskLink", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };
        Application.ThreadException += (s, e) =>
        {
            File.WriteAllText(LogFile(), $"[UI-ERROR] {DateTime.Now}\n{e.Exception}");
            MessageBox.Show($"Fehler:\n{e.Exception.Message}", "HA DeskLink", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };

        try
        {
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
        catch (Exception ex)
        {
            File.WriteAllText(LogFile(), $"[STARTUP] {DateTime.Now}\n{ex}");
            MessageBox.Show($"Startfehler:\n{ex.Message}\n\nLog: {LogFile()}", "HA DeskLink",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    public static string LogFile()
    {
        var dir = Config.GetConfigDir();
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "error.log");
    }
}