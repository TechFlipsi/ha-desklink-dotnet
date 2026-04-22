
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
/// First-time setup wizard for connecting to Home Assistant.
/// </summary>
public class SetupWizard : Form
{
    private TextBox _urlBox = null!;
    private TextBox _tokenBox = null!;
    private CheckBox _sslCheck = null!;

    public string HaUrl => _urlBox.Text.Trim();
    public string HaToken => _tokenBox.Text.Trim();
    public bool VerifySsl => _sslCheck.Checked;

    public SetupWizard()
    {
        Text = "HA DeskLink - Setup";
        Size = new System.Drawing.Size(520, 350);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        InitializeComponents();
    }

    private void InitializeComponents()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 6,
            Padding = new Padding(20),
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var title = new Label
        {
            Text = "HA DeskLink Setup",
            Font = new System.Drawing.Font("", 14, System.Drawing.FontStyle.Bold),
            AutoSize = true,
        };
        panel.Controls.Add(title, 0, 0);
        panel.SetColumnSpan(title, 2);

        var subtitle = new Label { Text = "Verbinde deinen PC mit Home Assistant", AutoSize = true };
        panel.Controls.Add(subtitle, 0, 1);
        panel.SetColumnSpan(subtitle, 2);

        panel.Controls.Add(new Label { Text = "HA URL:", AutoSize = true }, 0, 2);
        _urlBox = new TextBox { Text = "https://homeassistant.local:8123", Dock = DockStyle.Fill };
        panel.Controls.Add(_urlBox, 1, 2);

        panel.Controls.Add(new Label { Text = "Long-Lived Token:", AutoSize = true }, 0, 3);
        _tokenBox = new TextBox { UseSystemPasswordChar = true, Dock = DockStyle.Fill };
        panel.Controls.Add(_tokenBox, 1, 3);

        _sslCheck = new CheckBox { Text = "SSL-Zertifikat pr\u00fcfen", AutoSize = true };
        panel.Controls.Add(_sslCheck, 0, 4);
        panel.SetColumnSpan(_sslCheck, 2);

        var connectBtn = new Button
        {
            Text = "Verbinden",
            Size = new System.Drawing.Size(150, 40),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        connectBtn.Click += OnConnect;
        panel.Controls.Add(connectBtn, 1, 5);

        var hint = new Label
        {
            Text = "Token: HA \u2192 Profil \u2192 Sicherheit \u2192 Long-Lived Access Tokens",
            Font = new System.Drawing.Font("", 8),
            ForeColor = System.Drawing.Color.Gray,
            AutoSize = true,
        };
        panel.Controls.Add(hint, 0, 5);

        Controls.Add(panel);
    }

    private async void OnConnect(object? sender, EventArgs e)
    {
        var btn = (Button)sender!;
        btn.Enabled = false;
        btn.Text = "Verbinde...";

        try
        {
            var configDir = Config.GetConfigDir();
            var api = new HaApiClient(configDir, _sslCheck.Checked);
            await api.RegisterAsync(_urlBox.Text.Trim(), _tokenBox.Text.Trim());
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Verbindung fehlgeschlagen:\n{ex.Message}", "Fehler",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            btn.Enabled = true;
            btn.Text = "Verbinden";
        }
    }
}