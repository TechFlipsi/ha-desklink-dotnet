#nullable enable
using System;
using System.Windows.Forms;

namespace HaDeskLink;

/// <summary>
/// Shows Windows toast notifications from Home Assistant.
/// HA sends notifications via mobile_app webhook: { type: "handle_webhook", data: { title, message } }
/// </summary>
public static class NotificationHandler
{
    /// <summary>
    /// Show a Windows notification balloon from HA.
    /// </summary>
    public static void ShowNotification(string title, string message, NotifyIcon? trayIcon)
    {
        if (trayIcon != null)
        {
            trayIcon.BalloonTipTitle = title;
            trayIcon.BalloonTipText = message;
            trayIcon.BalloonTipIcon = ToolTipIcon.Info;
            trayIcon.ShowBalloonTip(5000);
        }
        else
        {
            // Fallback: MessageBox
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    /// <summary>
    /// Parse a notification webhook from HA and show it.
    /// </summary>
    public static bool TryHandleNotification(string jsonBody, NotifyIcon? trayIcon)
    {
        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(jsonBody);
            var root = doc.RootElement;

            // Check if this is a notification webhook
            var type = root.TryGetProperty("type", out var t) ? t.GetString() : "";
            if (type != "handle_webhook") return false;

            // Extract title and message
            string title = "HA DeskLink";
            string message = "";

            if (root.TryGetProperty("data", out var data))
            {
                if (data.TryGetProperty("title", out var tit))
                    title = tit.GetString() ?? title;
                if (data.TryGetProperty("message", out var msg))
                    message = msg.GetString() ?? "";
            }

            if (!string.IsNullOrEmpty(message))
            {
                ShowNotification(title, message, trayIcon);
                return true;
            }
        }
        catch { }
        return false;
    }
}