
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
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace HaDeskLink;

/// <summary>
/// Embedded HA Dashboard using WebView2.
/// Uses its own data directory to avoid Edge access errors.
/// Automatically offers to install WebView2 Runtime if missing.
/// </summary>
public class DashboardWindow : Form
{
    private WebView2? _webView;
    private readonly string _haUrl;
    private static bool _installPrompted = false;

    public DashboardWindow(string haUrl)
    {
        _haUrl = haUrl;
        Text = "HA DeskLink - Dashboard";
        Size = new System.Drawing.Size(1300, 850);
        MinimumSize = new System.Drawing.Size(800, 600);
        StartPosition = FormStartPosition.CenterScreen;
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        _webView = new WebView2 { Dock = DockStyle.Fill };
        Controls.Add(_webView);

        try
        {
            // Use dedicated data directory (fixes Edge read/write access error)
            var userDataDir = Path.Combine(Config.GetConfigDir(), "WebView2Data");
            Directory.CreateDirectory(userDataDir);

            var env = await CoreWebView2Environment.CreateAsync(null, userDataDir);
            await _webView.EnsureCoreWebView2Async(env);

            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _webView.CoreWebView2.Navigate(_haUrl);
        }
        catch (Exception)
        {
            if (!_installPrompted)
            {
                _installPrompted = true;
                Close();

                var result = MessageBox.Show(
                    "WebView2 Runtime wird f\u00fcr das eingebettete Dashboard ben\u00f6tigt.\n\n" +
                    "Jetzt herunterladen und installieren?\n(Nach Installation App neu starten)",
                    "WebView2 fehlt",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    try
                    {
                        var tmpPath = Path.Combine(Path.GetTempPath(), "MicrosoftEdgeWebview2Setup.exe");
                        using var http = new System.Net.Http.HttpClient();
                        var bytes = await http.GetByteArrayAsync("https://go.microsoft.com/fwlink/p/?LinkId=2124703");
                        File.WriteAllBytes(tmpPath, bytes);
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(tmpPath) { UseShellExecute = true });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Download fehlgeschlagen: {ex.Message}\n\n" +
                            "Bitte manuell installieren:\nhttps://developer.microsoft.com/en-us/microsoft-edge/webview2/",
                            "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_haUrl) { UseShellExecute = true });
                }
            }
            else
            {
                Close();
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_haUrl) { UseShellExecute = true });
            }
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _webView?.Dispose();
        base.OnFormClosing(e);
    }

    private static DashboardWindow? _instance;

    public static void Open(string haUrl)
    {
        if (_instance != null && !_instance.IsDisposed)
        {
            _instance.Activate();
            return;
        }
        _instance = new DashboardWindow(haUrl);
        _instance.Show();
    }
}