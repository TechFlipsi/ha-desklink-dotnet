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
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace HaDeskLink;

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
    private ComboBox _hotkeyModBox = null!;
    private ComboBox _hotkeyKeyBox = null!;
    private ComboBox _hotkeyDashModBox = null!;
    private ComboBox _hotkeyDashKeyBox = null!;
    private ComboBox _hotkeySettingsModBox = null!;
    private ComboBox _hotkeySettingsKeyBox = null!;
    private Label _statusLabel = null!;
    private DataGridView _qaGrid = null!;

    private static readonly Color DarkBg = Color.FromArgb(32, 32, 32);
    private static readonly Color DarkFg = Color.FromArgb(230, 230, 230);
    private static readonly Color DarkInput = Color.FromArgb(48, 48, 48);
    private static readonly Color DarkBorder = Color.FromArgb(64, 64, 64);
    private static readonly Color AccentBlue = Color.FromArgb(0, 120, 215);

    public SettingsWindow(Config config, Action onReconnect, HaApiClient? api = null)
    {
        _config = config;
        _onReconnect = onReconnect;
        _api = api;
        Text = $"HA DeskLink - {Localization.Get("settings_title")}";
        Size = new Size(620, 960);
        MinimumSize = new Size(520, 700);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        InitializeComponents();
        LoadSettings();
        ApplyTheme(_config.Theme);
    }

    private void InitializeComponents()
    {
        // Main panel that fills the entire window and scrolls
        var mainPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(20) };

        // Inner container that stretches to fill width
        var content = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Width = mainPanel.Width - 50,
        };

        // Resize content when panel resizes
        mainPanel.Resize += (s, e) => { content.Width = mainPanel.Width - 50; };

        // === Connection Section ===
        content.Controls.Add(MakeHeader("🔌 " + Localization.Get("settings_connection", "Connection")));

        var connTable = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, RowCount = 3, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(0, 5, 0, 15) };
        connTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        connTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _urlBox = new TextBox { Dock = DockStyle.Fill, Text = "https://homeassistant.local:8123" };
        connTable.Controls.Add(MakeLabel(Localization.Get("settings_ha_url")), 0, 0);
        connTable.Controls.Add(_urlBox, 1, 0);
        connTable.Controls.Add(MakeLabel(Localization.Get("settings_token")), 0, 1);
        _tokenBox = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
        connTable.Controls.Add(_tokenBox, 1, 1);
        _sslCheck = new CheckBox { Text = Localization.Get("settings_verify_ssl"), AutoSize = true };
        connTable.Controls.Add(_sslCheck, 0, 2);
        connTable.SetColumnSpan(_sslCheck, 2);
        content.Controls.Add(connTable);

        // === General Section ===
        content.Controls.Add(MakeHeader("⚙️ " + Localization.Get("settings_general", "General")));

        var genTable = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, RowCount = 8, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(0, 5, 0, 15) };
        genTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        genTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _autostartCheck = new CheckBox { Text = Localization.Get("settings_autostart"), AutoSize = true };
        genTable.Controls.Add(_autostartCheck, 0, 0);
        genTable.SetColumnSpan(_autostartCheck, 2);

        genTable.Controls.Add(MakeLabel(Localization.Get("settings_sensor_interval")), 0, 1);
        _intervalBox = new NumericUpDown { Minimum = 5, Maximum = 300, Value = 30, Dock = DockStyle.Fill };
        genTable.Controls.Add(_intervalBox, 1, 1);

        genTable.Controls.Add(MakeLabel(Localization.Get("settings_update_channel")), 0, 2);
        _updateChannelBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        _updateChannelBox.Items.AddRange(new object[] { Localization.Get("settings_channel_stable"), Localization.Get("settings_channel_prerelease") });
        genTable.Controls.Add(_updateChannelBox, 1, 2);

        genTable.Controls.Add(MakeLabel(Localization.Get("settings_language")), 0, 3);
        _languageBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var lang in Localization.AvailableLanguages)
            _languageBox.Items.Add($"{Localization.GetLanguageName(lang)} ({lang})");
        genTable.Controls.Add(_languageBox, 1, 3);

        genTable.Controls.Add(MakeLabel(Localization.Get("settings_theme")), 0, 4);
        _themeBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        _themeBox.Items.AddRange(new object[] { Localization.Get("settings_theme_system"), Localization.Get("settings_theme_light"), Localization.Get("settings_theme_dark") });
        genTable.Controls.Add(_themeBox, 1, 4);

        // Hotkey: Quick Actions
        genTable.Controls.Add(MakeLabel(Localization.Get("settings_hotkey_qa")), 0, 5);
        var hotkeyPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        _hotkeyModBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
        _hotkeyModBox.Items.AddRange(new object[] { "Ctrl+Shift", "Ctrl+Alt", "Ctrl", "Alt", "Shift", Localization.Get("settings_hotkey_none") });
        hotkeyPanel.Controls.Add(_hotkeyModBox);
        hotkeyPanel.Controls.Add(new Label { Text = "+", AutoSize = true, Margin = new Padding(4, 6, 4, 0) });
        _hotkeyKeyBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
        _hotkeyKeyBox.Items.AddRange(new object[] { "H", "Q", "A", "S", "D", "F", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12", "Space" });
        hotkeyPanel.Controls.Add(_hotkeyKeyBox);
        genTable.Controls.Add(hotkeyPanel, 1, 5);

        // Hotkey: Dashboard
        genTable.Controls.Add(MakeLabel(Localization.Get("settings_hotkey_dashboard")), 0, 6);
        var dashHotkeyPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        _hotkeyDashModBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
        _hotkeyDashModBox.Items.AddRange(new object[] { "Ctrl+Shift", "Ctrl+Alt", "Ctrl", "Alt", "Shift", Localization.Get("settings_hotkey_none") });
        dashHotkeyPanel.Controls.Add(_hotkeyDashModBox);
        dashHotkeyPanel.Controls.Add(new Label { Text = "+", AutoSize = true, Margin = new Padding(4, 6, 4, 0) });
        _hotkeyDashKeyBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
        _hotkeyDashKeyBox.Items.AddRange(new object[] { "D", "H", "Q", "A", "S", "F", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12", "Space" });
        dashHotkeyPanel.Controls.Add(_hotkeyDashKeyBox);
        genTable.Controls.Add(dashHotkeyPanel, 1, 6);

        // Hotkey: Settings
        genTable.Controls.Add(MakeLabel(Localization.Get("settings_hotkey_settings")), 0, 7);
        var settingsHotkeyPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        _hotkeySettingsModBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
        _hotkeySettingsModBox.Items.AddRange(new object[] { "Ctrl+Shift", "Ctrl+Alt", "Ctrl", "Alt", "Shift", Localization.Get("settings_hotkey_none") });
        settingsHotkeyPanel.Controls.Add(_hotkeySettingsModBox);
        settingsHotkeyPanel.Controls.Add(new Label { Text = "+", AutoSize = true, Margin = new Padding(4, 6, 4, 0) });
        _hotkeySettingsKeyBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
        _hotkeySettingsKeyBox.Items.AddRange(new object[] { "S", "H", "Q", "A", "D", "F", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12", "Space" });
        settingsHotkeyPanel.Controls.Add(_hotkeySettingsKeyBox);
        genTable.Controls.Add(settingsHotkeyPanel, 1, 7);

        content.Controls.Add(genTable);

        // === Action Buttons ===
        content.Controls.Add(MakeHeader("🔧 " + Localization.Get("settings_actions", "Actions")));
        var actionPanel = new FlowLayoutPanel { Dock = DockStyle.Top, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AutoSize = true, Padding = new Padding(0, 5, 0, 15) };
        actionPanel.Controls.Add(MakeButton(Localization.Get("settings_save"), AccentBlue, OnSave));
        actionPanel.Controls.Add(MakeButton(Localization.Get("settings_reconnect_msg").Replace("...", ""), Color.FromArgb(0, 100, 180), OnReconnectClicked));
        actionPanel.Controls.Add(MakeButton(Localization.Get("settings_reset_device"), Color.FromArgb(180, 80, 0), OnResetDeviceId));
        actionPanel.Controls.Add(MakeButton(Localization.Get("settings_reregister_sensors"), Color.FromArgb(0, 130, 100), OnReRegisterSensors));
        content.Controls.Add(actionPanel);

        // === Quick Actions Section ===
        content.Controls.Add(MakeHeader("⚡ " + Localization.Get("settings_quickactions")));
        content.Controls.Add(new Label { Text = Localization.Get("settings_quickactions_desc"), AutoSize = true, ForeColor = Color.Gray, Margin = new Padding(0, 0, 0, 8) });

        _qaGrid = new DataGridView
        {
            Dock = DockStyle.Top,
            AllowUserToAddRows = true,
            AllowUserToDeleteRows = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            MinimumSize = new Size(0, 150),
            MaximumSize = new Size(0, 220),
        };
        var entityCol = new DataGridViewComboBoxColumn
        {
            HeaderText = Localization.Get("settings_qa_entity"),
            Name = "EntityID",
            AutoComplete = true,
            FlatStyle = FlatStyle.Standard,
            Width = 250,
        };
        var nameCol = new DataGridViewTextBoxColumn
        {
            HeaderText = Localization.Get("settings_qa_name"),
            Name = "Name",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
        };
        _qaGrid.Columns.AddRange(entityCol, nameCol);
        _qaGrid.EditingControlShowing += OnGridEditingControlShowing;
        content.Controls.Add(_qaGrid);

        var qaBtnPanel = new FlowLayoutPanel { Dock = DockStyle.Top, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AutoSize = true, Padding = new Padding(0, 8, 0, 15) };
        qaBtnPanel.Controls.Add(MakeButton(Localization.Get("settings_load_entities"), Color.FromArgb(0, 130, 100), OnLoadEntities));
        qaBtnPanel.Controls.Add(MakeButton(Localization.Get("settings_edit_json"), Color.FromArgb(100, 100, 100), OnEditJson));
        qaBtnPanel.Controls.Add(MakeButton(Localization.Get("settings_save_quickactions"), AccentBlue, OnSaveQuickActions));
        content.Controls.Add(qaBtnPanel);

        // === Status ===
        _statusLabel = new Label { Text = Localization.Get("settings_saved"), ForeColor = Color.Gray, AutoSize = true, Margin = new Padding(0, 4, 0, 8) };
        content.Controls.Add(_statusLabel);

        mainPanel.Controls.Add(content);
        Controls.Add(mainPanel);
    }

    private static Label MakeHeader(string text)
    {
        return new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 2),
        };
    }

    private static Label MakeLabel(string text)
    {
        return new Label { Text = text, AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
    }

    private static Button MakeButton(string text, Color color, EventHandler onClick)
    {
        var btn = new Button
        {
            Text = text,
            Size = new Size(155, 38),
            BackColor = color,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f),
            Margin = new Padding(2),
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += onClick;
        return btn;
    }

    private void ApplyTheme(string theme)
    {
        bool dark = theme == "dark" || (theme == "system" && IsSystemDark());
        Color bg = dark ? DarkBg : SystemColors.Window;
        Color fg = dark ? DarkFg : SystemColors.WindowText;
        Color inputBg = dark ? DarkInput : SystemColors.Window;
        Color groupBg = dark ? DarkBg : SystemColors.Window;

        BackColor = bg;
        ForeColor = fg;

        foreach (Control c in GetAllControls(this))
        {
            if (c is TextBox || c is ComboBox || c is NumericUpDown)
            {
                c.BackColor = inputBg;
                c.ForeColor = fg;
            }
            else if (c is DataGridView grid)
            {
                grid.BackgroundColor = inputBg;
                grid.DefaultCellStyle.BackColor = inputBg;
                grid.DefaultCellStyle.ForeColor = fg;
                grid.ColumnHeadersDefaultCellStyle.BackColor = dark ? DarkBorder : SystemColors.Control;
                grid.ColumnHeadersDefaultCellStyle.ForeColor = fg;
                grid.EnableHeadersVisualStyles = !dark;
            }
            else if (c is Button btn)
            {
                // Keep accent-colored buttons as-is
                if (btn.ForeColor != Color.White)
                    btn.ForeColor = fg;
            }
            else if (c is CheckBox cb)
            {
                cb.ForeColor = fg;
            }
            else if (c is Label lbl)
            {
                if (lbl.ForeColor != Color.Gray && !IsAccentColor(lbl.ForeColor))
                    lbl.ForeColor = fg;
            }
        }
    }

    private static bool IsAccentColor(Color c) => c == AccentBlue;

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

        _themeBox.SelectedIndex = _config.Theme switch { "light" => 1, "dark" => 2, _ => 0 };

        _hotkeyModBox.SelectedIndex = _config.HotkeyModifiers switch
        {
            "ctrl_shift" => 0, "ctrl_alt" => 1, "ctrl" => 2, "alt" => 3, "shift" => 4, "none" => 5, _ => 0
        };
        var keyIndex = _hotkeyKeyBox.Items.IndexOf(_config.HotkeyKey.ToUpper());
        _hotkeyKeyBox.SelectedIndex = keyIndex >= 0 ? keyIndex : 0;

        // Dashboard hotkey
        _hotkeyDashModBox.SelectedIndex = _config.HotkeyDashboardModifiers switch
        {
            "ctrl_shift" => 0, "ctrl_alt" => 1, "ctrl" => 2, "alt" => 3, "shift" => 4, "none" => 5, _ => 0
        };
        var dashKeyIndex = _hotkeyDashKeyBox.Items.IndexOf(_config.HotkeyDashboardKey.ToUpper());
        _hotkeyDashKeyBox.SelectedIndex = dashKeyIndex >= 0 ? dashKeyIndex : 0;

        // Settings hotkey
        _hotkeySettingsModBox.SelectedIndex = _config.HotkeySettingsModifiers switch
        {
            "ctrl_shift" => 0, "ctrl_alt" => 1, "ctrl" => 2, "alt" => 3, "shift" => 4, "none" => 5, _ => 0
        };
        var settingsKeyIndex = _hotkeySettingsKeyBox.Items.IndexOf(_config.HotkeySettingsKey.ToUpper());
        _hotkeySettingsKeyBox.SelectedIndex = settingsKeyIndex >= 0 ? settingsKeyIndex : 0;

        try
        {
            var actions = JsonSerializer.Deserialize<List<QuickAction>>(_config.QuickActions) ?? new List<QuickAction>();
            foreach (var action in actions)
                _qaGrid.Rows.Add(action.EntityId, action.Name);
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
            _config.Language = Localization.AvailableLanguages[_languageBox.SelectedIndex];

        _config.Theme = _themeBox.SelectedIndex switch { 1 => "light", 2 => "dark", _ => "system" };

        _config.HotkeyModifiers = _hotkeyModBox.SelectedIndex switch
        {
            0 => "ctrl_shift", 1 => "ctrl_alt", 2 => "ctrl", 3 => "alt", 4 => "shift", 5 => "none", _ => "ctrl_shift"
        };
        _config.HotkeyKey = _hotkeyKeyBox.SelectedItem?.ToString() ?? "H";

        _config.HotkeyDashboardModifiers = _hotkeyDashModBox.SelectedIndex switch
        {
            0 => "ctrl_shift", 1 => "ctrl_alt", 2 => "ctrl", 3 => "alt", 4 => "shift", 5 => "none", _ => "ctrl_shift"
        };
        _config.HotkeyDashboardKey = _hotkeyDashKeyBox.SelectedItem?.ToString() ?? "D";

        _config.HotkeySettingsModifiers = _hotkeySettingsModBox.SelectedIndex switch
        {
            0 => "ctrl_shift", 1 => "ctrl_alt", 2 => "ctrl", 3 => "alt", 4 => "shift", 5 => "none", _ => "ctrl_shift"
        };
        _config.HotkeySettingsKey = _hotkeySettingsKeyBox.SelectedItem?.ToString() ?? "S";

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
        var result = MessageBox.Show(Localization.Get("settings_reset_device_confirm"),
            Localization.Get("settings_reset_device"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (result == DialogResult.Yes)
        {
            _api?.ResetDeviceId();
            _statusLabel.Text = Localization.Get("settings_reset_device_done");
        }
    }

    private void OnReRegisterSensors(object? sender, EventArgs e)
    {
        var result = MessageBox.Show(Localization.Get("settings_reregister_confirm"),
            Localization.Get("settings_reregister_sensors"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (result == DialogResult.Yes)
        {
            DeskLinkApp.ReRegisterSensors();
            _statusLabel.Text = Localization.Get("settings_reregister_done");
        }
    }

    private List<(string entityId, string friendlyName)> _entities = new();
    private List<string> _entityList = new();

    private async void OnLoadEntities(object? sender, EventArgs e)
    {
        if (_api == null) { _statusLabel.Text = Localization.Get("settings_no_connection"); return; }

        _statusLabel.Text = Localization.Get("settings_loading_entities");
        try
        {
            _entities = await _api.GetEntitiesAsync();
            _entityList = _entities.Select(e => e.entityId).OrderBy(x => x).ToList();

            // Populate ComboBox column items
            if (_qaGrid.Columns[0] is DataGridViewComboBoxColumn comboCol)
            {
                comboCol.Items.Clear();
                comboCol.Items.AddRange(_entityList.ToArray());
            }

            _statusLabel.Text = $"{Localization.Get("settings_entities_loaded")} ({_entities.Count})";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"{Localization.Get("settings_entities_failed")}: {ex.Message}";
        }
    }

    private void OnGridEditingControlShowing(object? sender, DataGridViewEditingControlShowingEventArgs e)
    {
        if (_qaGrid.CurrentCell.ColumnIndex == 0 && e.Control is ComboBox cb)
        {
            cb.DropDownStyle = ComboBoxStyle.DropDown;
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

    private void OnEditJson(object? sender, EventArgs e)
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
        var json = JsonSerializer.Serialize(actions, new JsonSerializerOptions { WriteIndented = true });

        using var dialog = new Form
        {
            Text = "Quick Actions JSON",
            Size = new Size(550, 450),
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false
        };

        var jsonBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 10),
            Text = json
        };

        var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 50, Padding = new Padding(8) };
        var cancelBtn = new Button { Text = Localization.Get("settings_cancel"), Size = new Size(90, 35) };
        cancelBtn.Click += (s, ev) => dialog.Close();
        btnPanel.Controls.Add(cancelBtn);
        var applyBtn = new Button { Text = Localization.Get("settings_save"), Size = new Size(90, 35) };
        applyBtn.Click += (s, ev) =>
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<QuickAction>>(jsonBox.Text);
                if (parsed != null)
                {
                    _qaGrid.Rows.Clear();
                    foreach (var action in parsed)
                        if (!string.IsNullOrEmpty(action.EntityId) && !string.IsNullOrEmpty(action.Name))
                            _qaGrid.Rows.Add(action.EntityId, action.Name);
                    _statusLabel.Text = Localization.Get("settings_quickactions_saved");
                    dialog.Close();
                }
            }
            catch (JsonException ex)
            {
                MessageBox.Show($"JSON Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        btnPanel.Controls.Add(applyBtn);

        bool dark = _config.Theme == "dark" || (_config.Theme == "system" && IsSystemDark());
        if (dark)
        {
            dialog.BackColor = DarkBg;
            jsonBox.BackColor = DarkInput;
            jsonBox.ForeColor = DarkFg;
            btnPanel.BackColor = DarkBg;
        }

        dialog.Controls.Add(jsonBox);
        dialog.Controls.Add(btnPanel);
        dialog.ShowDialog(this);
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