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
    private ComboBox _languageBox = null!;
    private Label _statusLabel = null!;

    public SettingsWindow(Config config, Action onReconnect, HaApiClient? api = null)
    {
        _config = config;
        _onReconnect = onReconnect;
        _api = api;
        Text = $"HA DeskLink - {Localization.Get("settings_title")}";
        Size = new System.Drawing.Size(520, 600);
        MinimumSize = new System.Drawing.Size(480, 500);
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
            RowCount = 10,
            Padding = new Padding(15),
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        panel.Controls.Add(new Label { Text = Localization.Get("settings_ha_url"), Anchor = AnchorStyles.Left, AutoSize = true }, 0, 0);
        _urlBox = new TextBox { Dock = DockStyle.Fill, Text = "https://homeassistant.local:8123" };
        panel.Controls.Add(_urlBox, 1, 0);

        panel.Controls.Add(new Label { Text = Localization.Get("settings_token"), Anchor = AnchorStyles.Left, AutoSize = true }, 0, 1);
        _tokenBox = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
        panel.Controls.Add(_tokenBox, 1, 1);

        _sslCheck = new CheckBox { Text = Localization.Get("settings_verify_ssl"), AutoSize = true };
        panel.Controls.Add(_sslCheck, 0, 2);
        panel.SetColumnSpan(_sslCheck, 2);

        var sslHint = new Label
        {
            Text = "Local/Self-signed: Uncheck | Nabu Casa/Let's Encrypt: Check",
            Font = new System.Drawing.Font("", 7),
            ForeColor = System.Drawing.Color.Gray,
            AutoSize = true,
        };
        panel.Controls.Add(sslHint, 0, 3);
        panel.SetColumnSpan(sslHint, 2);

        _autostartCheck = new CheckBox { Text = Localization.Get("settings_autostart"), AutoSize = true };
        panel.Controls.Add(_autostartCheck, 0, 4);
        panel.SetColumnSpan(_autostartCheck, 2);

        panel.Controls.Add(new Label { Text = Localization.Get("settings_sensor_interval"), Anchor = AnchorStyles.Left, AutoSize = true }, 0, 5);
        _intervalBox = new NumericUpDown { Minimum = 5, Maximum = 300, Value = 30, Dock = DockStyle.Fill };
        panel.Controls.Add(_intervalBox, 1, 5);

        panel.Controls.Add(new Label { Text = Localization.Get("settings_update_channel"), Anchor = AnchorStyles.Left, AutoSize = true }, 0, 6);
        _updateChannelBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        _updateChannelBox.Items.AddRange(new object[] { Localization.Get("settings_channel_stable"), Localization.Get("settings_channel_prerelease") });
        panel.Controls.Add(_updateChannelBox, 1, 6);

        panel.Controls.Add(new Label { Text = Localization.Get("settings_language"), Anchor = AnchorStyles.Left, AutoSize = true }, 0, 7);
        _languageBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var lang in Localization.AvailableLanguages)
        {
            _languageBox.Items.Add($"{Localization.GetLanguageName(lang)} ({lang})");
        }
        panel.Controls.Add(_languageBox, 1, 7);

        var langHint = new Label
        {
            Text = "Restart required after language change",
            Font = new System.Drawing.Font("", 7),
            ForeColor = System.Drawing.Color.Gray,
            AutoSize = true,
        };
        panel.Controls.Add(langHint, 0, 8);
        panel.SetColumnSpan(langHint, 2);

        var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        var saveBtn = new Button { Text = Localization.Get("settings_save"), Size = new System.Drawing.Size(100, 35) };
        saveBtn.Click += OnSave;
        btnPanel.Controls.Add(saveBtn);
        var reconnectBtn = new Button { Text = Localization.Get("settings_reconnect_msg").Replace("...", ""), Size = new System.Drawing.Size(120, 35) };
        reconnectBtn.Click += OnReconnectClicked;
        btnPanel.Controls.Add(reconnectBtn);
        var resetDeviceBtn = new Button { Text = Localization.Get("tray_settings"), Size = new System.Drawing.Size(120, 35) };
        resetDeviceBtn.Click += OnResetDeviceId;
        btnPanel.Controls.Add(resetDeviceBtn);
        panel.Controls.Add(btnPanel, 0, 9);
        panel.SetColumnSpan(btnPanel, 2);

        _statusLabel = new Label { Text = Localization.Get("settings_saved"), ForeColor = System.Drawing.Color.Gray, AutoSize = true };
        panel.Controls.Add(_statusLabel, 0, 10);
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

        // Select current language
        var currentLangIndex = Localization.AvailableLanguages.IndexOf(_config.Language);
        if (currentLangIndex < 0) currentLangIndex = 0;
        _languageBox.SelectedIndex = currentLangIndex;
    }

    private void OnSave(object? sender, EventArgs e)
    {
        _config.HaUrl = _urlBox.Text;
        _config.HaToken = _tokenBox.Text;
        _config.VerifySsl = _sslCheck.Checked;
        _config.Autostart = _autostartCheck.Checked;
        _config.SensorInterval = (int)_intervalBox.Value;
        _config.UpdateChannel = _updateChannelBox.SelectedIndex == 1 ? "prerelease" : "stable";

        // Save language
        if (_languageBox.SelectedIndex >= 0 && _languageBox.SelectedIndex < Localization.AvailableLanguages.Count)
        {
            _config.Language = Localization.AvailableLanguages[_languageBox.SelectedIndex];
        }

        _config.Save();
        if (_config.Autostart) Autostart.Enable(); else Autostart.Disable();
        _statusLabel.Text = Localization.Get("settings_saved");
    }

    private void OnReconnectClicked(object? sender, EventArgs e)
    {
        _statusLabel.Text = Localization.Get("settings_reconnect_msg");
        _onReconnect.Invoke();
        _statusLabel.Text = Localization.Get("settings_saved");
    }

    private void OnResetDeviceId(object? sender, EventArgs e)
    {
        var result = MessageBox.Show(
            "Reset device ID?\n\nThe old device will remain in Home Assistant. " +
            "A new device will be created on next restart.",
            "Reset Device ID", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (result == DialogResult.Yes)
        {
            _api?.ResetDeviceId();
            _statusLabel.Text = "New ID created – please restart!";
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