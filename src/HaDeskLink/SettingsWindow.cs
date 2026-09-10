// HA DeskLink - Home Assistant Companion App
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
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
    // ═══ Config und API ═══
    private readonly Config _config;
    private readonly Action _onReconnect;
    private readonly HaApiClient? _api;

    // ═══ Steuerlemente — Verbindung ═══
    private TextBox _urlBox = null!;
    private TextBox _tokenBox = null!;
    private CheckBox _sslCheck = null!;

    // ═══ Steuerlemente — Allgemein ═══
    private CheckBox _autostartCheck = null!;
    private NumericUpDown _intervalBox = null!;
    private ComboBox _updateChannelBox = null!;

    // ═══ Steuerlemente — Erscheinungsbild ═══
    private ComboBox _languageBox = null!;
    private ComboBox _themeBox = null!;

    // ═══ Steuerlemente — Benachrichtigungen ═══
    private ComboBox _notifPosBox = null!;
    private ComboBox _notifMonitorBox = null!;

    // ═══ Steuerlemente — Tastenkombinationen ═══
    private ComboBox _hotkeyModBox = null!;
    private ComboBox _hotkeyKeyBox = null!;
    private ComboBox _hotkeyDashModBox = null!;
    private ComboBox _hotkeyDashKeyBox = null!;
    private ComboBox _hotkeySettingsModBox = null!;
    private ComboBox _hotkeySettingsKeyBox = null!;

    // ═══ Steuerlemente — Status und Quick Actions ═══
    private Label _statusLabel = null!;
    private ListBox _qaList = null!;
    private List<(string entityId, string friendlyName)> _entities = new();

    // ═══ Steuerlemente — Desktop-Widgets ═══
    private ListBox _widgetList = null!;
    private Button _widgetTestBtn = null!;
    private Label _widgetDescLabel = null!;

    // ═══ MQTT-Steuerlemente ═══
    private CheckBox _mqttEnabledCheck = null!;
    private TextBox _mqttBrokerBox = null!;
    private TextBox _mqttPortBox = null!;
    private TextBox _mqttUserBox = null!;
    private TextBox _mqttPassBox = null!;
    private CheckBox _mqttSslCheck = null!;
    private TextBox _mqttFallbackBox = null!;
    private Label _mqttStatusLabel = null!;

    // ═══ Steuerlemente — Music-Assistant ═══
    private TextBox _maHostBox = null!;
    private TextBox _maPortBox = null!;
    private TextBox _maTokenBox = null!;
    private Label _maTestResultLabel = null!;
    private Button _maTestBtn = null!;

    // ═══ Steuerlemente — Streaming (Sendspin) ═══
    private CheckBox _streamEnabledCheck = null!;
    private TextBox _streamPlayerNameBox = null!;
    private Label _streamStatusLabel = null!;

    // ═══ Layout-Panels für Navigation und Theme ═══
    private Panel _sidebarPanel = null!;
    private Panel _contentPanel = null!;
    private Panel _bottomPanel = null!;
    private readonly List<Button> _sidebarButtons = new();
    private readonly List<Panel> _sectionPanels = new();
    private int _currentSection = 0;
    private bool _isDark = true;
    private Color _sidebarNormalBg;
    private Color _sidebarHoverBg;

    // ═══ Dark Theme Farben ═══
    private static readonly Color DarkBg = Color.FromArgb(32, 32, 32);
    private static readonly Color DarkFg = Color.FromArgb(230, 230, 230);
    private static readonly Color DarkInput = Color.FromArgb(48, 48, 48);
    private static readonly Color DarkSectionBg = Color.FromArgb(40, 40, 40);
    private static readonly Color AccentBlue = Color.FromArgb(0, 120, 215);
    private static readonly Color SuccessGreen = Color.FromArgb(0, 134, 100);
    private static readonly Color WarningOrange = Color.FromArgb(180, 80, 0);
    private static readonly Color DangerRed = Color.FromArgb(200, 50, 50);

    // ═══ Light Theme Farben ═══
    private static readonly Color LightBg = Color.White;
    private static readonly Color LightFg = Color.FromArgb(32, 32, 32);
    private static readonly Color LightInput = Color.FromArgb(248, 248, 248);
    private static readonly Color LightSidebarBg = Color.FromArgb(240, 240, 240);
    private static readonly Color LightBottomBg = Color.FromArgb(248, 248, 248);

    public SettingsWindow(Config config, Action onReconnect, HaApiClient? api = null)
    {
        _config = config;
        _onReconnect = onReconnect;
        _api = api;
        Text = $"HA DeskLink - {Localization.Get("settings_title")}";
        Size = new Size(800, 600);
        MinimumSize = new Size(600, 400);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        InitializeComponents();
        LoadSettings();
        LoadQuickActionsList();
        LoadWidgetsList();
        ApplyTheme(_config.Theme);
        ShowSection(0);
    }

    // ═══════════════════════════════════════════════════════
    // INITIALISIERUNG — Sidebar (Dock=Left) + Inhaltsbereich (Dock=Fill)
    // ═══════════════════════════════════════════════════════

    private void InitializeComponents()
    {
        // Sidebar (Panel1, links, 200px breit) — Dock=Left, NICHT SplitContainer
        // SplitContainer crasht weil SplitterDistance-Validierung zur Laufzeit fehlschlägt
        BuildSidebar();
        _sidebarPanel.Dock = DockStyle.Left;
        _sidebarPanel.Width = 200;

        // Inhaltsbereich (rechts, füllt restlichen Platz)
        _contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0),
        };

        // Sections erstellen — jede ist ein Panel mit Dock=Fill und AutoScroll
        _sectionPanels.Add(BuildConnectionSection());
        _sectionPanels.Add(BuildGeneralSection());
        _sectionPanels.Add(BuildAppearanceSection());
        _sectionPanels.Add(BuildNotificationsSection());
        _sectionPanels.Add(BuildHotkeysSection());
        _sectionPanels.Add(BuildMqttSection());
        _sectionPanels.Add(BuildMusicAssistantSection());
        _sectionPanels.Add(BuildStreamingSection());
        _sectionPanels.Add(BuildQuickActionsSection());
        _sectionPanels.Add(BuildWidgetsSection());

        // Sections zum Inhaltsbereich hinzufügen (alle unsichtbar, ShowSection() blendet eine ein)
        foreach (var section in _sectionPanels)
        {
            section.Visible = false;
            _contentPanel.Controls.Add(section);
        }

        // Reihenfolge WICHTIG: ContentPanel zuerst (Dock=Fill), dann Sidebar (Dock=Left)
        // WinForms dockt in umgekehrter Reihenfolge — Fill muss zuerst hinzugefügt werden
        Controls.Add(_contentPanel);
        Controls.Add(_sidebarPanel);

        // Bottom Bar — Save, Reconnect, Status (immer sichtbar unten)
        BuildBottomBar();
    }

    // ═══════════════════════════════════════════════════════
    // SIDEBAR — Navigation mit 7 Items
    // ═══════════════════════════════════════════════════════

    private void BuildSidebar()
    {
        _sidebarPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = DarkBg,
        };

        // Sidebar-Header (Branding)
        var sidebarHeader = new Label
        {
            Text = "HA DeskLink",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = DarkFg,
            Dock = DockStyle.Top,
            Height = 48,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(16, 0, 16, 8),
        };

        // Navigation-Items definieren
        var navItems = new[]
        {
            (0, "🔌 " + Localization.Get("settings_connection", "Verbindung")),
            (1, "⚙️ " + Localization.Get("settings_general", "Allgemein")),
            (2, "🎨 " + Localization.Get("settings_appearance", "Erscheinungsbild")),
            (3, "🔔 " + Localization.Get("settings_notifications", "Benachrichtigungen")),
            (4, "⌨️ " + Localization.Get("settings_hotkeys", "Tastenkombinationen")),
            (5, "📡 " + Localization.Get("mqtt_settings")),
            (6, "🎵 " + Localization.Get("ma_settings", "Music Assistant")),
            (7, "🔊 " + Localization.Get("stream_settings", "Streaming")),
            (8, "⚡ " + Localization.Get("settings_quickactions")),
            (9, "🖥️ " + Localization.Get("ma_widgets", "Desktop-Widgets")),
        };

        // Buttons erstellen (in korrekter Reihenfolge für _sidebarButtons Liste)
        for (int i = 0; i < navItems.Length; i++)
        {
            var btn = MakeSidebarButton(navItems[i].Item2, navItems[i].Item1);
            _sidebarButtons.Add(btn);
        }

        // Buttons in umgekehrter Reihenfolge zum Panel hinzufügen
        // (Dock=Top: zuletzt hinzugefügter Control erscheint ganz oben)
        for (int i = _sidebarButtons.Count - 1; i >= 0; i--)
        {
            _sidebarPanel.Controls.Add(_sidebarButtons[i]);
        }

        // Header zuletzt hinzufügen (erscheint ganz oben in der Sidebar)
        _sidebarPanel.Controls.Add(sidebarHeader);
    }

    private Button MakeSidebarButton(string text, int index)
    {
        var btn = new Button
        {
            Text = "  " + text,
            Dock = DockStyle.Top,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 10f),
            ForeColor = DarkFg,
            BackColor = DarkBg,
            Cursor = Cursors.Hand,
            Tag = index,
            Padding = new Padding(16, 0, 0, 0),
            Margin = new Padding(0),
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += (s, e) => ShowSection(index);
        // Hover-Effekt: leicht hellerer Hintergrund (nur bei nicht ausgewähltem Item)
        btn.MouseEnter += (s, e) =>
        {
            if (index != _currentSection)
                btn.BackColor = _sidebarHoverBg;
        };
        btn.MouseLeave += (s, e) =>
        {
            if (index != _currentSection)
                btn.BackColor = _sidebarNormalBg;
        };
        return btn;
    }

    private void ShowSection(int index)
    {
        // Alle Sections ausblenden und Scroll-Position zurücksetzen
        foreach (var section in _sectionPanels)
        {
            section.Visible = false;
            section.AutoScrollPosition = new Point(0, 0);
        }

        // Ausgewählte Section anzeigen und nach vorne bringen
        if (index >= 0 && index < _sectionPanels.Count)
        {
            _sectionPanels[index].Visible = true;
            _sectionPanels[index].BringToFront();
        }

        // Sidebar-Buttons aktualisieren (ausgewählter = AccentBlue, weiße Schrift)
        for (int i = 0; i < _sidebarButtons.Count; i++)
        {
            if (i == index)
            {
                _sidebarButtons[i].BackColor = AccentBlue;
                _sidebarButtons[i].ForeColor = Color.White;
            }
            else
            {
                _sidebarButtons[i].BackColor = _sidebarNormalBg;
                _sidebarButtons[i].ForeColor = _isDark ? DarkFg : LightFg;
            }
        }

        _currentSection = index;
    }

    // ═══════════════════════════════════════════════════════
    // SECTION-BUILDER — Jede Section ist ein Panel mit Dock=Fill und AutoScroll
    // ═══════════════════════════════════════════════════════

    // ─── Helper: Section-Panel (Dock=Fill, AutoScroll, 16px Padding) ───
    private static Panel MakeSectionPanel()
    {
        return new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(16),
        };
    }

    // ─── Helper: TableLayoutPanel für 2-Spalten Layout (Label 200px + Input Percent 100%) ───
    private static TableLayoutPanel MakeFieldTable(int rowCount)
    {
        var t = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = rowCount,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(0),
            Margin = new Padding(0, 8, 0, 0),
        };
        // 200px Label-Spalte — breit genug für deutsche Langwörter ohne Wortumbruch
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return t;
    }

    // ─── Section 1: 🔌 Verbindung ───
    private Panel BuildConnectionSection()
    {
        var section = MakeSectionPanel();
        var header = MakeSectionHeader("🔌 " + Localization.Get("settings_connection", "Verbindung"));

        // 6 Zeilen: URL | URL-Beschreibung | Token | Token-Beschreibung | SSL | SSL-Beschreibung
        var table = MakeFieldTable(6);

        // HA URL (TextBox, volle Breite)
        _urlBox = new TextBox { Dock = DockStyle.Fill, Text = "https://homeassistant.local:8123", Height = 28 };
        AddTooltip(_urlBox, Localization.Get("tooltip_ha_url"));
        table.Controls.Add(MakeLabel(Localization.Get("settings_ha_url")), 0, 0);
        table.Controls.Add(_urlBox, 1, 0);

        // Beschreibung: HA URL
        var urlDesc = MakeDescriptionLabel(Localization.Get("desc_ha_url"));
        table.Controls.Add(urlDesc, 0, 1);
        table.SetColumnSpan(urlDesc, 2);

        // Long-Lived Token (TextBox, password, volle Breite)
        _tokenBox = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true, Height = 28 };
        AddTooltip(_tokenBox, Localization.Get("tooltip_token"));
        table.Controls.Add(MakeLabel(Localization.Get("settings_token")), 0, 2);
        table.Controls.Add(_tokenBox, 1, 2);

        // Beschreibung: Token
        var tokenDesc = MakeDescriptionLabel(Localization.Get("desc_token"));
        table.Controls.Add(tokenDesc, 0, 3);
        table.SetColumnSpan(tokenDesc, 2);

        // SSL-Zertifikat prüfen (CheckBox, volle Breite)
        _sslCheck = new CheckBox { Text = Localization.Get("settings_verify_ssl"), AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
        AddTooltip(_sslCheck, Localization.Get("tooltip_ssl"));
        table.Controls.Add(_sslCheck, 0, 4);
        table.SetColumnSpan(_sslCheck, 2);

        // Beschreibung: SSL
        var sslDesc = MakeDescriptionLabel(Localization.Get("desc_ssl"));
        table.Controls.Add(sslDesc, 0, 5);
        table.SetColumnSpan(sslDesc, 2);

        // Neu verbinden Button (separate Zeile unter der Tabelle)
        var actionPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(0),
            Margin = new Padding(0, 12, 0, 0),
        };
        var reconnectBtn = MakeButton("🔄 " + Localization.Get("settings_reconnect", "Neu verbinden"), Color.FromArgb(0, 100, 180), OnReconnectClicked);
        AddTooltip(reconnectBtn, Localization.Get("tooltip_reconnect"));
        actionPanel.Controls.Add(reconnectBtn);

        // In umgekehrter Reihenfolge hinzufügen (Dock=Top: zuletzt hinzugefügter oben)
        section.Controls.Add(actionPanel);
        section.Controls.Add(table);
        section.Controls.Add(header);

        return section;
    }

    // ─── Section 2: ⚙️ Allgemein (Autostart, Sensor-Intervall, Update-Kanal, Reset/Reregister) ───
    private Panel BuildGeneralSection()
    {
        var section = MakeSectionPanel();
        var header = MakeSectionHeader("⚙️ " + Localization.Get("settings_general", "Allgemein"));

        // 9 Zeilen: Autostart | Autostart-Beschreibung | Sensor-Intervall | Hint |
        //           Sensor-Beschreibung | Update-Kanal | Update-Kanal-Beschreibung |
        //           Reset-Beschreibung | Reregister-Beschreibung
        var table = MakeFieldTable(9);

        // Autostart
        _autostartCheck = new CheckBox { Text = Localization.Get("settings_autostart"), AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
        AddTooltip(_autostartCheck, Localization.Get("tooltip_autostart"));
        table.Controls.Add(_autostartCheck, 0, 0);
        table.SetColumnSpan(_autostartCheck, 2);

        // Beschreibung: Autostart
        var autostartDesc = MakeDescriptionLabel(Localization.Get("desc_autostart"));
        table.Controls.Add(autostartDesc, 0, 1);
        table.SetColumnSpan(autostartDesc, 2);

        // Sensor-Intervall
        table.Controls.Add(MakeLabel(Localization.Get("settings_sensor_interval")), 0, 2);
        _intervalBox = new NumericUpDown { Minimum = 10, Maximum = 300, Value = 30, Dock = DockStyle.Fill, Height = 28 };
        AddTooltip(_intervalBox, Localization.Get("tooltip_sensor_interval"));
        var intervalHint = new Label
        {
            Text = Localization.Get("sensors_interval_hint"),
            AutoSize = true,
            ForeColor = Color.Gray,
            Font = new Font("Segoe UI", 8f),
            Margin = new Padding(0, 2, 0, 0),
        };
        table.Controls.Add(_intervalBox, 1, 2);
        table.Controls.Add(intervalHint, 0, 3);
        table.SetColumnSpan(intervalHint, 2);

        // Beschreibung: Sensor-Intervall (detaillierter als der Hint)
        var intervalDesc = MakeDescriptionLabel(Localization.Get("desc_sensor_interval"));
        table.Controls.Add(intervalDesc, 0, 4);
        table.SetColumnSpan(intervalDesc, 2);

        // Update-Kanal
        table.Controls.Add(MakeLabel(Localization.Get("settings_update_channel")), 0, 5);
        _updateChannelBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Height = 28 };
        AddTooltip(_updateChannelBox, Localization.Get("tooltip_update_channel"));
        _updateChannelBox.Items.AddRange(new object[] { Localization.Get("settings_channel_stable"), Localization.Get("settings_channel_prerelease") });
        table.Controls.Add(_updateChannelBox, 1, 5);

        // Beschreibung: Update-Kanal
        var updateChannelDesc = MakeDescriptionLabel(Localization.Get("desc_update_channel"));
        table.Controls.Add(updateChannelDesc, 0, 6);
        table.SetColumnSpan(updateChannelDesc, 2);

        // Beschreibung: Reset Device ID (Erklärung vor den Buttons)
        var resetDesc = MakeDescriptionLabel(Localization.Get("desc_reset_device"));
        table.Controls.Add(resetDesc, 0, 7);
        table.SetColumnSpan(resetDesc, 2);

        // Beschreibung: Sensoren neu registrieren
        var reregisterDesc = MakeDescriptionLabel(Localization.Get("desc_reregister"));
        table.Controls.Add(reregisterDesc, 0, 8);
        table.SetColumnSpan(reregisterDesc, 2);

        // Reset Device ID und Re-register Sensors
        var actionPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(0),
            Margin = new Padding(0, 12, 0, 0),
        };

        var resetBtn = MakeButton("🔑 " + Localization.Get("settings_reset_device", "Geräte-ID zurücksetzen"), WarningOrange, OnResetDeviceId);
        AddTooltip(resetBtn, Localization.Get("tooltip_reset_device"));

        var reregisterBtn = MakeButton("📊 " + Localization.Get("settings_reregister_sensors", "Sensoren neu registrieren"), SuccessGreen, OnReRegisterSensors);
        AddTooltip(reregisterBtn, Localization.Get("tooltip_reregister"));

        actionPanel.Controls.Add(resetBtn);
        actionPanel.Controls.Add(reregisterBtn);

        // In umgekehrter Reihenfolge hinzufügen
        section.Controls.Add(actionPanel);
        section.Controls.Add(table);
        section.Controls.Add(header);

        return section;
    }

    // ─── Section 3: 🎨 Erscheinungsbild (Sprache, Theme) ───
    private Panel BuildAppearanceSection()
    {
        var section = MakeSectionPanel();
        var header = MakeSectionHeader("🎨 " + Localization.Get("settings_appearance", "Erscheinungsbild"));

        // 4 Zeilen: Sprache | Sprache-Beschreibung | Design | Design-Beschreibung
        var table = MakeFieldTable(4);

        // Sprache (ComboBox, zeigt alle AvailableLanguages)
        table.Controls.Add(MakeLabel(Localization.Get("settings_language")), 0, 0);
        _languageBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Height = 28 };
        foreach (var lang in Localization.AvailableLanguages)
            _languageBox.Items.Add($"{Localization.GetLanguageName(lang)} ({lang})");
        table.Controls.Add(_languageBox, 1, 0);

        // Beschreibung: Sprache
        var langDesc = MakeDescriptionLabel(Localization.Get("desc_language"));
        table.Controls.Add(langDesc, 0, 1);
        table.SetColumnSpan(langDesc, 2);

        // Design (ComboBox: System/Hell/Dunkel)
        table.Controls.Add(MakeLabel(Localization.Get("settings_theme")), 0, 2);
        _themeBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Height = 28 };
        AddTooltip(_themeBox, Localization.Get("tooltip_theme"));
        _themeBox.Items.AddRange(new object[] { Localization.Get("settings_theme_system"), Localization.Get("settings_theme_light"), Localization.Get("settings_theme_dark") });
        table.Controls.Add(_themeBox, 1, 2);

        // Beschreibung: Design
        var themeDesc = MakeDescriptionLabel(Localization.Get("desc_theme"));
        table.Controls.Add(themeDesc, 0, 3);
        table.SetColumnSpan(themeDesc, 2);

        // In umgekehrter Reihenfolge hinzufügen
        section.Controls.Add(table);
        section.Controls.Add(header);

        return section;
    }

    // ─── Section 4: 🔔 Benachrichtigungen (Position, Monitor) ───
    private Panel BuildNotificationsSection()
    {
        var section = MakeSectionPanel();
        var header = MakeSectionHeader("🔔 " + Localization.Get("settings_notifications", "Benachrichtigungen"));

        // 4 Zeilen: Position | Position-Beschreibung | Monitor | Monitor-Beschreibung
        var table = MakeFieldTable(4);

        // Position (ComboBox: Unten Links, Unten Rechts, Oben Links, Oben Rechts)
        table.Controls.Add(MakeLabel(Localization.Get("settings_notif_position")), 0, 0);
        _notifPosBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Height = 28 };
        AddTooltip(_notifPosBox, Localization.Get("tooltip_notif_position"));
        _notifPosBox.Items.AddRange(new object[] {
            Localization.Get("settings_notif_bottom_left"),
            Localization.Get("settings_notif_bottom_right"),
            Localization.Get("settings_notif_top_left"),
            Localization.Get("settings_notif_top_right")
        });
        table.Controls.Add(_notifPosBox, 1, 0);

        // Beschreibung: Position
        var posDesc = MakeDescriptionLabel(Localization.Get("desc_notif_position"));
        table.Controls.Add(posDesc, 0, 1);
        table.SetColumnSpan(posDesc, 2);

        // Monitor (ComboBox: alle Screens)
        table.Controls.Add(MakeLabel(Localization.Get("settings_notif_monitor")), 0, 2);
        _notifMonitorBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Height = 28 };
        AddTooltip(_notifMonitorBox, Localization.Get("tooltip_notif_monitor"));
        for (int i = 0; i < Screen.AllScreens.Length; i++)
        {
            var label = i == 0
                ? $"{Localization.Get("settings_notif_primary_monitor")} ({Screen.AllScreens[i].DeviceName?.Trim(':')})"
                : $"Monitor {i + 1} ({Screen.AllScreens[i].DeviceName?.Trim(':')})";
            _notifMonitorBox.Items.Add(label);
        }
        table.Controls.Add(_notifMonitorBox, 1, 2);

        // Beschreibung: Monitor
        var monitorDesc = MakeDescriptionLabel(Localization.Get("desc_notif_monitor"));
        table.Controls.Add(monitorDesc, 0, 3);
        table.SetColumnSpan(monitorDesc, 2);

        // In umgekehrter Reihenfolge hinzufügen
        section.Controls.Add(table);
        section.Controls.Add(header);

        return section;
    }

    // ─── Section 5: ⌨️ Tastenkombinationen (3 Hotkey Rows) ───
    private Panel BuildHotkeysSection()
    {
        var section = MakeSectionPanel();
        var header = MakeSectionHeader("⌨️ " + Localization.Get("settings_hotkeys", "Tastenkombinationen"));

        // 6 Zeilen: QA-Hotkey | QA-Beschreibung | Dash-Hotkey | Dash-Beschreibung | Settings-Hotkey | Settings-Beschreibung
        var table = MakeFieldTable(6);

        // Quick Actions Hotkey
        table.Controls.Add(MakeLabel(Localization.Get("settings_hotkey_qa")), 0, 0);
        var hotkeyPanel = CreateHotkeyRow(out _hotkeyModBox, out _hotkeyKeyBox);
        AddTooltip(_hotkeyModBox, Localization.Get("tooltip_hotkey_qa"));
        table.Controls.Add(hotkeyPanel, 1, 0);

        // Beschreibung: Quick Actions Hotkey
        var qaHotkeyDesc = MakeDescriptionLabel(Localization.Get("desc_hotkey_qa"));
        table.Controls.Add(qaHotkeyDesc, 0, 1);
        table.SetColumnSpan(qaHotkeyDesc, 2);

        // Dashboard Hotkey
        table.Controls.Add(MakeLabel(Localization.Get("settings_hotkey_dashboard")), 0, 2);
        var dashPanel = CreateHotkeyRow(out _hotkeyDashModBox, out _hotkeyDashKeyBox);
        AddTooltip(dashPanel, Localization.Get("tooltip_hotkey_dashboard"));
        table.Controls.Add(dashPanel, 1, 2);

        // Beschreibung: Dashboard Hotkey
        var dashHotkeyDesc = MakeDescriptionLabel(Localization.Get("desc_hotkey_dashboard"));
        table.Controls.Add(dashHotkeyDesc, 0, 3);
        table.SetColumnSpan(dashHotkeyDesc, 2);

        // Settings Hotkey
        table.Controls.Add(MakeLabel(Localization.Get("settings_hotkey_settings")), 0, 4);
        var settingsPanel = CreateHotkeyRow(out _hotkeySettingsModBox, out _hotkeySettingsKeyBox);
        AddTooltip(settingsPanel, Localization.Get("tooltip_hotkey_settings"));
        table.Controls.Add(settingsPanel, 1, 4);

        // Beschreibung: Settings Hotkey
        var settingsHotkeyDesc = MakeDescriptionLabel(Localization.Get("desc_hotkey_settings"));
        table.Controls.Add(settingsHotkeyDesc, 0, 5);
        table.SetColumnSpan(settingsHotkeyDesc, 2);

        // In umgekehrter Reihenfolge hinzufügen
        section.Controls.Add(table);
        section.Controls.Add(header);

        return section;
    }

    // ─── Section 6: 📡 MQTT ───
    private Panel BuildMqttSection()
    {
        var section = MakeSectionPanel();
        var header = MakeSectionHeader("📡 " + Localization.Get("mqtt_settings"));

        // 16 Zeilen: Enable | Enable-Beschreibung | Broker | Broker-Beschreibung |
        //            Port | Port-Beschreibung | User | User-Beschreibung |
        //            Pass | Pass-Beschreibung | SSL | SSL-Beschreibung |
        //            Fallback | Fallback-Beschreibung | Test | Status
        var table = MakeFieldTable(16);

        // MQTT aktivieren
        _mqttEnabledCheck = new CheckBox { Text = Localization.Get("mqtt_enabled"), AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
        AddTooltip(_mqttEnabledCheck, "MQTT für Echtzeit-Mediensteuerung und schnelle Sensor-Updates aktivieren");
        table.Controls.Add(_mqttEnabledCheck, 0, 0);
        table.SetColumnSpan(_mqttEnabledCheck, 2);

        // Beschreibung: MQTT aktivieren
        var mqttEnableDesc = MakeDescriptionLabel(Localization.Get("desc_mqtt_enabled"));
        table.Controls.Add(mqttEnableDesc, 0, 1);
        table.SetColumnSpan(mqttEnableDesc, 2);

        // Broker
        table.Controls.Add(MakeLabel(Localization.Get("mqtt_broker")), 0, 2);
        _mqttBrokerBox = new TextBox { Dock = DockStyle.Fill, Height = 28 };
        AddTooltip(_mqttBrokerBox, "MQTT-Broker Hostname (z.B. homeassistant.local)");
        table.Controls.Add(_mqttBrokerBox, 1, 2);

        // Beschreibung: Broker
        var brokerDesc = MakeDescriptionLabel(Localization.Get("desc_mqtt_broker"));
        table.Controls.Add(brokerDesc, 0, 3);
        table.SetColumnSpan(brokerDesc, 2);

        // Port
        table.Controls.Add(MakeLabel(Localization.Get("mqtt_port")), 0, 4);
        _mqttPortBox = new TextBox { Dock = DockStyle.Fill, Text = "1883", Height = 28 };
        AddTooltip(_mqttPortBox, "MQTT-Broker Port (Standard: 1883, SSL: 8883)");
        table.Controls.Add(_mqttPortBox, 1, 4);

        // Beschreibung: Port
        var portDesc = MakeDescriptionLabel(Localization.Get("desc_mqtt_port"));
        table.Controls.Add(portDesc, 0, 5);
        table.SetColumnSpan(portDesc, 2);

        // Username
        table.Controls.Add(MakeLabel(Localization.Get("mqtt_username")), 0, 6);
        _mqttUserBox = new TextBox { Dock = DockStyle.Fill, Height = 28 };
        AddTooltip(_mqttUserBox, "MQTT-Benutzername (optional, leer lassen bei anonymem Zugang)");
        table.Controls.Add(_mqttUserBox, 1, 6);

        // Beschreibung: Username
        var userDesc = MakeDescriptionLabel(Localization.Get("desc_mqtt_username"));
        table.Controls.Add(userDesc, 0, 7);
        table.SetColumnSpan(userDesc, 2);

        // Password
        table.Controls.Add(MakeLabel(Localization.Get("mqtt_password")), 0, 8);
        _mqttPassBox = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true, Height = 28 };
        AddTooltip(_mqttPassBox, "MQTT-Passwort (optional)");
        table.Controls.Add(_mqttPassBox, 1, 8);

        // Beschreibung: Password
        var passDesc = MakeDescriptionLabel(Localization.Get("desc_mqtt_password"));
        table.Controls.Add(passDesc, 0, 9);
        table.SetColumnSpan(passDesc, 2);

        // SSL
        _mqttSslCheck = new CheckBox { Text = Localization.Get("mqtt_use_ssl"), AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
        AddTooltip(_mqttSslCheck, "SSL/TLS für MQTT-Verbindung aktivieren");
        table.Controls.Add(_mqttSslCheck, 0, 10);
        table.SetColumnSpan(_mqttSslCheck, 2);

        // Beschreibung: SSL
        var mqttSslDesc = MakeDescriptionLabel(Localization.Get("desc_mqtt_ssl"));
        table.Controls.Add(mqttSslDesc, 0, 11);
        table.SetColumnSpan(mqttSslDesc, 2);

        // Fallback-Adresse
        table.Controls.Add(MakeLabel(Localization.Get("mqtt_fallback_address", "Fallback-Adresse")), 0, 12);
        _mqttFallbackBox = new TextBox { Dock = DockStyle.Fill, Height = 28 };
        AddTooltip(_mqttFallbackBox, "Alternative MQTT-Broker-Adresse (z.B. lokale IP), falls die Hauptadresse nicht erreichbar ist. Leer lassen für keinen Fallback.");
        table.Controls.Add(_mqttFallbackBox, 1, 12);

        // Beschreibung: Fallback
        var fallbackDesc = MakeDescriptionLabel(Localization.Get("desc_mqtt_fallback"));
        table.Controls.Add(fallbackDesc, 0, 13);
        table.SetColumnSpan(fallbackDesc, 2);

        // Verbindung testen Button
        table.Controls.Add(MakeLabel(Localization.Get("mqtt_test_connection", "Verbindung testen")), 0, 14);
        var mqttTestBtn = MakeButton("🔌 " + Localization.Get("mqtt_test_connection", "Verbindung testen"), SuccessGreen, OnMqttTestConnection);
        AddTooltip(mqttTestBtn, "Verbindung zum MQTT-Broker testen, bevor gespeichert wird");
        table.Controls.Add(mqttTestBtn, 1, 14);

        // Status Label
        _mqttStatusLabel = new Label
        {
            Text = "",
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.Gray,
            AutoSize = true,
            Margin = new Padding(0, 6, 0, 0),
        };
        table.Controls.Add(_mqttStatusLabel, 0, 15);
        table.SetColumnSpan(_mqttStatusLabel, 2);

        _mqttEnabledCheck.CheckedChanged += (s, e) => UpdateMqttStatusLabel();

        // In umgekehrter Reihenfolge hinzufügen
        section.Controls.Add(table);
        section.Controls.Add(header);

        return section;
    }

    // ─── Section 7: 🎵 Music Assistant ───
    private Panel BuildMusicAssistantSection()
    {
        var section = MakeSectionPanel();
        var header = MakeSectionHeader("🎵 " + Localization.Get("ma_settings", "Music Assistant"));

        // 9 Zeilen: Host | Host-Beschreibung | Port | Port-Beschreibung |
        //          Token | Token-Beschreibung | Test | Status
        var table = MakeFieldTable(8);

        // MA Host
        table.Controls.Add(MakeLabel(Localization.Get("ma_host", "MA-Host")), 0, 0);
        _maHostBox = new TextBox { Dock = DockStyle.Fill, Height = 28 };
        AddTooltip(_maHostBox, Localization.Get("ma_host_tooltip"));
        table.Controls.Add(_maHostBox, 1, 0);

        // Beschreibung: MA Host
        var maHostDesc = MakeDescriptionLabel(Localization.Get("ma_host_desc"));
        table.Controls.Add(maHostDesc, 0, 1);
        table.SetColumnSpan(maHostDesc, 2);

        // MA Port
        table.Controls.Add(MakeLabel(Localization.Get("ma_port", "MA-Port")), 0, 2);
        _maPortBox = new TextBox { Dock = DockStyle.Fill, Text = "8095", Height = 28 };
        AddTooltip(_maPortBox, Localization.Get("ma_port_tooltip"));
        table.Controls.Add(_maPortBox, 1, 2);

        // Beschreibung: MA Port
        var maPortDesc = MakeDescriptionLabel(Localization.Get("ma_port_desc"));
        table.Controls.Add(maPortDesc, 0, 3);
        table.SetColumnSpan(maPortDesc, 2);

        // MA Token (password)
        table.Controls.Add(MakeLabel(Localization.Get("ma_token", "MA-Token")), 0, 4);
        _maTokenBox = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true, Height = 28 };
        AddTooltip(_maTokenBox, Localization.Get("ma_token_tooltip"));
        table.Controls.Add(_maTokenBox, 1, 4);

        // Beschreibung: MA Token
        var maTokenDesc = MakeDescriptionLabel(Localization.Get("ma_token_desc"));
        table.Controls.Add(maTokenDesc, 0, 5);
        table.SetColumnSpan(maTokenDesc, 2);

        // Verbindung testen Button
        table.Controls.Add(MakeLabel(Localization.Get("mqtt_test_connection", "Verbindung testen")), 0, 6);
        _maTestBtn = MakeButton("🧪 " + Localization.Get("mqtt_test_connection", "Verbindung testen"), Color.FromArgb(0, 100, 180), OnMaTestConnection);
        AddTooltip(_maTestBtn, Localization.Get("ma_test_tooltip"));
        table.Controls.Add(_maTestBtn, 1, 6);

        // Test-Status Label
        _maTestResultLabel = new Label
        {
            Text = "",
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.Gray,
            AutoSize = true,
            Margin = new Padding(0, 6, 0, 0),
        };
        table.Controls.Add(_maTestResultLabel, 0, 7);
        table.SetColumnSpan(_maTestResultLabel, 2);

        // In umgekehrter Reihenfolge hinzufügen
        section.Controls.Add(table);
        section.Controls.Add(header);

        return section;
    }

    // ─── Section 8: 🔊 Streaming (Sendspin) ───
    private Panel BuildStreamingSection()
    {
        var section = MakeSectionPanel();
        var header = MakeSectionHeader("🔊 " + Localization.Get("stream_settings", "Streaming"));

        // 5 Zeilen: Enable | Enable-Desc | Player-Name | Name-Desc | Status
        var table = MakeFieldTable(5);

        // Enable-Toggle
        table.Controls.Add(MakeLabel(Localization.Get("stream_enable", "Aktiviert")), 0, 0);
        _streamEnabledCheck = new CheckBox
        {
            Dock = DockStyle.Fill,
            Text = Localization.Get("stream_enable", "Aktiviert"),
            ForeColor = Color.Transparent,
            AutoSize = true,
        };
        table.Controls.Add(_streamEnabledCheck, 1, 0);

        var enableDesc = MakeDescriptionLabel(Localization.Get("stream_enable_desc"));
        table.Controls.Add(enableDesc, 0, 1);
        table.SetColumnSpan(enableDesc, 2);

        // Player-Name
        table.Controls.Add(MakeLabel(Localization.Get("stream_player_name", "Player-Name")), 0, 2);
        _streamPlayerNameBox = new TextBox { Dock = DockStyle.Fill, Height = 28 };
        AddTooltip(_streamPlayerNameBox, Localization.Get("stream_player_name_desc"));
        table.Controls.Add(_streamPlayerNameBox, 1, 2);

        var nameDesc = MakeDescriptionLabel(Localization.Get("stream_player_name_desc"));
        table.Controls.Add(nameDesc, 0, 3);
        table.SetColumnSpan(nameDesc, 2);

        // Status-Label (Live-Verbindungsstatus des Sendspin-Clients)
        _streamStatusLabel = new Label
        {
            Text = "○ " + Localization.Get("stream_status", "Deaktiviert"),
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.Gray,
            AutoSize = true,
            Margin = new Padding(0, 6, 0, 0),
        };
        table.Controls.Add(_streamStatusLabel, 0, 4);
        table.SetColumnSpan(_streamStatusLabel, 2);
        UpdateStreamStatusLabel();

        // In umgekehrter Reihenfolge hinzufügen
        section.Controls.Add(table);
        section.Controls.Add(header);

        return section;
    }

    private void UpdateStreamStatusLabel()
    {
        if (_streamStatusLabel == null) return;
        if (!_config.SendspinEnabled)
        {
            _streamStatusLabel.Text = "○ " + Localization.Get("stream_status", "Deaktiviert");
            _streamStatusLabel.ForeColor = Color.Gray;
            return;
        }
        var app = DeskLinkApp.Instance;
        var manager = app?._sendspinManager;
        if (manager?.IsRunning == true)
        {
            _streamStatusLabel.Text = "● " + Localization.Get("stream_status_connecting", "Verbinde…");
            _streamStatusLabel.ForeColor = WarningOrange;
        }
        else
        {
            _streamStatusLabel.Text = "● " + Localization.Get("stream_status_stopped", "Gestoppt");
            _streamStatusLabel.ForeColor = Color.Gray;
        }
    }

    // ─── Section 9: ⚡ Quick Actions ───
    private Panel BuildQuickActionsSection()
    {
        var section = MakeSectionPanel();
        var header = MakeSectionHeader("⚡ " + Localization.Get("settings_quickactions"));

        // Detaillierte Beschreibung oben in der Section
        var descLabel = new Label
        {
            Text = Localization.Get("desc_quickactions_intro"),
            AutoSize = true,
            ForeColor = Color.Gray,
            Font = new Font("Segoe UI", 8f),
            Margin = new Padding(0, 4, 0, 8),
            Dock = DockStyle.Top,
            Tag = "desc",
        };

        // Load Entities Button
        var qaLoadPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 0, 4),
        };
        var loadBtn = MakeButton("📥 " + Localization.Get("settings_load_entities", "Entities laden"), SuccessGreen, OnLoadEntities);
        AddTooltip(loadBtn, Localization.Get("tooltip_load_entities"));
        qaLoadPanel.Controls.Add(loadBtn);

        // Beschreibung: Load Entities
        var loadEntitiesDesc = MakeDescriptionLabel(Localization.Get("desc_load_entities"));
        loadEntitiesDesc.Dock = DockStyle.Top;
        loadEntitiesDesc.Margin = new Padding(0, 0, 0, 8);

        // Entity ListBox (volle Breite)
        _qaList = new ListBox
        {
            Dock = DockStyle.Top,
            Height = 250,
            MinimumSize = new Size(0, 120),
            Margin = new Padding(0, 0, 0, 8),
        };

        // Add / Edit / Remove Buttons
        var qaEditPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 0, 0),
        };

        var addBtn = MakeButton("➕ " + Localization.Get("settings_qa_add", "Hinzufügen"), SuccessGreen, OnAddQuickAction);
        AddTooltip(addBtn, Localization.Get("tooltip_add_qa"));

        var editBtn = MakeButton("✏️ " + Localization.Get("settings_qa_edit", "Bearbeiten"), Color.FromArgb(100, 100, 100), OnEditQuickAction);
        AddTooltip(editBtn, Localization.Get("tooltip_edit_qa"));

        var removeBtn = MakeButton("🗑️ " + Localization.Get("settings_qa_remove", "Entfernen"), WarningOrange, OnRemoveQuickAction);
        AddTooltip(removeBtn, Localization.Get("tooltip_remove_qa"));

        qaEditPanel.Controls.Add(addBtn);
        qaEditPanel.Controls.Add(editBtn);
        qaEditPanel.Controls.Add(removeBtn);

        // In umgekehrter Reihenfolge hinzufügen (Dock=Top: zuletzt hinzugefügter oben)
        section.Controls.Add(qaEditPanel);        // ganz unten
        section.Controls.Add(_qaList);            // darüber
        section.Controls.Add(loadEntitiesDesc);    // darüber (Beschreibung für Load Entities)
        section.Controls.Add(qaLoadPanel);        // darüber (Load Entities Button)
        section.Controls.Add(descLabel);          // darüber (Section-Beschreibung)
        section.Controls.Add(header);              // ganz oben

        return section;
    }

    // ─── Section 10: 🖥️ Desktop-Widgets (Widget-Editor) ───
    private Panel BuildWidgetsSection()
    {
        var section = MakeSectionPanel();
        var header = MakeSectionHeader("🖥️ " + Localization.Get("ma_widgets", "Desktop-Widgets"));

        // Detaillierte Beschreibung oben in der Section
        _widgetDescLabel = new Label
        {
            Text = Localization.Get("widget_desc_intro",
                "Desktop-Widgets liegen hinter den Desktop-Icons (WorkerW-Schicht) und zeigen Live-Werte oder Schalter. " +
                "Zum Positionieren: Testen-Modus aktivieren und Widget mit rechter Maustaste ziehen."),
            AutoSize = true,
            ForeColor = Color.Gray,
            Font = new Font("Segoe UI", 8f),
            Margin = new Padding(0, 4, 0, 8),
            Dock = DockStyle.Top,
            Tag = "desc",
        };

        // Widget-Liste
        _widgetList = new ListBox
        {
            Dock = DockStyle.Top,
            Height = 180,
            MinimumSize = new Size(0, 100),
            Margin = new Padding(0, 0, 0, 8),
        };

        // Buttons: Add / Edit / Remove / Test (Drag-Modus)
        var widgetEditPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 0, 0),
        };

        var addBtn = MakeButton("➕ " + Localization.Get("widget_add", "Hinzufügen"), SuccessGreen, OnAddWidget);
        AddTooltip(addBtn, Localization.Get("widget_add_tooltip", "Neues Widget erstellen"));

        var editBtn = MakeButton("✏️ " + Localization.Get("settings_qa_edit", "Bearbeiten"), Color.FromArgb(100, 100, 100), OnEditWidget);
        AddTooltip(editBtn, Localization.Get("widget_edit_tooltip", "Ausgewähltes Widget bearbeiten"));

        var removeBtn = MakeButton("🗑️ " + Localization.Get("settings_qa_remove", "Entfernen"), WarningOrange, OnRemoveWidget);
        AddTooltip(removeBtn, Localization.Get("widget_remove_tooltip", "Ausgewähltes Widget löschen"));

        _widgetTestBtn = MakeButton("🎯 " + Localization.Get("widget_test", "Testen"), Color.FromArgb(0, 100, 180), OnTestWidgets);
        AddTooltip(_widgetTestBtn, Localization.Get("widget_test_tooltip",
            "Drag-Modus ein-/ausschalten: Widgets mit rechter Maustaste verschieben, Position wird gespeichert"));

        widgetEditPanel.Controls.Add(addBtn);
        widgetEditPanel.Controls.Add(editBtn);
        widgetEditPanel.Controls.Add(removeBtn);
        widgetEditPanel.Controls.Add(_widgetTestBtn);

        // In umgekehrter Reihenfolge hinzufügen (Dock=Top: zuletzt hinzugefügter oben)
        section.Controls.Add(widgetEditPanel);   // ganz unten
        section.Controls.Add(_widgetList);        // darüber
        section.Controls.Add(_widgetDescLabel);   // darüber
        section.Controls.Add(header);             // ganz oben

        return section;
    }

    // ═══════════════════════════════════════════════════════
    // BOTTOM BAR — Save (rechts), Reconnect (links daneben), Status (links)
    // 56px hoch, mit 1px Top-Border
    // ═══════════════════════════════════════════════════════

    private void BuildBottomBar()
    {
        _bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            Padding = new Padding(0),
        };

        // TableLayoutPanel: Status (links, Percent 100%) | Reconnect | Save (rechts)
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(16, 0, 16, 0),
            Margin = new Padding(0),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));  // Status (füllt restlichen Platz)
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));       // Reconnect
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));       // Save

        // Status Label (links)
        _statusLabel = new Label
        {
            Text = "",
            ForeColor = Color.Gray,
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9f),
            Margin = new Padding(0, 0, 16, 0),
        };

        // Reconnect Button
        var reconnectBtn = MakeButton("🔄 " + Localization.Get("settings_reconnect", "Neu verbinden"), Color.FromArgb(0, 100, 180), OnReconnectClicked);
        AddTooltip(reconnectBtn, Localization.Get("tooltip_reconnect"));

        // Save Button (AccentBlue im Dark Theme)
        var saveBtn = MakeButton("💾 " + Localization.Get("settings_save"), AccentBlue, OnSave);
        AddTooltip(saveBtn, Localization.Get("tooltip_save"));

        layout.Controls.Add(_statusLabel, 0, 0);
        layout.Controls.Add(reconnectBtn, 1, 0);
        layout.Controls.Add(saveBtn, 2, 0);

        _bottomPanel.Controls.Add(layout);

        // 1px Top-Border via Paint (Farbe je nach Theme)
        _bottomPanel.Paint += (s, e) =>
        {
            var borderColor = _isDark ? Color.FromArgb(60, 60, 60) : Color.FromArgb(200, 200, 200);
            using var pen = new Pen(borderColor, 1);
            e.Graphics.DrawLine(pen, 0, 0, _bottomPanel.Width, 0);
        };

        Controls.Add(_bottomPanel);
    }

    // ═══════════════════════════════════════════════════════
    // HELPER METHODEN
    // ═══════════════════════════════════════════════════════

    // ─── Helper: Hotkey-Row (Modifier Dropdown + "+" + Key Dropdown) ───
    private static FlowLayoutPanel CreateHotkeyRow(out ComboBox modBox, out ComboBox keyBox)
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
        };
        modBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120, Height = 28 };
        modBox.Items.AddRange(new object[] { "Ctrl+Shift", "Ctrl+Alt", "Ctrl", "Alt", "Shift", Localization.Get("settings_hotkey_none") });
        panel.Controls.Add(modBox);
        panel.Controls.Add(new Label { Text = "+", AutoSize = true, Margin = new Padding(4, 6, 4, 0) });
        keyBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80, Height = 28 };
        keyBox.Items.AddRange(new object[] { "H", "Q", "A", "S", "D", "F", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12", "Space" });
        panel.Controls.Add(keyBox);
        return panel;
    }

    // ─── Helper: Section Header (größer, fett, mit Abstand) ───
    private static Label MakeSectionHeader(string text)
    {
        return new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4),
            Dock = DockStyle.Top,
        };
    }

    // ─── Helper: Label (rechtsbündig für linke Spalte, max 192px Breite) ───
    // 200px Spalte - 8px rechter Margin = 192px max Label-Breite
    // Verhindert Wortumbruch bei deutschen Langwort-Begriffen
    private static Label MakeLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            MaximumSize = new Size(192, 0),
            TextAlign = ContentAlignment.MiddleRight,
            Margin = new Padding(0, 6, 8, 0),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
    }

    // ─── Helper: Beschreibungs-Label (klein, grau, unter Eingabefeldern) ───
    // Theme-abhängige Farbe wird in ApplyTheme() gesetzt (Tag="desc")
    private static Label MakeDescriptionLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Segoe UI", 8f),
            ForeColor = Color.Gray,  // wird in ApplyTheme theme-abhängig gesetzt
            Tag = "desc",
            Margin = new Padding(0, 2, 0, 6),
        };
    }

    // ─── Helper: Button mit AutoSize und FlatStyle ───
    private static Button MakeButton(string text, Color color, EventHandler onClick)
    {
        var btn = new Button
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(140, 36),
            BackColor = color,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f),
            Margin = new Padding(2),
            Padding = new Padding(12, 0, 12, 0),
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += onClick;
        return btn;
    }

    // ─── Helper: Tooltips ───
    private static void AddTooltip(Control control, string text)
    {
        var tip = new ToolTip { IsBalloon = true, InitialDelay = 300, ReshowDelay = 100, AutoPopDelay = 8000 };
        tip.SetToolTip(control, text);
    }

    // ═══════════════════════════════════════════════════════
    // QUICK ACTIONS LOGIK
    // ═══════════════════════════════════════════════════════

    private List<QuickAction> GetCurrentQuickActions()
    {
        var actions = new List<QuickAction>();
        try
        {
            var arr = JsonDocument.Parse(_config.QuickActions).RootElement;
            foreach (var item in arr.EnumerateArray())
            {
                var entityId = item.TryGetProperty("entityId", out var eid) ? eid.GetString() ?? "" : "";
                var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? entityId : entityId;
                if (!string.IsNullOrEmpty(entityId))
                    actions.Add(new QuickAction(entityId, name));
            }
        }
        catch { }
        return actions;
    }

    private void LoadQuickActionsList()
    {
        _qaList.Items.Clear();
        var actions = GetCurrentQuickActions();
        foreach (var a in actions)
            _qaList.Items.Add($"{a.Name} ({a.EntityId})");
    }

    // ═══════════════════════════════════════════════════════
    // WIDGET LOGIK
    // ═══════════════════════════════════════════════════════

    private void LoadWidgetsList()
    {
        _widgetList.Items.Clear();
        var widgets = WidgetConfigStore.Parse(_config);
        foreach (var w in widgets)
        {
            var typeStr = w.Type switch
            {
                WidgetType.SensorCard => Localization.Get("widget_sensors", "Sensor"),
                WidgetType.ToggleCard => Localization.Get("widget_toggles", "Schalter"),
                _ => Localization.Get("widget_multi_toggle", "Multi-Schalter")
            };
            var entityStr = w.Type == WidgetType.MultiToggleCard
                ? $"{w.Entities.Count}× Entity"
                : w.EntityId;
            _widgetList.Items.Add($"[{typeStr}] {w.Name} — {entityStr} (Monitor {w.Monitor}, {w.OffsetX}/{w.OffsetY})");
        }
    }

    private static string WidgetTypeToConfigString(WidgetType t) => t switch
    {
        WidgetType.SensorCard => "sensorCard",
        WidgetType.ToggleCard => "toggleCard",
        _ => "multiToggleCard"
    };

    private void SaveWidgets(List<WidgetConfig> widgets)
    {
        WidgetConfigStore.Save(_config, widgets);
        LoadWidgetsList();
        // Live neu laden, damit der Manager die Widgets aktualisiert
        DeskLinkApp.Instance?._widgetManager?.ReloadAll();
    }

    private void OnAddWidget(object? sender, EventArgs e)
    {
        if (_entities.Count == 0)
        {
            MessageBox.Show(Localization.Get("settings_load_entities_first"),
                "HA DeskLink", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        ShowWidgetEditor(null);
    }

    private void OnEditWidget(object? sender, EventArgs e)
    {
        if (_widgetList.SelectedIndex < 0)
        {
            MessageBox.Show(Localization.Get("settings_qa_select_first"),
                "HA DeskLink", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var widgets = WidgetConfigStore.Parse(_config);
        var idx = _widgetList.SelectedIndex;
        if (idx >= widgets.Count) return;
        ShowWidgetEditor(widgets[idx]);
    }

    private void OnRemoveWidget(object? sender, EventArgs e)
    {
        if (_widgetList.SelectedIndex < 0)
        {
            MessageBox.Show(Localization.Get("settings_qa_select_first"),
                "HA DeskLink", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var widgets = WidgetConfigStore.Parse(_config);
        var idx = _widgetList.SelectedIndex;
        if (idx < widgets.Count)
        {
            var removedId = widgets[idx].Id;
            widgets.RemoveAt(idx);
            WidgetConfigStore.Save(_config, widgets);
            LoadWidgetsList();
            // Widget live schließen
            DeskLinkApp.Instance?._widgetManager?.HideWidget(removedId);
        }
    }

    private bool _widgetDragActive;

    /// <summary>
    /// Test-Button: schaltet den Drag-Modus aller Widgets um (rechte Maus = verschieben,
    /// Position wird als Monitor+Offset gespeichert).
    /// </summary>
    private void OnTestWidgets(object? sender, EventArgs e)
    {
        var manager = DeskLinkApp.Instance?._widgetManager;
        _widgetDragActive = !_widgetDragActive;
        manager?.SetDragMode(_widgetDragActive);
        _widgetTestBtn.Text = _widgetDragActive
            ? "🎯 " + Localization.Get("widget_test_active", "Drag-Modus aktiv — klick zum Beenden")
            : "🎯 " + Localization.Get("widget_test", "Testen");
    }

    /// <summary>Widget-Editor-Dialog (Add + Edit). existing = null → neu anlegen.</summary>
    private void ShowWidgetEditor(WidgetConfig? existing)
    {
        var isNew = existing == null;
        var wc = existing ?? new WidgetConfig();

        using var dialog = new Form
        {
            Text = isNew
                ? Localization.Get("widget_add", "Widget hinzufügen")
                : Localization.Get("settings_qa_edit", "Widget bearbeiten"),
            Size = new Size(520, 460),
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
        };

        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 9, Padding = new Padding(16) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // Typ
        table.Controls.Add(MakeLabel(Localization.Get("widget_type", "Typ:")), 0, 0);
        var typeCombo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        typeCombo.Items.Add(new WidgetTypeItem(WidgetType.SensorCard, Localization.Get("widget_sensors", "Sensor-Karte")));
        typeCombo.Items.Add(new WidgetTypeItem(WidgetType.ToggleCard, Localization.Get("widget_toggles", "Schalter-Karte")));
        typeCombo.Items.Add(new WidgetTypeItem(WidgetType.MultiToggleCard, Localization.Get("widget_multi_toggle", "Multi-Schalter-Karte (2-4)")));
        var typeIdx = typeCombo.Items.OfType<WidgetTypeItem>()
            .ToList().FindIndex(i => i.Type == wc.Type);
        typeCombo.SelectedIndex = typeIdx >= 0 ? typeIdx : 0;
        table.Controls.Add(typeCombo, 1, 0);

        // Name
        table.Controls.Add(MakeLabel(Localization.Get("settings_qa_name", "Name:")), 0, 1);
        var nameBox = new TextBox { Dock = DockStyle.Fill, Text = wc.Name };
        table.Controls.Add(nameBox, 1, 1);

        // Entity (Sensor/Toggle)
        table.Controls.Add(MakeLabel(Localization.Get("settings_qa_entity", "Entity:")), 0, 2);
        var entityCombo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var (entityId, friendlyName) in _entities)
            entityCombo.Items.Add(new EntityItem(entityId, friendlyName));
        for (int i = 0; i < entityCombo.Items.Count; i++)
        {
            if (entityCombo.Items[i] is EntityItem ei && ei.EntityId == wc.EntityId)
            {
                entityCombo.SelectedIndex = i;
                break;
            }
        }
        if (entityCombo.SelectedIndex < 0 && entityCombo.Items.Count > 0) entityCombo.SelectedIndex = 0;
        table.Controls.Add(entityCombo, 1, 2);

        // MultiToggle-Entities (2-4) via CheckedListBox mit Entity-Auswahl
        table.Controls.Add(MakeLabel(Localization.Get("widget_entities", "Entities:")), 0, 3);
        var multiCheck = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true };
        foreach (var (entityId, friendlyName) in _entities)
        {
            var idx = multiCheck.Items.Add(new EntityItem(entityId, friendlyName));
            if (wc.Entities.Any(x => x.EntityId == entityId))
                multiCheck.SetItemChecked(idx, true);
        }
        table.Controls.Add(multiCheck, 1, 3);
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); // row 0
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); // row 1
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); // row 2
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // row 3 multiCheck
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); // row 4 monitor
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); // row 5 offsets
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); // row 6 clickThrough
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); // row 7 offset fields
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // row 8 buttons

        // Monitor
        table.Controls.Add(MakeLabel(Localization.Get("settings_notif_monitor", "Monitor:")), 0, 4);
        var monitorCombo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        for (int i = 0; i < Screen.AllScreens.Length; i++)
        {
            var label = i == 0
                ? Localization.Get("settings_notif_primary_monitor", "Primärer Monitor")
                : $"Monitor {i + 1}";
            monitorCombo.Items.Add(label);
        }
        monitorCombo.SelectedIndex = Math.Min(wc.Monitor, monitorCombo.Items.Count - 1);
        table.Controls.Add(monitorCombo, 1, 4);

        // Offset X/Y
        table.Controls.Add(MakeLabel(Localization.Get("widget_position", "Position (Offset):")), 0, 5);
        var offsetPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        var offX = new NumericUpDown { Minimum = -4000, Maximum = 8000, Value = wc.OffsetX, Width = 80 };
        var offY = new NumericUpDown { Minimum = -4000, Maximum = 8000, Value = wc.OffsetY, Width = 80 };
        offsetPanel.Controls.Add(offX);
        offsetPanel.Controls.Add(new Label { Text = "X", AutoSize = true, Margin = new Padding(8, 8, 8, 0) });
        offsetPanel.Controls.Add(offY);
        offsetPanel.Controls.Add(new Label { Text = "Y", AutoSize = true, Margin = new Padding(8, 8, 0, 0) });
        table.Controls.Add(offsetPanel, 1, 5);

        // Click-Through
        table.Controls.Add(MakeLabel(Localization.Get("widget_click_through", "Klick-Durchlässig:")), 0, 6);
        var clickThroughCheck = new CheckBox
        {
            Text = Localization.Get("widget_click_through_desc", "Maus-Klicks gehen durch das Widget hindurch (nur Anzeige)"),
            AutoSize = true,
            Checked = wc.ClickThrough,
        };
        table.Controls.Add(clickThroughCheck, 1, 6);

        // Buttons
        var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        var saveBtn = new Button { Text = "💾 " + Localization.Get("settings_save", "Speichern"), BackColor = AccentBlue, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, AutoSize = true };
        saveBtn.FlatAppearance.BorderSize = 0;
        var cancelBtn = new Button { Text = Localization.Get("settings_cancel", "Abbrechen"), BackColor = Color.FromArgb(100, 100, 100), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, AutoSize = true };
        cancelBtn.FlatAppearance.BorderSize = 0;
        btnPanel.Controls.Add(saveBtn);
        btnPanel.Controls.Add(cancelBtn);
        table.Controls.Add(btnPanel, 1, 8);

        // Typ-abhängige Felder ein-/ausblenden
        void UpdateVisibility()
        {
            var type = ((WidgetTypeItem?)typeCombo.SelectedItem)?.Type ?? WidgetType.SensorCard;
            var isMulti = type == WidgetType.MultiToggleCard;
            multiCheck.Visible = isMulti;
            entityCombo.Visible = !isMulti;
        }
        typeCombo.SelectedIndexChanged += (s, args) => UpdateVisibility();
        UpdateVisibility();

        cancelBtn.Click += (s, args) => dialog.Close();

        saveBtn.Click += (s, args) =>
        {
            var type = ((WidgetTypeItem?)typeCombo.SelectedItem)?.Type ?? WidgetType.SensorCard;
            wc.Type = type;
            wc.Name = nameBox.Text.Trim();
            wc.Monitor = Math.Max(0, monitorCombo.SelectedIndex);
            wc.OffsetX = (int)offX.Value;
            wc.OffsetY = (int)offY.Value;
            wc.ClickThrough = clickThroughCheck.Checked;
            wc.Entities.Clear();

            bool valid = true;
            if (type == WidgetType.MultiToggleCard)
            {
                foreach (var checkedItem in multiCheck.CheckedItems)
                {
                    if (checkedItem is EntityItem ei)
                        wc.Entities.Add(new WidgetEntity(ei.EntityId, ei.FriendlyName));
                }
                if (wc.Entities.Count is < 2 or > 4)
                {
                    MessageBox.Show(Localization.Get("widget_multi_count_error", "Multi-Schalter benötigt 2 bis 4 Entities."),
                        "HA DeskLink", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    valid = false;
                }
                wc.EntityId = "";
            }
            else
            {
                if (entityCombo.SelectedItem is EntityItem item)
                    wc.EntityId = item.EntityId;
                if (string.IsNullOrEmpty(wc.EntityId))
                {
                    MessageBox.Show(Localization.Get("settings_load_entities_first"),
                        "HA DeskLink", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    valid = false;
                }
            }
            if (string.IsNullOrEmpty(wc.Name)) wc.Name = wc.EntityId;

            if (!valid) return;

            var widgets = WidgetConfigStore.Parse(_config);
            if (isNew) widgets.Add(wc);
            else
            {
                var i = widgets.FindIndex(x => x.Id == wc.Id);
                if (i >= 0) widgets[i] = wc; else widgets.Add(wc);
            }
            SaveWidgets(widgets);
            dialog.Close();
        };

        dialog.Controls.Add(table);
        ApplyThemeToControls(dialog, _config.Theme);
        dialog.ShowDialog(this);
    }

    private class WidgetTypeItem
    {
        public WidgetType Type { get; }
        public string Label { get; }
        public WidgetTypeItem(WidgetType type, string label) { Type = type; Label = label; }
        public override string ToString() => Label;
    }

    // ═══════════════════════════════════════════════════════
    // BUTTON HANDLERS
    // ═══════════════════════════════════════════════════════

    private void OnSave(object? sender, EventArgs e)
    {
        // URL validieren
        var url = _urlBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(url))
        {
            MessageBox.Show(Localization.Get("validation_url_empty"), Localization.Get("validation_title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _urlBox.Focus();
            return;
        }
        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
        {
            MessageBox.Show(Localization.Get("validation_url_invalid"), Localization.Get("validation_title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _urlBox.Focus();
            return;
        }

        // Token validieren
        if (string.IsNullOrWhiteSpace(_tokenBox.Text.Trim()))
        {
            MessageBox.Show(Localization.Get("validation_token_empty"), Localization.Get("validation_title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _tokenBox.Focus();
            return;
        }

        _config.HaUrl = _urlBox.Text.Trim();
        _config.HaToken = _tokenBox.Text.Trim();
        _config.VerifySsl = _sslCheck.Checked;
        _config.Autostart = _autostartCheck.Checked;
        _config.SensorInterval = Math.Max(10, (int)_intervalBox.Value);  // Minimum 10s erzwingen
        _config.UpdateChannel = _updateChannelBox.SelectedIndex == 1 ? "prerelease" : "stable";

        if (_languageBox.SelectedIndex >= 0 && _languageBox.SelectedIndex < Localization.AvailableLanguages.Count)
            _config.Language = Localization.AvailableLanguages[_languageBox.SelectedIndex];

        _config.Theme = _themeBox.SelectedIndex switch { 1 => "light", 2 => "dark", _ => "system" };

        // Benachrichtigungs-Position
        _config.NotificationPosition = _notifPosBox.SelectedIndex switch
        {
            1 => "bottom_right", 2 => "top_left", 3 => "top_right", _ => "bottom_left"
        };
        _config.NotificationMonitor = Math.Max(0, _notifMonitorBox.SelectedIndex);

        // Hotkeys
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

        // MQTT-Einstellungen
        _config.MqttEnabled = _mqttEnabledCheck.Checked;
        _config.MqttBroker = _mqttBrokerBox.Text.Trim();
        if (int.TryParse(_mqttPortBox.Text.Trim(), out var mqttPort))
            _config.MqttPort = mqttPort;
        _config.MqttUsername = _mqttUserBox.Text.Trim();
        _config.MqttPassword = _mqttPassBox.Text;
        _config.MqttUseSsl = _mqttSslCheck.Checked;
        _config.MqttAutoConfigured = false; // manuelles Speichern
        _config.MqttBrokerFallback = _mqttFallbackBox.Text.Trim();

        // Music-Assistant-Einstellungen
        _config.MaHost = _maHostBox.Text.Trim();
        if (int.TryParse(_maPortBox.Text.Trim(), out var maPort) && maPort > 0 && maPort <= 65535)
            _config.MaPort = maPort;
        // Token nur übernehmen, wenn eines eingegeben wurde (leer lassen = bestehenden behalten)
        if (!string.IsNullOrEmpty(_maTokenBox.Text))
            _config.MaToken = _maTokenBox.Text.Trim();

        // Streaming (Sendspin)-Einstellungen
        _config.SendspinEnabled = _streamEnabledCheck.Checked;
        _config.SendspinPlayerName = string.IsNullOrWhiteSpace(_streamPlayerNameBox.Text.Trim())
            ? "HA DeskLink PC"
            : _streamPlayerNameBox.Text.Trim();

        _config.Save();
        if (_config.Autostart) Autostart.Enable(); else Autostart.Disable();
        ApplyTheme(_config.Theme);
        // Sprache neu laden, falls sie geändert wurde
        Localization.LoadLanguage(_config.Language);
        _statusLabel.Text = $"✓ {Localization.Get("settings_saved")}";

        // Sendspin-Client bei Konfigurationsänderung neu starten
        var app = DeskLinkApp.Instance;
        app?._sendspinManager?.Restart();
        UpdateStreamStatusLabel();
    }

    /// <summary>
    /// Neu verbinden mit HA — setzt auch Login-Block zurück falls blockiert.
    /// Das ist der EINZIGE Reconnect-Button — keine Duplikate.
    /// </summary>
    private void OnReconnectClicked(object? sender, EventArgs e)
    {
        // WebSocket-Login-Block zurücksetzen (z.B. nach 3 fehlgeschlagenen Token-Versuchen)
        var app = DeskLinkApp.Instance;
        if (app?._wsClient != null && app._wsClient.LoginBlocked)
        {
            app._wsClient.ResetLoginBlock();
            _statusLabel.Text = Localization.Get("status_login_reset");
        }
        else
        {
            _statusLabel.Text = Localization.Get("status_reconnecting");
        }

        // Reconnect asynchron ausführen, um den UI-Thread nicht zu blockieren
        System.Threading.Tasks.Task.Run(() =>
        {
            try { _onReconnect.Invoke(); }
            catch { }
        });

        _statusLabel.Text = Localization.Get("status_reconnect_done");
    }

    private void OnResetDeviceId(object? sender, EventArgs e)
    {
        var result = MessageBox.Show(
            Localization.Get("settings_reset_device_confirm") + Localization.Get("settings_extra_note_device_reset"),
            Localization.Get("settings_reset_device"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (result == DialogResult.Yes)
        {
            _api?.ResetDeviceId();
            _statusLabel.Text = $"✓ {Localization.Get("settings_reset_device_done")}";
        }
    }

    private void OnReRegisterSensors(object? sender, EventArgs e)
    {
        var result = MessageBox.Show(
            Localization.Get("settings_reregister_confirm") + Localization.Get("settings_extra_note_reregister"),
            Localization.Get("settings_reregister_sensors"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (result == DialogResult.Yes)
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                try { DeskLinkApp.ReRegisterSensors(); }
                catch { }
            });
            _statusLabel.Text = $"✓ {Localization.Get("settings_reregister_done")}";
        }
    }

    private async void OnLoadEntities(object? sender, EventArgs e)
    {
        if (_api == null)
        {
            _statusLabel.Text = Localization.Get("status_no_ha_connection");
            return;
        }

        _statusLabel.Text = Localization.Get("status_loading_entities");
        try
        {
            _entities = await _api.GetEntitiesAsync();
            _entities = _entities.OrderBy(x => x.entityId).ToList();
            _statusLabel.Text = Localization.Get("status_entities_loaded", _entities.Count);
        }
        catch (Exception ex)
        {
            _statusLabel.Text = Localization.Get("status_error", ex.Message);
        }
    }

    private void OnAddQuickAction(object? sender, EventArgs e)
    {
        if (_entities.Count == 0)
        {
            MessageBox.Show(Localization.Get("settings_load_entities_first"), "HA DeskLink", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new Form
        {
            Text = Localization.Get("settings_qa_add", "Quick Action hinzufügen"),
            Size = new Size(450, 200),
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
        };

        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Padding = new Padding(16) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var entityCombo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var (entityId, friendlyName) in _entities)
            entityCombo.Items.Add(new EntityItem(entityId, friendlyName));
        if (entityCombo.Items.Count > 0) entityCombo.SelectedIndex = 0;

        var nameBox = new TextBox { Dock = DockStyle.Fill };

        entityCombo.SelectedIndexChanged += (s, args) =>
        {
            if (entityCombo.SelectedItem is EntityItem item)
                nameBox.Text = item.FriendlyName;
        };
        if (entityCombo.Items.Count > 0) entityCombo.SelectedIndex = 0;

        var okBtn = new Button { Text = Localization.Get("settings_qa_add", "Hinzufügen"), Dock = DockStyle.Fill, BackColor = AccentBlue, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        okBtn.FlatAppearance.BorderSize = 0;

        table.Controls.Add(MakeLabel(Localization.Get("settings_qa_entity", "Entity:")), 0, 0);
        table.Controls.Add(entityCombo, 1, 0);
        table.Controls.Add(MakeLabel(Localization.Get("settings_qa_name", "Name:")), 0, 1);
        table.Controls.Add(nameBox, 1, 1);
        table.Controls.Add(new Label(), 0, 2);
        table.Controls.Add(okBtn, 1, 2);

        okBtn.Click += (s, args) =>
        {
            if (entityCombo.SelectedItem is EntityItem item)
            {
                var actions = GetCurrentQuickActions();
                actions.Add(new QuickAction(item.EntityId, string.IsNullOrEmpty(nameBox.Text) ? item.FriendlyName : nameBox.Text));
                _config.QuickActions = JsonSerializer.Serialize(actions, _jsonOpts);
                _config.Save();
                LoadQuickActionsList();
                dialog.Close();
            }
        };

        dialog.Controls.Add(table);
        ApplyThemeToControls(dialog, _config.Theme);
        dialog.ShowDialog(this);
    }

    private void OnEditQuickAction(object? sender, EventArgs e)
    {
        if (_qaList.SelectedIndex < 0)
        {
            MessageBox.Show(Localization.Get("settings_qa_select_first"), "HA DeskLink", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var actions = GetCurrentQuickActions();
        var idx = _qaList.SelectedIndex;
        if (idx >= actions.Count) return;

        var action = actions[idx];

        using var dialog = new Form
        {
            Text = Localization.Get("settings_qa_edit", "Quick Action bearbeiten"),
            Size = new Size(450, 250),
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
        };

        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, Padding = new Padding(16) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var entityCombo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var (entityId, friendlyName) in _entities)
            entityCombo.Items.Add(new EntityItem(entityId, friendlyName));

        for (int i = 0; i < entityCombo.Items.Count; i++)
        {
            if (entityCombo.Items[i] is EntityItem ei && ei.EntityId == action.EntityId)
            {
                entityCombo.SelectedIndex = i;
                break;
            }
        }
        if (entityCombo.SelectedIndex < 0 && entityCombo.Items.Count > 0) entityCombo.SelectedIndex = 0;

        var nameBox = new TextBox { Dock = DockStyle.Fill, Text = action.Name };

        entityCombo.SelectedIndexChanged += (s, args) =>
        {
            if (entityCombo.SelectedItem is EntityItem item)
                nameBox.Text = item.FriendlyName;
        };

        var deleteBtn = new Button { Text = "🗑️ " + Localization.Get("settings_qa_remove", "Entfernen"), BackColor = WarningOrange, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        deleteBtn.FlatAppearance.BorderSize = 0;
        var saveBtn = new Button { Text = "💾 " + Localization.Get("settings_save", "Speichern"), BackColor = AccentBlue, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        saveBtn.FlatAppearance.BorderSize = 0;

        var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        btnPanel.Controls.Add(saveBtn);
        btnPanel.Controls.Add(deleteBtn);

        table.Controls.Add(MakeLabel(Localization.Get("settings_qa_entity", "Entity:")), 0, 0);
        table.Controls.Add(entityCombo, 1, 0);
        table.Controls.Add(MakeLabel(Localization.Get("settings_qa_name", "Name:")), 0, 1);
        table.Controls.Add(nameBox, 1, 1);
        table.Controls.Add(new Label(), 0, 2);
        table.Controls.Add(btnPanel, 1, 2);

        saveBtn.Click += (s, args) =>
        {
            if (entityCombo.SelectedItem is EntityItem item)
            {
                actions[idx] = new QuickAction(item.EntityId, string.IsNullOrEmpty(nameBox.Text) ? item.FriendlyName : nameBox.Text);
                _config.QuickActions = JsonSerializer.Serialize(actions, _jsonOpts);
                _config.Save();
                LoadQuickActionsList();
                dialog.Close();
            }
        };

        deleteBtn.Click += (s, args) =>
        {
            actions.RemoveAt(idx);
            _config.QuickActions = JsonSerializer.Serialize(actions, _jsonOpts);
            _config.Save();
            LoadQuickActionsList();
            dialog.Close();
        };

        dialog.Controls.Add(table);
        ApplyThemeToControls(dialog, _config.Theme);
        dialog.ShowDialog(this);
    }

    private void OnRemoveQuickAction(object? sender, EventArgs e)
    {
        if (_qaList.SelectedIndex < 0)
        {
            MessageBox.Show(Localization.Get("settings_qa_select_first"), "HA DeskLink", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var actions = GetCurrentQuickActions();
        var idx = _qaList.SelectedIndex;
        if (idx < actions.Count)
        {
            actions.RemoveAt(idx);
            _config.QuickActions = JsonSerializer.Serialize(actions, _jsonOpts);
            _config.Save();
            LoadQuickActionsList();
        }
    }

    // ═══════════════════════════════════════════════════════
    // THEME
    // ═══════════════════════════════════════════════════════

    private void ApplyThemeToControls(Form dialog, string theme)
    {
        bool dark = theme == "dark" || (theme == "system" && IsSystemDark());
        if (dark)
        {
            dialog.BackColor = DarkBg;
            dialog.ForeColor = DarkFg;
        }
        foreach (Control c in GetAllControls(dialog))
        {
            if (dark)
            {
                if (c is TextBox || c is ComboBox || c is NumericUpDown || c is ListBox)
                {
                    c.BackColor = DarkInput;
                    c.ForeColor = DarkFg;
                }
            }
            else
            {
                if (c is TextBox || c is ComboBox || c is NumericUpDown || c is ListBox)
                {
                    c.BackColor = SystemColors.Window;
                    c.ForeColor = SystemColors.WindowText;
                }
            }
        }
    }

    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false };

    private class EntityItem
    {
        public string EntityId { get; }
        public string FriendlyName { get; }
        public EntityItem(string entityId, string friendlyName) { EntityId = entityId; FriendlyName = friendlyName; }
        public override string ToString() => $"{FriendlyName} ({EntityId})";
    }

    private void ApplyTheme(string theme)
    {
        _isDark = theme == "dark" || (theme == "system" && IsSystemDark());
        Color bg = _isDark ? DarkBg : LightBg;
        Color fg = _isDark ? DarkFg : LightFg;
        Color inputBg = _isDark ? DarkInput : LightInput;
        Color sidebarBg = _isDark ? DarkBg : LightSidebarBg;
        Color bottomBg = _isDark ? DarkSectionBg : LightBottomBg;

        // Sidebar Hover/Normal Farben für MouseEnter/Leave
        _sidebarNormalBg = sidebarBg;
        _sidebarHoverBg = _isDark ? Color.FromArgb(48, 48, 48) : Color.FromArgb(220, 220, 220);

        // Form
        BackColor = bg;
        ForeColor = fg;

        // Sidebar
        _sidebarPanel.BackColor = sidebarBg;
        _sidebarPanel.ForeColor = fg;

        // Inhaltsbereich
        _contentPanel.BackColor = bg;
        _contentPanel.ForeColor = fg;

        // Section-Panels
        foreach (var section in _sectionPanels)
        {
            section.BackColor = bg;
            section.ForeColor = fg;
        }

        // Bottom Bar
        _bottomPanel.BackColor = bottomBg;
        _bottomPanel.ForeColor = fg;
        _bottomPanel.Invalidate();  // Paint-Event neu auslösen für Top-Border

        // Sidebar-Buttons aktualisieren (ausgewählter = AccentBlue, weiße Schrift)
        for (int i = 0; i < _sidebarButtons.Count; i++)
        {
            if (i == _currentSection)
            {
                _sidebarButtons[i].BackColor = AccentBlue;
                _sidebarButtons[i].ForeColor = Color.White;
            }
            else
            {
                _sidebarButtons[i].BackColor = sidebarBg;
                _sidebarButtons[i].ForeColor = fg;
            }
        }

        // Alle Controls durchlaufen und einfärben
        foreach (Control c in GetAllControls(this))
        {
            // Section-Panels, Sidebar und Bottom-Bar bereits eingefärbt — überspringen
            if (c is Panel p && _sectionPanels.Contains(p))
                continue;
            if (c == _sidebarPanel || c == _bottomPanel || c == _contentPanel)
                continue;

            if (c is TextBox || c is ComboBox || c is NumericUpDown || c is ListBox)
            {
                c.BackColor = inputBg;
                c.ForeColor = fg;
            }
            else if (c is Button btn)
            {
                // Farbige Buttons behalten weiße Schrift
                if (btn.ForeColor != Color.White)
                    btn.ForeColor = fg;
            }
            else if (c is CheckBox cb)
            {
                cb.ForeColor = fg;
            }
            else if (c is Label lbl)
            {
                // Beschreibungs-Labels: Theme-abhängige graue Farbe
                if (lbl.Tag is string tag && tag == "desc")
                {
                    lbl.ForeColor = _isDark ? Color.Gray : Color.FromArgb(100, 100, 100);
                }
                // Graue und AccentBlue Labels nicht überschreiben
                else if (lbl.ForeColor != Color.Gray && lbl.ForeColor != AccentBlue)
                {
                    lbl.ForeColor = fg;
                }
            }
            else if (c is FlowLayoutPanel || c is TableLayoutPanel)
            {
                c.BackColor = bg;
                c.ForeColor = fg;
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
        _intervalBox.Value = Math.Max(10, _config.SensorInterval);  // Minimum 10s erzwingen
        _updateChannelBox.SelectedIndex = _config.UpdateChannel == "prerelease" ? 1 : 0;

        var currentLangIndex = Localization.AvailableLanguages.IndexOf(_config.Language);
        if (currentLangIndex < 0) currentLangIndex = 0;
        _languageBox.SelectedIndex = currentLangIndex;

        _themeBox.SelectedIndex = _config.Theme switch { "light" => 1, "dark" => 2, _ => 0 };

        // Benachrichtigungs-Position
        _notifPosBox.SelectedIndex = _config.NotificationPosition switch
        {
            "bottom_right" => 1, "top_left" => 2, "top_right" => 3, _ => 0
        };

        // Benachrichtigungs-Monitor
        if (_notifMonitorBox.Items.Count > 0)
            _notifMonitorBox.SelectedIndex = Math.Min(_config.NotificationMonitor, _notifMonitorBox.Items.Count - 1);

        // Hotkeys laden
        _hotkeyModBox.SelectedIndex = _config.HotkeyModifiers switch
        {
            "ctrl_shift" => 0, "ctrl_alt" => 1, "ctrl" => 2, "alt" => 3, "shift" => 4, "none" => 5, _ => 0
        };
        var keyIndex = _hotkeyKeyBox.Items.IndexOf(_config.HotkeyKey.ToUpper());
        _hotkeyKeyBox.SelectedIndex = keyIndex >= 0 ? keyIndex : 0;

        _hotkeyDashModBox.SelectedIndex = _config.HotkeyDashboardModifiers switch
        {
            "ctrl_shift" => 0, "ctrl_alt" => 1, "ctrl" => 2, "alt" => 3, "shift" => 4, "none" => 5, _ => 0
        };
        var dashKeyIndex = _hotkeyDashKeyBox.Items.IndexOf(_config.HotkeyDashboardKey.ToUpper());
        _hotkeyDashKeyBox.SelectedIndex = dashKeyIndex >= 0 ? dashKeyIndex : 0;

        _hotkeySettingsModBox.SelectedIndex = _config.HotkeySettingsModifiers switch
        {
            "ctrl_shift" => 0, "ctrl_alt" => 1, "ctrl" => 2, "alt" => 3, "shift" => 4, "none" => 5, _ => 0
        };
        var settingsKeyIndex = _hotkeySettingsKeyBox.Items.IndexOf(_config.HotkeySettingsKey.ToUpper());
        _hotkeySettingsKeyBox.SelectedIndex = settingsKeyIndex >= 0 ? settingsKeyIndex : 0;

        // MQTT-Einstellungen laden
        _mqttEnabledCheck.Checked = _config.MqttEnabled;
        _mqttBrokerBox.Text = _config.MqttBroker;
        _mqttPortBox.Text = _config.MqttPort.ToString();
        _mqttUserBox.Text = _config.MqttUsername;
        _mqttPassBox.Text = _config.MqttPassword;
        _mqttSslCheck.Checked = _config.MqttUseSsl;
        _mqttFallbackBox.Text = _config.MqttBrokerFallback ?? "";
        UpdateMqttStatusLabel();

        // Music-Assistant-Einstellungen laden (Token wurde von Config.Load entschlüsselt)
        _maHostBox.Text = _config.MaHost ?? "";
        _maPortBox.Text = _config.MaPort.ToString();
        _maTokenBox.Text = _config.MaToken ?? "";

        // Streaming (Sendspin)-Einstellungen laden
        _streamEnabledCheck.Checked = _config.SendspinEnabled;
        _streamPlayerNameBox.Text = string.IsNullOrWhiteSpace(_config.SendspinPlayerName)
            ? "HA DeskLink PC"
            : _config.SendspinPlayerName;
        UpdateStreamStatusLabel();
    }

    private void UpdateMqttStatusLabel()
    {
        if (!_mqttEnabledCheck.Checked)
        {
            _mqttStatusLabel.Text = "○ " + Localization.Get("mqtt_disabled");
            _mqttStatusLabel.ForeColor = Color.Gray;
        }
        else if (_config.MqttBroker.Length > 0)
        {
            _mqttStatusLabel.Text = "● " + Localization.Get("mqtt_connected") + $" ({_config.MqttBroker}:{_config.MqttPort})";
            _mqttStatusLabel.ForeColor = SuccessGreen;
        }
        else
        {
            _mqttStatusLabel.Text = "● " + Localization.Get("mqtt_disconnected");
            _mqttStatusLabel.ForeColor = WarningOrange;
        }
    }

    private async void OnMqttTestConnection(object? sender, EventArgs e)
    {
        var broker = _mqttBrokerBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(broker))
        {
            MessageBox.Show(Localization.Get("validation_mqtt_broker_empty"), Localization.Get("validation_title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!int.TryParse(_mqttPortBox.Text.Trim(), out var port) || port < 1 || port > 65535)
        {
            MessageBox.Show(Localization.Get("validation_port_range"), Localization.Get("validation_port_title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var btn = (Button)sender!;
        btn.Enabled = false;
        btn.Text = "🔌 " + Localization.Get("mqtt_testing");
        _mqttStatusLabel.Text = "⏳ " + Localization.Get("mqtt_testing_status");
        _mqttStatusLabel.ForeColor = Color.Gray;

        try
        {
            var result = await System.Threading.Tasks.Task.Run(() =>
                MqttSetupHelper.TestConnectionAsync(broker, port, _mqttUserBox.Text.Trim(), _mqttPassBox.Text, _mqttSslCheck.Checked));

            if (result)
            {
                _mqttStatusLabel.Text = $"✓ {Localization.Get("mqtt_test_success")} ({broker}:{port})";
                _mqttStatusLabel.ForeColor = SuccessGreen;
            }
            else
            {
                _mqttStatusLabel.Text = $"✗ {Localization.Get("mqtt_test_failed")} ({broker}:{port})";
                _mqttStatusLabel.ForeColor = DangerRed;
            }
        }
        catch (Exception ex)
        {
            _mqttStatusLabel.Text = $"✗ {Localization.Get("status_error", ex.Message)}";
            _mqttStatusLabel.ForeColor = DangerRed;
        }
        finally
        {
            btn.Enabled = true;
            btn.Text = "🔌 " + Localization.Get("mqtt_test_connection", "Verbindung testen");
        }
    }

    private async void OnMaTestConnection(object? sender, EventArgs e)
    {
        var host = _maHostBox.Text.Trim();
        var portText = _maPortBox.Text.Trim();
        var token = _maTokenBox.Text;

        if (string.IsNullOrWhiteSpace(host) || !int.TryParse(portText, out var port) || port < 1 || port > 65535)
        {
            _maTestResultLabel.Text = "✗ " + Localization.Get("ma_test_invalid", "Bitte Host und Port angeben.");
            _maTestResultLabel.ForeColor = DangerRed;
            return;
        }

        var btn = sender as Button ?? _maTestBtn;
        btn.Enabled = false;
        btn.Text = "🧪 " + Localization.Get("mqtt_testing", "Teste...");
        _maTestResultLabel.Text = "⏳ " + Localization.Get("mqtt_testing_status", "Teste Verbindung...");
        _maTestResultLabel.ForeColor = Color.Gray;

        try
        {
            var info = await System.Threading.Tasks.Task.Run(() => MaManager.TestConnectionAsync(host, port, token));
            _maTestResultLabel.Text = $"✓ {Localization.Get("mqtt_test_success", "Verbindung erfolgreich!")}" +
                $" – {info.Name} (MA v{info.ServerVersion})";
            _maTestResultLabel.ForeColor = SuccessGreen;
        }
        catch (Exception ex)
        {
            _maTestResultLabel.Text = $"✗ {Localization.Get("mqtt_test_failed", "Verbindung fehlgeschlagen")}: {ex.Message}";
            _maTestResultLabel.ForeColor = DangerRed;
        }
        finally
        {
            btn.Enabled = true;
            btn.Text = "🧪 " + Localization.Get("mqtt_test_connection", "Verbindung testen");
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