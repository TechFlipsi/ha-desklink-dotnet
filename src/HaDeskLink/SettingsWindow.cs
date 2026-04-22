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
using System.Drawing;
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
    private ComboBox _themeBox = null!;
    private Label _statusLabel = null!;
    private DataGridView _qaGrid = null!;

    // Colors
    private static readonly Color DarkBg = Color.FromArgb(30, 30, 30);
    private static readonly Color DarkFg = Color.FromArgb(220, 220, 220);
    private static readonly Color DarkInput = Color.FromArgb(45, 45, 45);
    private static readonly Color DarkBorder = Color.FromArgb(60, 60, 60);

    public SettingsWindow(Config config, Action onReconnect, HaApiClient? api = null)
    {
        _config = config;
        _onReconnect = onReconnect;
        _api = api;
        Text = $"HA DeskLink - {Localization.Get("settings_title")}";
        Size = new Size(520, 850);
        MinimumSize = new Size(480, 750);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        InitializeComponents();
        LoadSettings();
        ApplyTheme(_config.Theme);
    }

    private void InitializeComponents()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 16,
            Padding = new Padding(15),
            AutoScroll = true,
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // Row 0: HA URL
        panel.Controls.Add(new Label { Text = Localization.Get("settings_ha_url"), Anchor = AnchorStyles.Left, AutoSize = true }, 0, 0);
        _urlBox = new TextBox { Dock = DockStyle.Fill, Text = "https://homeassistant.local:8123" };
        panel.Controls.Add(_urlBox, 1, 0);

        // Row 1: Token
        panel.Controls.Add(new Label { Text = Localization.Get("settings_token"), Anchor = AnchorStyles.Left, AutoSize = true }, 0, 1);
        _tokenBox = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
        panel.Controls.Add(_tokenBox, 1, 1);

        // Row 2: SSL
        _sslCheck = new CheckBox { Text = Localization.Get("settings_verify_ssl"), AutoSize = true };
        panel.Controls.Add(_sslCheck, 0, 2);
        panel.SetColumnSpan(_sslCheck, 2);

        // Row 3: SSL hint
        var sslHint = new Label
        {
            Text = "Local/Self-signed: Uncheck | Nabu Casa/Let's Encrypt: Check",
            Font = new Font("", 7),
            ForeColor = Color.Gray,
            AutoSize = true,
        };
        panel.Controls.Add(sslHint, 0, 3);
        panel.SetColumnSpan(sslHint, 2);

        // Row 4: Autostart
        _autostartCheck = new CheckBox { Text = Localization.Get("settings_autostart"), AutoSize = true };
        panel.Controls.Add(_autostartCheck, 0, 4);
        panel.SetColumnSpan(_autostartCheck, 2);

        // Row 5: Sensor interval
        panel.Controls.Add(new Label { Text = Localization.Get("settings_sensor_interval"), Anchor = AnchorStyles.Left, AutoSize = true }, 0, 5);
        _intervalBox = new NumericUpDown { Minimum = 5, Maximum = 300, Value = 30, Dock = DockStyle.Fill };
        panel.Controls.Add(_intervalBox, 1, 5);

        // Row 6: Update channel
        panel.Controls.Add(new Label { Text = Localization.Get("settings_update_channel"), Anchor = AnchorStyles.Left, AutoSize = true }, 0, 6);
        _updateChannelBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        _updateChannelBox.Items.AddRange(new object[] { Localization.Get("settings_channel_stable"), Localization.Get("settings_channel_prerelease") });
        panel.Controls.Add(_updateChannelBox, 1, 6);

        // Row 7: Language
        panel.Controls.Add(new Label { Text = Localization.Get("settings_language"), Anchor = AnchorStyles.Left, AutoSize = true }, 0, 7);
        _languageBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var lang in Localization.AvailableLanguages)
        {
            _languageBox.Items.Add($"{Localization.GetLanguageName(lang)} ({lang})");
        }
        panel.Controls.Add(_languageBox, 1, 7);

        // Row 8: Theme (Dark/Light)
        panel.Controls.Add(new Label { Text = Localization.Get("settings_theme"), Anchor = AnchorStyles.Left, AutoSize = true }, 0, 8);
        _themeBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        _themeBox.Items.AddRange(new object[] { Localization.Get("settings_theme_system"), Localization.Get("settings_theme_light"), Localization.Get("settings_theme_dark") });
        panel.Controls.Add(_themeBox, 1, 8);

        // Row 9: Buttons
        var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AutoSize = true };
        var saveBtn = new Button { Text = Localization.Get("settings_save"), Size = new Size(100, 35) };
        saveBtn.Click += OnSave;
        btnPanel.Controls.Add(saveBtn);
        var reconnectBtn = new Button { Text = Localization.Get("settings_reconnect_msg").Replace("...", ""), Size = new Size(120, 35) };
        reconnectBtn.Click += OnReconnectClicked;
        btnPanel.Controls.Add(reconnectBtn);
        var resetDeviceBtn = new Button { Text = Localization.Get("settings_reset_device"), Size = new Size(140, 35) };
        resetDeviceBtn.Click += OnResetDeviceId;
        btnPanel.Controls.Add(resetDeviceBtn);
        var reRegisterBtn = new Button { Text = Localization.Get("settings_reregister_sensors"), Size = new Size(160, 35) };
        reRegisterBtn.Click += OnReRegisterSensors;
        btnPanel.Controls.Add(reRegisterBtn);
        panel.Controls.Add(btnPanel, 0, 9);
        panel.SetColumnSpan(btnPanel, 2);

        // Row 10: Status label
        _statusLabel = new Label { Text = Localization.Get("settings_saved"), ForeColor = Color.Gray, AutoSize = true };
        panel.Controls.Add(_statusLabel, 0, 10);
        panel.SetColumnSpan(_statusLabel, 2);

        // Row 11: Quick Actions header
        var qaLabel = new Label { Text = Localization.Get("settings_quickactions"), AutoSize = true, Font = new Font("", 10, FontStyle.Bold) };
        panel.Controls.Add(qaLabel, 0, 11);
        panel.SetColumnSpan(qaLabel, 2);

        // Row 12: Quick Actions description
        var qaDesc = new Label { Text = Localization.Get("settings_quickactions_desc"), AutoSize = true, ForeColor = Color.Gray };
        panel.Controls.Add(qaDesc, 0, 12);
        panel.SetColumnSpan(qaDesc, 2);

        // Row 13: Quick Actions grid
        _qaGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = true,
            AllowUserToDeleteRows = true,
            ColumnCount = 2,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            Height = 150,
            MaximumSize = new Size(0, 200),
        };
        _qaGrid.Columns[0].HeaderText = Localization.Get("settings_qa_entity");
        _qaGrid.Columns[1].HeaderText = Localization.Get("settings_qa_name");
        panel.Controls.Add(_qaGrid, 0, 13);
        panel.SetColumnSpan(_qaGrid, 2);

        // Row 14: Save Quick Actions button
        var saveQaBtn = new Button { Text = Localization.Get("settings_save_quickactions"), Size = new Size(180, 35) };
        saveQaBtn.Click += OnSaveQuickActions;
        panel.Controls.Add(saveQaBtn, 0, 14);
        panel.SetColumnSpan(saveQaBtn, 2);

        Controls.Add(panel);
    }

    private void ApplyTheme(string theme)
    {
        bool dark = theme == "dark" || (theme == "system" && IsSystemDark());

        if (dark)
        {
            BackColor = DarkBg;
            ForeColor = DarkFg;
            foreach (Control c in GetAllControls(this))
            {
                if (c is TextBox || c is ComboBox || c is NumericUpDown)
                {
                    c.BackColor = DarkInput;
                    c.ForeColor = DarkFg;
                }
                else if (c is DataGridView grid)
                {
                    grid.BackgroundColor = DarkInput;
                    grid.DefaultCellStyle.BackColor = DarkInput;
                    grid.DefaultCellStyle.ForeColor = DarkFg;
                    grid.ColumnHeadersDefaultCellStyle.BackColor = DarkBorder;
                    grid.ColumnHeadersDefaultCellStyle.ForeColor = DarkFg;
                    grid.EnableHeadersVisualStyles = false;
                }
                else if (c is Button btn)
                {
                    btn.BackColor = DarkBorder;
                    btn.ForeColor = DarkFg;
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
                }
                else if (c is CheckBox cb)
                {
                    cb.ForeColor = DarkFg;
                }
                else if (c is Label lbl)
                {
                    if (lbl.ForeColor != Color.Gray)
                        lbl.ForeColor = DarkFg;
                }
            }
        }
        else
        {
            BackColor = SystemColors.Window;
            ForeColor = SystemColors.WindowText;
            foreach (Control c in GetAllControls(this))
            {
                if (c is TextBox || c is ComboBox || c is NumericUpDown)
                {
                    c.BackColor = SystemColors.Window;
                    c.ForeColor = SystemColors.WindowText;
                }
                else if (c is DataGridView grid)
                {
                    grid.BackgroundColor = SystemColors.Window;
                    grid.DefaultCellStyle.BackColor = SystemColors.Window;
                    grid.DefaultCellStyle.ForeColor = SystemColors.WindowText;
                    grid.ColumnHeadersDefaultCellStyle.BackColor = SystemColors.Control;
                    grid.ColumnHeadersDefaultCellStyle.ForeColor = SystemColors.WindowText;
                    grid.EnableHeadersVisualStyles = true;
                }
                else if (c is Button btn)
                {
                    btn.BackColor = SystemColors.Control;
                    btn.ForeColor = SystemColors.WindowText;
                    btn.FlatStyle = FlatStyle.Standard;
                }
                else if (c is CheckBox cb)
                {
                    cb.ForeColor = SystemColors.WindowText;
                }
                else if (c is Label lbl)
                {
                    if (lbl.ForeColor != Color.Gray)
                        lbl.ForeColor = SystemColors.WindowText;
                }
            }
        }
    }

    private static bool IsSystemDark()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key != null)
            {
                var value = key.GetValue("AppsUseLightTheme");
                if (value is int v) return v == 0;
            }
        }
        catch { }
        return false;
    }

    private static IEnumerable<Control> GetAllControls(Control container)
    {
        foreach (Control c in container.Controls)
        {
            yield return c;
            foreach (var child in GetAllControls(c))
                yield return child;
        }
    }

    private void LoadSettings()
    {
        _urlBox.Text = _config.HaUrl;
        _tokenBox.Text = _config.HaToken;
        _sslCheck.Checked = _config.VerifySsl;
        _autostartCheck.Checked = _config.Autostart;
        _intervalBox.Value = _config.SensorInterval;
        _updateChannelBox.SelectedIndex = _config.UpdateChannel == "prerelease" ? 1 : 0;

        var currentLangIndex = Localization.AvailableLanguages.IndexOf(_config.Language);
        if (currentLangIndex < 0) currentLangIndex = 0;
        _languageBox.SelectedIndex = currentLangIndex;

        // Theme
        _themeBox.SelectedIndex = _config.Theme switch
        {
            "light" => 1,
            "dark" => 2,
            _ => 0 // system
        };

        // Load Quick Actions into grid
        try
        {
            var actions = JsonSerializer.Deserialize<List<QuickAction>>(_config.QuickActions) ?? new List<QuickAction>();
            foreach (var action in actions)
            {
                _qaGrid.Rows.Add(action.EntityId, action.Name);
            }
        }
        catch { }
    }

    private void OnSave(object? sender, EventArgs e)
    {
        _config.HaUrl = _urlBox.Text;
        _config.HaToken = _tokenBox.Text;
        _config.VerifySsl = _sslCheck.Checked;
        _config.Autostart = _autostartCheck.Checked;
        _config.SensorInterval = (int)_intervalBox.Value;
        _config.UpdateChannel = _updateChannelBox.SelectedIndex == 1 ? "prerelease" : "stable";

        if (_languageBox.SelectedIndex >= 0 && _languageBox.SelectedIndex < Localization.AvailableLanguages.Count)
        {
            _config.Language = Localization.AvailableLanguages[_languageBox.SelectedIndex];
        }

        // Theme
        _config.Theme = _themeBox.SelectedIndex switch
        {
            1 => "light",
            2 => "dark",
            _ => "system"
        };

        _config.Save();
        if (_config.Autostart) Autostart.Enable(); else Autostart.Disable();
        ApplyTheme(_config.Theme);
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
            Localization.Get("settings_reset_device_confirm"),
            Localization.Get("settings_reset_device"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (result == DialogResult.Yes)
        {
            _api?.ResetDeviceId();
            _statusLabel.Text = Localization.Get("settings_reset_device_done");
        }
    }

    private void OnReRegisterSensors(object? sender, EventArgs e)
    {
        var result = MessageBox.Show(
            Localization.Get("settings_reregister_confirm"),
            Localization.Get("settings_reregister_sensors"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (result == DialogResult.Yes)
        {
            DeskLinkApp.ReRegisterSensors();
            _statusLabel.Text = Localization.Get("settings_reregister_done");
        }
    }

    private void OnSaveQuickActions(object? sender, EventArgs e)
    {
        var actions = new List<QuickAction>();
        foreach (DataGridViewRow row in _qaGrid.Rows)
        {
            if (row.IsNewRow) continue;
            var entityId = row.Cells[0].Value?.ToString()?.Trim() ?? "";
            var name = row.Cells[1].Value?.ToString()?.Trim() ?? "";
            if (!string.IsNullOrEmpty(entityId) && !string.IsNullOrEmpty(name))
                actions.Add(new QuickAction(entityId, name));
        }
        _config.QuickActions = JsonSerializer.Serialize(actions);
        _config.Save();
        _statusLabel.Text = Localization.Get("settings_quickactions_saved");
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