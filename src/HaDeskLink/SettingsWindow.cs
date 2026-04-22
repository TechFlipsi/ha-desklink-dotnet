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

/// <summary>
/// Settings window for configuring HA connection and app options.
/// Modern layout with grouped sections.
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
    private ComboBox _hotkeyModBox = null!;
    private ComboBox _hotkeyKeyBox = null!;
    private Label _statusLabel = null!;
    private DataGridView _qaGrid = null!;

    // Colors
    private static readonly Color DarkBg = Color.FromArgb(32, 32, 32);
    private static readonly Color DarkFg = Color.FromArgb(230, 230, 230);
    private static readonly Color DarkInput = Color.FromArgb(48, 48, 48);
    private static readonly Color DarkBorder = Color.FromArgb(64, 64, 64);
    private static readonly Color DarkGroupBg = Color.FromArgb(38, 38, 38);
    private static readonly Color AccentBlue = Color.FromArgb(0, 120, 215);

    public SettingsWindow(Config config, Action onReconnect, HaApiClient? api = null)
    {
        _config = config;
        _onReconnect = onReconnect;
        _api = api;
        Text = $"HA DeskLink - {Localization.Get("settings_title")}";
        Size = new Size(560, 880);
        MinimumSize = new Size(500, 780);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        InitializeComponents();
        LoadSettings();
        ApplyTheme(_config.Theme);
    }

    private void InitializeComponents()
    {
        var scrollPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Padding = new Padding(16),
            Width = 520,
        };

        // === Connection Group ===
        var connGroup = CreateGroupBox("🔌 " + Localization.Get("settings_connection", "Connection"));
        var connPanel = new TableLayoutPanel { ColumnCount = 2, RowCount = 3, Dock = DockStyle.Fill, Padding = new Padding(8) };
        connPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        connPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        connPanel.Controls.Add(CreateLabel(Localization.Get("settings_ha_url")), 0, 0);
        _urlBox = new TextBox { Dock = DockStyle.Fill, Text = "https://homeassistant.local:8123" };
        connPanel.Controls.Add(_urlBox, 1, 0);

        connPanel.Controls.Add(CreateLabel(Localization.Get("settings_token")), 0, 1);
        _tokenBox = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
        connPanel.Controls.Add(_tokenBox, 1, 1);

        _sslCheck = new CheckBox { Text = Localization.Get("settings_verify_ssl"), AutoSize = true };
        connPanel.Controls.Add(_sslCheck, 0, 2);
        connPanel.SetColumnSpan(_sslCheck, 2);

        connGroup.Controls.Add(connPanel);
        layout.Controls.Add(connGroup);

        // === General Group ===
        var genGroup = CreateGroupBox("⚙️ " + Localization.Get("settings_general", "General"));
        var genPanel = new TableLayoutPanel { ColumnCount = 2, RowCount = 6, Dock = DockStyle.Fill, Padding = new Padding(8) };
        genPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        genPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _autostartCheck = new CheckBox { Text = Localization.Get("settings_autostart"), AutoSize = true };
        genPanel.Controls.Add(_autostartCheck, 0, 0);
        genPanel.SetColumnSpan(_autostartCheck, 2);

        genPanel.Controls.Add(CreateLabel(Localization.Get("settings_sensor_interval")), 0, 1);
        _intervalBox = new NumericUpDown { Minimum = 5, Maximum = 300, Value = 30, Dock = DockStyle.Fill };
        genPanel.Controls.Add(_intervalBox, 1, 1);

        genPanel.Controls.Add(CreateLabel(Localization.Get("settings_update_channel")), 0, 2);
        _updateChannelBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        _updateChannelBox.Items.AddRange(new object[] { Localization.Get("settings_channel_stable"), Localization.Get("settings_channel_prerelease") });
        genPanel.Controls.Add(_updateChannelBox, 1, 2);

        genPanel.Controls.Add(CreateLabel(Localization.Get("settings_language")), 0, 3);
        _languageBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var lang in Localization.AvailableLanguages)
            _languageBox.Items.Add($"{Localization.GetLanguageName(lang)} ({lang})");
        genPanel.Controls.Add(_languageBox, 1, 3);

        genPanel.Controls.Add(CreateLabel(Localization.Get("settings_theme")), 0, 4);
        _themeBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        _themeBox.Items.AddRange(new object[] { Localization.Get("settings_theme_system"), Localization.Get("settings_theme_light"), Localization.Get("settings_theme_dark") });
        genPanel.Controls.Add(_themeBox, 1, 4);

        genPanel.Controls.Add(CreateLabel(Localization.Get("settings_hotkey_modifiers")), 0, 5);
        var hotkeyPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        _hotkeyModBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
        _hotkeyModBox.Items.AddRange(new object[] { "Ctrl+Shift", "Ctrl+Alt", "Ctrl", "Alt", "Shift", Localization.Get("settings_hotkey_none") });
        hotkeyPanel.Controls.Add(_hotkeyModBox);
        hotkeyPanel.Controls.Add(new Label { Text = "+", AutoSize = true, Padding = new Padding(8, 6, 8, 0) });
        _hotkeyKeyBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
        _hotkeyKeyBox.Items.AddRange(new object[] { "H", "Q", "A", "S", "D", "F", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12", "Space" });
        hotkeyPanel.Controls.Add(_hotkeyKeyBox);
        genPanel.Controls.Add(hotkeyPanel, 1, 5);

        genGroup.Controls.Add(genPanel);
        layout.Controls.Add(genGroup);

        // === Actions Group ===
        var actionGroup = CreateGroupBox("🔧 " + Localization.Get("settings_actions", "Actions"));
        var actionPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Padding = new Padding(8) };
        var saveBtn = CreateButton(Localization.Get("settings_save"), AccentBlue);
        saveBtn.Click += OnSave;
        actionPanel.Controls.Add(saveBtn);
        var reconnectBtn = CreateButton(Localization.Get("settings_reconnect_msg").Replace("...", ""), Color.FromArgb(0, 100, 180));
        reconnectBtn.Click += OnReconnectClicked;
        actionPanel.Controls.Add(reconnectBtn);
        var resetDeviceBtn = CreateButton(Localization.Get("settings_reset_device"), Color.FromArgb(180, 80, 0));
        resetDeviceBtn.Click += OnResetDeviceId;
        actionPanel.Controls.Add(resetDeviceBtn);
        var reRegisterBtn = CreateButton(Localization.Get("settings_reregister_sensors"), Color.FromArgb(0, 130, 100));
        reRegisterBtn.Click += OnReRegisterSensors;
        actionPanel.Controls.Add(reRegisterBtn);
        actionGroup.Controls.Add(actionPanel);
        layout.Controls.Add(actionGroup);

        // === Quick Actions Group ===
        var qaGroup = CreateGroupBox("⚡ " + Localization.Get("settings_quickactions"));
        var qaInnerPanel = new TableLayoutPanel { ColumnCount = 1, RowCount = 4, Dock = DockStyle.Fill, Padding = new Padding(8) };

        var qaDesc = new Label { Text = Localization.Get("settings_quickactions_desc"), AutoSize = true, ForeColor = Color.Gray };
        qaInnerPanel.Controls.Add(qaDesc);

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
        _qaGrid.EditingControlShowing += OnGridEditingControlShowing;
        qaInnerPanel.Controls.Add(_qaGrid);

        var qaBtnPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        var loadEntitiesBtn = CreateButton(Localization.Get("settings_load_entities"), Color.FromArgb(0, 130, 100));
        loadEntitiesBtn.Click += OnLoadEntities;
        qaBtnPanel.Controls.Add(loadEntitiesBtn);
        var jsonEditBtn = CreateButton(Localization.Get("settings_edit_json"), Color.FromArgb(100, 100, 100));
        jsonEditBtn.Click += OnEditJson;
        qaBtnPanel.Controls.Add(jsonEditBtn);
        var saveQaBtn = CreateButton(Localization.Get("settings_save_quickactions"), AccentBlue);
        saveQaBtn.Click += OnSaveQuickActions;
        qaBtnPanel.Controls.Add(saveQaBtn);
        qaInnerPanel.Controls.Add(qaBtnPanel);

        qaGroup.Controls.Add(qaInnerPanel);
        layout.Controls.Add(qaGroup);

        // === Status ===
        _statusLabel = new Label { Text = Localization.Get("settings_saved"), ForeColor = Color.Gray, AutoSize = true, Padding = new Padding(0, 4, 0, 8) };
        layout.Controls.Add(_statusLabel);

        scrollPanel.Controls.Add(layout);
        Controls.Add(scrollPanel);
    }

    private GroupBox CreateGroupBox(string title)
    {
        return new GroupBox
        {
            Text = title,
            Dock = DockStyle.Top,
            Width = 510,
            Margin = new Padding(0, 0, 0, 8),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
        };
    }

    private static Label CreateLabel(string text)
    {
        return new Label { Text = text, Anchor = AnchorStyles.Left, AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
    }

    private static Button CreateButton(string text, Color color)
    {
        return new Button
        {
            Text = text,
            Size = new Size(155, 36),
            BackColor = color,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f),
            Margin = new Padding(2),
        };
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
                else if (c is GroupBox gb)
                {
                    gb.ForeColor = DarkFg;
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
                else if (c is GroupBox gb)
                {
                    gb.ForeColor = SystemColors.WindowText;
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

        // Hotkey
        _hotkeyModBox.SelectedIndex = _config.HotkeyModifiers switch
        {
            "ctrl_shift" => 0,
            "ctrl_alt" => 1,
            "ctrl" => 2,
            "alt" => 3,
            "shift" => 4,
            "none" => 5,
            _ => 0
        };
        var keyIndex = _hotkeyKeyBox.Items.IndexOf(_config.HotkeyKey.ToUpper());
        _hotkeyKeyBox.SelectedIndex = keyIndex >= 0 ? keyIndex : 0;

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

        // Hotkey
        _config.HotkeyModifiers = _hotkeyModBox.SelectedIndex switch
        {
            0 => "ctrl_shift",
            1 => "ctrl_alt",
            2 => "ctrl",
            3 => "alt",
            4 => "shift",
            5 => "none",
            _ => "ctrl_shift"
        };
        _config.HotkeyKey = _hotkeyKeyBox.SelectedItem?.ToString() ?? "H";

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

    private List<(string entityId, string friendlyName)> _entities = new();
    private AutoCompleteStringCollection _entityAutoComplete = new();

    private async void OnLoadEntities(object? sender, EventArgs e)
    {
        if (_api == null)
        {
            _statusLabel.Text = Localization.Get("settings_no_connection");
            return;
        }

        _statusLabel.Text = Localization.Get("settings_loading_entities");
        try
        {
            _entities = await _api.GetEntitiesAsync();
            _entityAutoComplete = new AutoCompleteStringCollection();
            _entityAutoComplete.AddRange(_entities.Select(e => e.entityId).ToArray());
            _statusLabel.Text = $"{Localization.Get("settings_entities_loaded")} ({_entities.Count})";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"{Localization.Get("settings_entities_failed")}: {ex.Message}";
        }
    }

    private void OnGridEditingControlShowing(object? sender, DataGridViewEditingControlShowingEventArgs e)
    {
        if (_qaGrid.CurrentCell.ColumnIndex == 0 && e.Control is TextBox tb)
        {
            tb.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            tb.AutoCompleteSource = AutoCompleteSource.CustomSource;
            tb.AutoCompleteCustomSource = _entityAutoComplete;
        }
        else if (e.Control is TextBox tb2)
        {
            tb2.AutoCompleteMode = AutoCompleteMode.None;
            tb2.AutoCompleteCustomSource = new AutoCompleteStringCollection();
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
        // Build current JSON from grid
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

        // Show JSON editor dialog
        var dialog = new Form
        {
            Text = "Quick Actions JSON",
            Size = new Size(500, 400),
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
        dialog.Controls.Add(jsonBox);

        // Apply theme to dialog
        bool dark = _config.Theme == "dark" || (_config.Theme == "system" && IsSystemDark());
        if (dark)
        {
            dialog.BackColor = DarkBg;
            jsonBox.BackColor = DarkInput;
            jsonBox.ForeColor = DarkFg;
        }

        var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 45 };
        var cancelBtn = new Button { Text = Localization.Get("settings_cancel"), Size = new Size(80, 35) };
        cancelBtn.Click += (s, ev) => dialog.Close();
        btnPanel.Controls.Add(cancelBtn);
        var applyBtn = new Button { Text = Localization.Get("settings_save"), Size = new Size(80, 35) };
        applyBtn.Click += (s, ev) =>
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<QuickAction>>(jsonBox.Text);
                if (parsed != null)
                {
                    // Update grid from JSON
                    _qaGrid.Rows.Clear();
                    foreach (var action in parsed)
                    {
                        if (!string.IsNullOrEmpty(action.EntityId) && !string.IsNullOrEmpty(action.Name))
                            _qaGrid.Rows.Add(action.EntityId, action.Name);
                    }
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