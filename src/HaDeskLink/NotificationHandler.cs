
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
using System.Windows.Forms;

namespace HaDeskLink;

/// <summary>
/// Handles notifications and commands from Home Assistant.
/// HA sends via mobile_app webhook:
/// - Notifications: { type: "handle_webhook", data: { title, message } }
/// - Commands: { type: "handle_webhook", data: { title, message, command: "shutdown" } }
/// </summary>
public static class NotificationHandler
{
    /// <summary>
    /// Parse a notification/command webhook from HA and handle it.
    /// Returns true if this was a valid HA notification/command.
    /// </summary>
    public static bool TryHandleNotification(string jsonBody, NotifyIcon? trayIcon)
    {
        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(jsonBody);
            var root = doc.RootElement;

            // HA mobile_app sends webhook data in different formats
            string title = "HA DeskLink";
            string message = "";
            string? command = null;

            // Format 1: direct data at root level (some HA versions)
            if (root.TryGetProperty("title", out var t1))
                title = t1.GetString() ?? title;
            if (root.TryGetProperty("message", out var m1))
                message = m1.GetString() ?? "";
            if (root.TryGetProperty("command", out var c1))
                command = c1.GetString();

            // Format 2: data nested under "data"
            if (root.TryGetProperty("data", out var data))
            {
                if (data.TryGetProperty("title", out var t2))
                    title = t2.GetString() ?? title;
                if (data.TryGetProperty("message", out var m2))
                    message = m2.GetString() ?? message;
                if (data.TryGetProperty("command", out var c2))
                    command = c2.GetString();
            }

            // If there's a command, execute it
            if (!string.IsNullOrEmpty(command))
            {
                try { CommandHandler.Execute(command); }
                catch { }
            }

            // Show notification if there's a message
            if (!string.IsNullOrEmpty(message))
            {
                ShowNotification(title, message, trayIcon);
                return true;
            }

            // If there's a command but no message, still return true
            if (!string.IsNullOrEmpty(command)) return true;
        }
        catch { }
        return false;
    }

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
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}