using System;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace HaDeskLink;

/// <summary>
/// Embedded HA Dashboard using WebView2 (EdgeChromium).
/// Works on Windows 10/11 without installing Edge – WebView2 Runtime is built-in.
/// </summary>
public class DashboardWindow : Form
{
    private WebView2? _webView;
    private readonly string _haUrl;
    private readonly string _token;

    public DashboardWindow(string haUrl, string token = "")
    {
        _haUrl = haUrl;
        _token = token;

        Text = "HA DeskLink - Dashboard";
        Size = new System.Drawing.Size(1300, 850);
        MinimumSize = new System.Drawing.Size(800, 600);
        StartPosition = FormStartPosition.CenterScreen;
        Icon = System.Drawing.SystemIcons.Information;
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        _webView = new WebView2
        {
            Dock = DockStyle.Fill,
        };
        Controls.Add(_webView);

        try
        {
            await _webView.EnsureCoreWebView2Async(null);
            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;

            // Navigate to HA
            _webView.CoreWebView2.Navigate(_haUrl);
        }
        catch (WebView2RuntimeException ex)
        {
            // WebView2 not available – fallback to default browser
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_haUrl)
            { UseShellExecute = true });
            Close();
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _webView?.Dispose();
        base.OnFormClosing(e);
    }

    /// <summary>
    /// Open dashboard – reuses existing window or creates new one.
    /// </summary>
    private static DashboardWindow? _instance;

    public static void Open(string haUrl, string token = "")
    {
        if (_instance != null && !_instance.IsDisposed)
        {
            _instance.Activate();
            return;
        }

        _instance = new DashboardWindow(haUrl, token);
        _instance.Show();
    }
}