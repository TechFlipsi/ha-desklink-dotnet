#nullable enable
using System;
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
            await _webView.EnsureCoreWebView2Async(null);
            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _webView.CoreWebView2.Navigate(_haUrl);
        }
        catch (Exception)
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