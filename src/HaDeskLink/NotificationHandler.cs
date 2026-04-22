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
using System.Collections.Generic;
using System.Windows.Forms;
using Microsoft.Toolkit.Uwp.Notifications;

namespace HaDeskLink;

/// <summary>
/// Handles notifications and commands from Home Assistant.
/// Supports actionable notifications with buttons via Windows Toast.
/// </summary>
public static class NotificationHandler
{
    private static bool _toastActivatedRegistered = false;

    public static bool TryHandleNotification(string jsonBody, NotifyIcon? trayIcon)
    {
        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(jsonBody);
            var root = doc.RootElement;

            string title = "HA DeskLink";
            string message = "";
            string? command = null;
            List<NotificationAction>? actions = null;
            string? commandOnAction = null;

            if (root.TryGetProperty("title", out var t1))
                title = t1.GetString() ?? title;
            if (root.TryGetProperty("message", out var m1))
                message = m1.GetString() ?? "";
            if (root.TryGetProperty("command", out var c1))
                command = c1.GetString();

            if (root.TryGetProperty("data", out var data))
            {
                if (data.TryGetProperty("title", out var t2))
                    title = t2.GetString() ?? title;
                if (data.TryGetProperty("message", out var m2))
                    message = m2.GetString() ?? message;
                if (data.TryGetProperty("command", out var c2))
                    command = c2.GetString();
                if (data.TryGetProperty("command_on_action", out var coa))
                    commandOnAction = coa.GetString();
                if (data.TryGetProperty("actions", out var actionsArr))
                {
                    actions = new List<NotificationAction>();
                    foreach (var a in actionsArr.EnumerateArray())
                    {
                        var act = a.GetProperty("action").GetString() ?? "";
                        var actTitle = a.TryGetProperty("title", out var at) ? at.GetString() ?? act : act;
                        var actCommand = a.TryGetProperty("command", out var ac) ? ac.GetString() : null;
                        actions.Add(new NotificationAction(act, actTitle, actCommand));
                    }
                }
            }

            if (!string.IsNullOrEmpty(command))
            {
                try { CommandHandler.Execute(command!); }
                catch { }
            }

            if (!string.IsNullOrEmpty(message))
            {
                if (actions != null && actions.Count > 0)
                    ShowActionableNotification(title, message, actions, commandOnAction, trayIcon);
                else
                    ShowNotification(title, message, trayIcon);
                return true;
            }

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

    public static void ShowActionableNotification(string title, string message,
        List<NotificationAction> actions, string? commandOnAction, NotifyIcon? trayIcon)
    {
        try
        {
            var builder = new ToastContentBuilder()
                .AddText(title)
                .AddText(message);

            foreach (var action in actions)
            {
                builder.AddButton(new ToastButton()
                    .SetContent(action.Title)
                    .AddArgument("action", action.ActionKey)
                    .AddArgument("command", action.Command ?? commandOnAction ?? ""));
            }

            // Register activation handler once
            if (!_toastActivatedRegistered)
            {
                ToastNotificationManagerCompat.OnActivated += args =>
                {
                    if (args.Arguments.TryGetValue("command", out var cmd) && !string.IsNullOrEmpty(cmd))
                    {
                        try { CommandHandler.Execute(cmd); }
                        catch { }
                    }
                };
                _toastActivatedRegistered = true;
            }

            builder.Show();
            return;
        }
        catch
        {
            // Fallback to WinForms dialog
        }

        // Fallback: show balloon tip + dialog with action buttons
        if (trayIcon != null)
        {
            trayIcon.BalloonTipTitle = title;
            trayIcon.BalloonTipText = message;
            trayIcon.BalloonTipIcon = ToolTipIcon.Info;
            trayIcon.ShowBalloonTip(5000);
        }

        using var form = new Form
        {
            Text = title,
            Size = new System.Drawing.Size(400, 200),
            StartPosition = FormStartPosition.CenterScreen,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            TopMost = true
        };

        var lbl = new Label { Text = message, Dock = DockStyle.Top, Height = 60, TextAlign = System.Drawing.ContentAlignment.MiddleLeft };
        form.Controls.Add(lbl);

        var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, FlowDirection = FlowDirection.LeftToRight };
        foreach (var action in actions)
        {
            var btn = new Button { Text = action.Title, Width = 120, Height = 35, Tag = action };
            btn.Click += (s, e) =>
            {
                var a = (NotificationAction)((Button)s!).Tag!;
                if (!string.IsNullOrEmpty(a.Command))
                {
                    try { CommandHandler.Execute(a.Command!); }
                    catch { }
                }
                else if (!string.IsNullOrEmpty(commandOnAction))
                {
                    try { CommandHandler.Execute(commandOnAction); }
                    catch { }
                }
                form.Close();
            };
            btnPanel.Controls.Add(btn);
        }
        var dismissBtn = new Button { Text = "✕", Width = 50, Height = 35 };
        dismissBtn.Click += (s, e) => form.Close();
        btnPanel.Controls.Add(dismissBtn);

        form.Controls.Add(btnPanel);
        form.Show();
    }

    public static void ShowWebSocketNotification(string title, string message,
        List<NotificationAction>? actions, string? commandOnAction, NotifyIcon? trayIcon)
    {
        if (actions != null && actions.Count > 0)
            ShowActionableNotification(title, message, actions, commandOnAction, trayIcon);
        else
            ShowNotification(title, message, trayIcon);
    }
}

public class NotificationAction
{
    public string ActionKey { get; }
    public string Title { get; }
    public string? Command { get; }

    public NotificationAction(string actionKey, string title, string? command = null)
    {
        ActionKey = actionKey;
        Title = title;
        Command = command;
    }
}