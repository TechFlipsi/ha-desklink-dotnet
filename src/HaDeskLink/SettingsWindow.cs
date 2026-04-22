
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
using System.Text.Json;
using System.Windows.Forms;

namespace HaDeskLink;

/// <summary>
/// Settings window for configuring HA connection and app options.
/// </summary>
public class SettingsWindow : Form
{
    private readonly Config _config;
    private readonly Action _onReconnect;
    private readonly HaApiClient? _api;
    private TextBox _urlBox = null!;
    private TextBox _tokenBox = null!;
    private CheckBox _sslCheck = null!;
    private CheckBox _autostartCheck = null!;
    private NumericUpDown _intervalBox = null!;
    private ComboBox _updateChannelBox = null!;
    private Label _statusLabel = null!;

    public SettingsWindow(Config config, Action onReconnect, HaApiClient? api = null)
    {
        _config = config;
        _onReconnect = onReconnect;
        _api = api;
        Text = "HA DeskLink - Einstellungen";
        Size = new System.Drawing.Size(520, 550);
        MinimumSize = new System.Drawing.Size(480, 420);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        InitializeComponents();
        LoadSettings();
    }

    private void InitializeComponents()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 8,
            Padding = new Padding(15),
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        panel.Controls.Add(new Label { Text = "HA URL:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 0);
        _urlBox = new TextBox { Dock = DockStyle.Fill, Text = "https://homeassistant.local:8123" };
        panel.Controls.Add(_urlBox, 1, 0);

        panel.Controls.Add(new Label { Text = "Long-Lived Token:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 1);
        _tokenBox = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
        panel.Controls.Add(_tokenBox, 1, 1);

        _sslCheck = new CheckBox { Text = "SSL-Zertifikat pr\u00fcfen", AutoSize = true };
        panel.Controls.Add(_sslCheck, 0, 2);
        panel.SetColumnSpan(_sslCheck, 2);

        var sslHint = new Label
        {
            Text = "Lokal/Self-signed: Haken entfernen | Nabu Casa/Lets Encrypt: Haken setzen",
            Font = new System.Drawing.Font("", 7),
            ForeColor = System.Drawing.Color.Gray,
            AutoSize = true,
        };
        panel.Controls.Add(sslHint, 0, 3);
        panel.SetColumnSpan(sslHint, 2);

        _autostartCheck = new CheckBox { Text = "Autostart (beim Windows-Start)", AutoSize = true };
        panel.Controls.Add(_autostartCheck, 0, 4);
        panel.SetColumnSpan(_autostartCheck, 2);

        panel.Controls.Add(new Label { Text = "Sensor-Intervall (Sek):", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 5);
        _intervalBox = new NumericUpDown { Minimum = 5, Maximum = 300, Value = 30, Dock = DockStyle.Fill };
        panel.Controls.Add(_intervalBox, 1, 5);

        panel.Controls.Add(new Label { Text = "Update-Kanal:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 6);
        _updateChannelBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        _updateChannelBox.Items.AddRange(new object[] { "stable", "prerelease" });
        panel.Controls.Add(_updateChannelBox, 1, 6);

        var channelHint = new Label
        {
            Text = "Stable = nur fertige Versionen | Pre-Release = auch Beta-Versionen",
            Font = new System.Drawing.Font("", 7),
            ForeColor = System.Drawing.Color.Gray,
            AutoSize = true,
        };
        panel.Controls.Add(channelHint, 0, 7);
        panel.SetColumnSpan(channelHint, 2);

        var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        var saveBtn = new Button { Text = "Speichern", Size = new System.Drawing.Size(100, 35) };
        saveBtn.Click += OnSave;
        btnPanel.Controls.Add(saveBtn);
        var reconnectBtn = new Button { Text = "Neu verbinden", Size = new System.Drawing.Size(120, 35) };
        reconnectBtn.Click += OnReconnectClicked;
        btnPanel.Controls.Add(reconnectBtn);
        var resetDeviceBtn = new Button { Text = "Neue Ger\u00e4te-ID", Size = new System.Drawing.Size(120, 35) };
        resetDeviceBtn.Click += OnResetDeviceId;
        btnPanel.Controls.Add(resetDeviceBtn);
        panel.Controls.Add(btnPanel, 0, 8);
        panel.SetColumnSpan(btnPanel, 2);

        _statusLabel = new Label { Text = "Bereit", ForeColor = System.Drawing.Color.Gray, AutoSize = true };
        panel.Controls.Add(_statusLabel, 0, 9);
        panel.SetColumnSpan(_statusLabel, 2);

        Controls.Add(panel);
    }

    private void LoadSettings()
    {
        _urlBox.Text = _config.HaUrl;
        _tokenBox.Text = _config.HaToken;
        _sslCheck.Checked = _config.VerifySsl;
        _autostartCheck.Checked = _config.Autostart;
        _intervalBox.Value = _config.SensorInterval;
        _updateChannelBox.SelectedIndex = _config.UpdateChannel == "prerelease" ? 1 : 0;
    }

    private void OnSave(object? sender, EventArgs e)
    {
        _config.HaUrl = _urlBox.Text;
        _config.HaToken = _tokenBox.Text;
        _config.VerifySsl = _sslCheck.Checked;
        _config.Autostart = _autostartCheck.Checked;
        _config.SensorInterval = (int)_intervalBox.Value;
        _config.UpdateChannel = _updateChannelBox.SelectedItem?.ToString() ?? "stable";
        _config.Save();
        if (_config.Autostart) Autostart.Enable(); else Autostart.Disable();
        _statusLabel.Text = "Gespeichert!";
    }

    private void OnReconnectClicked(object? sender, EventArgs e)
    {
        _statusLabel.Text = "Verbinde neu...";
        _onReconnect.Invoke();
        _statusLabel.Text = "Neu verbunden!";
    }

    private void OnResetDeviceId(object? sender, EventArgs e)
    {
        var result = MessageBox.Show(
            "Neue Ger\u00e4te-ID erstellen?\n\nDas alte Ger\u00e4t wird in Home Assistant zur\u00fcckgelassen. " +
            "Beim n\u00e4chsten Neustart wird ein neues Ger\u00e4t angelegt.",
            "Ger\u00e4te-ID zur\u00fccksetzen", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (result == DialogResult.Yes)
        {
            _api?.ResetDeviceId();
            _statusLabel.Text = "Neue ID erstellt – App bitte neustarten!";
        }
    }

    private static SettingsWindow? _instance;

    public static void Open(Config config, Action onReconnect, HaApiClient? api = null)
    {
        if (_instance != null && !_instance.IsDisposed)
        {
            _instance.Activate();
            return;
        }
        _instance = new SettingsWindow(config, onReconnect, api);
        _instance.Show();
    }
}