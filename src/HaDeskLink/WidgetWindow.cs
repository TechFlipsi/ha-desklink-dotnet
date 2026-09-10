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
using System.Windows.Forms;

namespace HaDeskLink;

/// <summary>
/// Widget-Typ: Sensorkarte (Name + Live-Wert), Einzelschalter oder Multischalter (2-4).
/// </summary>
public enum WidgetType
{
    SensorCard = 0,
    ToggleCard = 1,
    MultiToggleCard = 2
}

/// <summary>
/// Ein Desktop-Widget. Rahmenloses WinForms-Form, das vom WidgetManager
/// in die WorkerW/Progman-Desktop-Schicht gesetzt wird.
/// - PerMonitorV2-ready: WM_DPICHANGED wird behandelt (Positionen werden
///   vom Manager als Monitor+Offset verwaltet, nicht in absoluten Pixeln).
/// - Klick-Durchlässigkeit pro Widget konfigurierbar (WS_EX_TRANSPARENT).
/// - Drag-Modus: rechte Maustaste zieht das Widget (Position wird über
///   das WidgetMoved-Event an den Manager gemeldet).
/// </summary>
public class WidgetWindow : Form
{
    // ── Dark Theme (wie QuickActionWindow/SettingsWindow) ─────────
    private static readonly Color CardBg = Color.FromArgb(0x1A, 0x1A, 0x2E);   // #1A1A2E
    private static readonly Color CardPanel = Color.FromArgb(0x16, 0x21, 0x3E); // #16213E
    private static readonly Color Accent = Color.FromArgb(0x0F, 0x34, 0x60);    // #0F3460
    private static readonly Color TextMain = Color.FromArgb(230, 230, 235);
    private static readonly Color TextDim = Color.FromArgb(150, 150, 160);
    private static readonly Color On = Color.FromArgb(0, 160, 110);
    private static readonly Color Off = Color.FromArgb(90, 90, 105);

    // ── Win32: klick-durchlässig (WS_EX_TRANSPARENT) ─────────────
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WM_MOUSEACTIVATE = 0x0021;
    private const int MA_NOACTIVATE = 3;
    private const int WM_DPICHANGED = 0x02E0;
    private const int WM_DPICHANGED_AFTERPARENT = 0x02E3;

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    /// <summary>
    /// SetWindowLong 64-bit-sicher: auf x64-Prozessen muss SetWindowLongPtr(64) her,
    /// sonst truncat der int-Pfad GWL_EXSTYLE/GWL_STYLE (WS_POPUP sitzt in Bit 31).
    /// Styles sind 32-Bit-Werte → zero-extenden, IntPtr(int) würde sign-extenden.
    /// </summary>
    private static int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong)
    {
        if (IntPtr.Size == 8)
        {
            var prev = SetWindowLongPtr64(hWnd, nIndex, (IntPtr)(long)(uint)dwNewLong);
            return unchecked((int)prev.ToInt64());
        }
        return SetWindowLong32(hWnd, nIndex, dwNewLong);
    }

    // ── Widget-Zustand ───────────────────────────────────────────
    private readonly WidgetConfig _config;
    private readonly HaApiClient _api;

    private Label _nameLabel = null!;
    private Label _valueLabel = null!;
    private Panel _cardPanel = null!;
    private TableLayoutPanel _cardLayout = null!;

    /// <summary>Ein Schalter: entityId → Button + State-Label (MultiToggle: 2-4).</summary>
    private class ToggleItem
    {
        public string EntityId = "";
        public string Name = "";
        public Button Button = null!;
        public string State = "unknown";
        public bool Busy;
    }

    private readonly List<ToggleItem> _toggles = new();

    // ── Drag-Modus (rechte Maustaste) ────────────────────────────
    private bool _dragging;
    private Point _dragStart;
    private bool _dragMode; // true = Drag-Modus aktiv (vom Manager beim Positionieren gesetzt)
    private bool _clickThroughDesired; // Gewünschter Zustand — WinForms wirft Ex-Styles bei RecreateHandle weg

    /// <summary>Feuert, wenn der User das Widget per Drag verschoben hat.
    /// Args: (widgetId, newOffsetX, newOffsetY) — Offset relativ zum Monitor-Desktop.</summary>
    public event Action<string, int, int>? WidgetMoved;

    public WidgetWindow(WidgetConfig config, HaApiClient api)
    {
        _config = config;
        _api = api;

        // ── Form-Grundstil: rahmenlos, kein Taskbar, kein Fokus ──
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        MinimizeBox = false;
        MaximizeBox = false;
        TopLevel = true;
        DoubleBuffered = true;

        // Größe je nach Typ
        var (w, h) = GetDefaultSize(config.Type);
        Size = new Size(w, h);
        MaximumSize = new Size(w * 3, h * 3);
        MinimumSize = new Size(Math.Max(120, w / 2), Math.Max(70, h / 2));

        BackColor = CardBg;

        BuildCard();
        // Ex-Styles sind am frischen Handle noch nicht gesetzt UND gehen bei jedem
        // RecreateHandle verloren (WinForms recreatet bei diversen Property-
        // Änderungen) → Wunsch-Zustand cachen und bei HandleCreated nachziehen.
        HandleCreated += (s, e) => ApplyClickThrough(_clickThroughDesired);
        ApplyClickThrough(config.ClickThrough);
    }

    /// <summary>Standardgröße je Widget-Typ (logische Pixel, PerMonitorV2 skaliert mit).</summary>
    public static (int width, int height) GetDefaultSize(WidgetType type) => type switch
    {
        WidgetType.SensorCard => (200, 110),
        WidgetType.ToggleCard => (200, 100),
        _ => (320, 220)
    };

    // ═════════════════════════════════════════════════════════
    // UI-AUFBAU
    // ═════════════════════════════════════════════════════════

    private void BuildCard()
    {
        _cardPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = CardPanel,
            Padding = new Padding(14),
        };
        // Abgerundete Optik: 1px-Rand in Accent
        _cardPanel.Paint += (s, e) =>
        {
            using var pen = new Pen(Accent, 1.5f);
            e.Graphics.DrawRectangle(pen, 0.75f, 0.75f, _cardPanel.Width - 1.5f, _cardPanel.Height - 1.5f);
        };

        switch (_config.Type)
        {
            case WidgetType.SensorCard:
                BuildSensorCard();
                break;
            case WidgetType.ToggleCard:
                BuildToggleCard();
                break;
            case WidgetType.MultiToggleCard:
                BuildMultiToggleCard();
                break;
        }

        Controls.Add(_cardPanel);
    }

    private void BuildSensorCard()
    {
        _cardLayout = MakeCardLayout(3);
        _nameLabel = MakeNameLabel(_config.Name, 9.5f);
        _valueLabel = new Label
        {
            Text = "…",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 17f, FontStyle.Bold),
            ForeColor = TextMain,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _cardLayout.Controls.Add(_nameLabel, 0, 0);
        _cardLayout.Controls.Add(_valueLabel, 0, 1);
        _cardLayout.Controls.Add(new Label { Dock = DockStyle.Fill, Height = 4 }, 0, 2); // Flex
        _cardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        _cardLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _cardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 4));
        _cardPanel.Controls.Add(_cardLayout);
    }

    private void BuildToggleCard()
    {
        _cardLayout = MakeCardLayout(2);
        _nameLabel = MakeNameLabel(_config.Name, 10f);

        var item = AddToggleRow(_config.EntityId, _config.Name);

        _cardLayout.Controls.Add(_nameLabel, 0, 0);
        _cardLayout.Controls.Add(item.Button, 0, 1);
        _cardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        _cardLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _cardPanel.Controls.Add(_cardLayout);
    }

    private void BuildMultiToggleCard()
    {
        _cardLayout = MakeCardLayout(_config.Entities.Count);
        _nameLabel = MakeNameLabel(_config.Name, 9.5f);
        _cardLayout.Controls.Add(_nameLabel, 0, 0);
        _cardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));

        var idx = 1;
        foreach (var (entityId, name) in _config.Entities)
        {
            var item = AddToggleRow(entityId, name);
            _cardLayout.Controls.Add(item.Button, 0, idx++);
            _cardLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 33));
        }
        _cardPanel.Controls.Add(_cardLayout);
    }

    private static TableLayoutPanel MakeCardLayout(int rowCount)
    {
        return new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = rowCount,
            Margin = new Padding(0),
        };
    }

    private static Label MakeNameLabel(string text, float size)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", size, FontStyle.Bold),
            ForeColor = TextDim,
            TextAlign = ContentAlignment.MiddleLeft,
        };
    }

    private ToggleItem AddToggleRow(string entityId, string fallbackName)
    {
        var item = new ToggleItem
        {
            EntityId = entityId,
            Name = fallbackName,
            Button = new Button
            {
                Text = "…",
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                BackColor = Off,
                ForeColor = TextMain,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Margin = new Padding(0, 4, 0, 4),
                Tag = entityId,
            },
        };
        item.Button.FlatAppearance.BorderSize = 0;
        item.Button.Click += async (s, e) => await OnToggleClicked(item);
        _toggles.Add(item);
        return item;
    }

    // ═════════════════════════════════════════════════════════
    // Klick-Durchlässigkeit / STYLES
    // ═════════════════════════════════════════════════════════

    /// <summary>
    /// WS_EX_TRANSPARENT pro Widget setzen/entfernen.
    /// Muss NACH dem Re-Attach (SetParent) erneut laufen, da SetParent Styles zurücksetzen kann.
    /// </summary>
    public void ApplyClickThrough(bool clickThrough)
    {
        _clickThroughDesired = clickThrough; // Wunsch-Zustand merken (RecreateHandle-Verlust ausgleichen)
        if (!IsHandleCreated) return;
        var style = GetWindowLong(Handle, GWL_EXSTYLE);
        if (clickThrough)
        {
            // WS_EX_TRANSPARENT ist NUR in Kombination mit WS_EX_LAYERED wirksam
            // (Hit-Testing läuft über die LAYERED-Oberfläche) — nie allein setzen.
            SetWindowLong(Handle, GWL_EXSTYLE, style | WS_EX_TRANSPARENT | WS_EX_LAYERED);
        }
        else
        {
            // TRANSPARENT raus; LAYERED darf bleiben (mit WS_CHILD unkritisch)
            SetWindowLong(Handle, GWL_EXSTYLE, style & ~WS_EX_TRANSPARENT);
        }
    }

    // Kein Fokus-Stehlen beim Anklicken (interaktive Widgets)
    protected override void WndProc(ref Message m)
    {
        // WM_MOUSEACTIVATE → MA_NOACTIVATE: Klick aktiviert das Fenster nicht
        if (m.Msg == WM_MOUSEACTIVATE)
        {
            m.Result = (IntPtr)MA_NOACTIVATE;
            return;
        }
        // WM_DPICHANGED (PerMonitorV2): Form skaliert automatisch (WinForms
        // verarbeitet die Botschaft), Position/Größe werden vom WidgetManager
        // über Monitor+Offset neu berechnet — hier nur Neuzeichnen anstoßen.
        if (m.Msg == WM_DPICHANGED || m.Msg == WM_DPICHANGED_AFTERPARENT)
        {
            base.WndProc(ref m);
            Invalidate(true);
            return;
        }
        base.WndProc(ref m);
    }

    // ═════════════════════════════════════════════════════════
    // DRAG-MODUS (rechte Maustaste = Positionieren)
    // ═════════════════════════════════════════════════════════

    /// <summary>
    /// Drag-Modus aktivieren (vom WidgetManager für Positionierungs-Phase).
    /// Im Drag-Modus wird die rechte Maustaste zum Verschieben benutzt und
    /// Klick-Durchlässigkeit temporär aufgehoben.
    /// </summary>
    public void SetDragMode(bool enabled)
    {
        if (_dragMode == enabled) return;
        _dragMode = enabled;
        if (enabled)
        {
            ApplyClickThrough(false);
            BackColor = Accent; // visuelles Feedback: Drag-Modus an
            // Kind-Controls deaktivieren: (a) verhindert versehentliche Toggle-
            // Klicks im Positionierungs-Modus, (b) disabled Controls sind
            // maus-durchlässig zum Parent — ohne das käme WM_RBUTTONDOWN nie am
            // Form an (OnMouseDown), weil _cardPanel (Dock=Fill) die gesamte
            // Client-Fläche belegt und WinForms-Maus-Events nicht bubbeln.
            _cardPanel.Enabled = false;
        }
        else
        {
            ApplyClickThrough(_config.ClickThrough);
            BackColor = CardBg;
            _cardPanel.Enabled = true;
        }
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_dragging)
        {
            _dragging = false;
            Capture = false; // Mouse-Capture am Drag-Ende freigeben
            Cursor = Cursors.Default;
            // Neue Position an den Manager melden → speichert Monitor+Offset
            WidgetMoved?.Invoke(_config.Id, Left, Top);
        }
    }

    /// <summary>
    /// Drag auch bei Capture-Verlust sauber beenden (Alt+Tab, Popup, anderer
    /// Fensterwechsel) — sonst bliebe das Widget im Drag-Lock, obwohl keine
    /// Maustaste mehr gehalten wird.
    /// </summary>
    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (_dragging && !Capture)
        {
            _dragging = false;
            Cursor = Cursors.Default;
            WidgetMoved?.Invoke(_config.Id, Left, Top);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging)
        {
            var dx = MousePosition.X - _dragStart.X;
            var dy = MousePosition.Y - _dragStart.Y;
            Left += dx;
            Top += dy;
            _dragStart = MousePosition;
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Right && _dragMode)
        {
            _dragging = true;
            _dragStart = MousePosition;
            Cursor = Cursors.SizeAll;
            // Mouse-Capture NUR beim Drag-Start (rechte Taste, Drag-Modus): hält
            // den Drag über Child-Controls/Maus-Leave hinweg, OnMouseUp feuert
            // garantiert am Form. Bei linker Taste würde Capture die Klicks der
            // Toggle-Buttons schlucken — daher bewusst nur im Drag-Zweig.
            Capture = true;
        }
    }

    // Buttons sollen beim Drag nicht klicken
    protected override void OnClick(EventArgs e)
    {
        if (!_dragging) base.OnClick(e);
    }

    // ═════════════════════════════════════════════════════════
    // LIVE-DATEN
    // ═════════════════════════════════════════════════════════

    /// <summary>
    /// Entity-Status von außen setzen (Sensor-Fan-out über DeskLinkApp).
    /// SensorCard: zeigt State + Unit. Toggle: aktualisiert Button-Farbe/Text.
    /// Wird vom WidgetManager auf dem UI-Thread aufgerufen (BeginInvoke).
    /// </summary>
    public void UpdateEntityState(string entityId, string state, string? unit)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => UpdateEntityStateCore(entityId, state, unit)));
            return;
        }
        UpdateEntityStateCore(entityId, state, unit);
    }

    private void UpdateEntityStateCore(string entityId, string state, string? unit)
    {
        if (_config.Type == WidgetType.SensorCard)
        {
            if (entityId != _config.EntityId) return;
            var text = state;
            if (!string.IsNullOrEmpty(unit)) text += $" {unit}";
            _valueLabel.Text = text;
            _valueLabel.ForeColor = ParseStateColor(state);
            return;
        }

        // Toggle / MultiToggle
        var item = _toggles.FirstOrDefault(t => t.EntityId == entityId);
        if (item == null || item.Busy) return;
        item.State = state;
        item.Busy = false;
        RenderToggle(item);
    }

    private static Color ParseStateColor(string state) => state switch
    {
        "on" or "open" or "opening" or "home" or "playing" => On,
        "off" or "closed" or "closing" or "away" or "idle" or "paused" or "standby" => TextDim,
        "unavailable" or "unknown" => Color.FromArgb(200, 60, 60),
        _ => TextMain
    };

    private void RenderToggle(ToggleItem item)
    {
        var isOn = item.State is "on" or "open" or "playing" or "home";
        item.Button.BackColor = isOn ? On : Off;
        item.Button.ForeColor = isOn ? Color.White : TextMain;
        item.Button.Text = $"{item.Name}  {(isOn ? "● ON" : "○ OFF")}";
    }

    private async System.Threading.Tasks.Task OnToggleClicked(ToggleItem item)
    {
        if (item.Busy) return;
        item.Busy = true;
        item.Button.Text = $"{item.Name}  ⏳";
        try
        {
            await _api.ToggleEntityAsync(item.EntityId);
            item.State = item.State == "on" ? "off" : "on"; // optimistisch
            RenderToggle(item);
            // Echten Zustand asynchron nachziehen (Manager-Fan-out liefert ihn)
        }
        catch
        {
            item.Button.Text = $"{item.Name}  ✗";
        }
        finally
        {
            item.Busy = false;
        }
    }

    /// <summary>Alle Buttons initial rendern (nach State-Load).</summary>
    public void RefreshAllToggles()
    {
        foreach (var item in _toggles)
            RenderToggle(item);
    }
}

/// <summary>
/// Konfiguration eines einzelnen Widgets (wird als JSON in Config.Widgets gespeichert).
/// </summary>
public class WidgetConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public WidgetType Type { get; set; } = WidgetType.SensorCard;
    /// <summary>Haupt-Entity (SensorCard, ToggleCard). Leer bei MultiToggle.</summary>
    public string EntityId { get; set; } = "";
    /// <summary>2-4 Entities für MultiToggleCard.</summary>
    public List<WidgetEntity> Entities { get; set; } = new();
    public string Name { get; set; } = "";
    /// <summary>Monitor-Index (0 = primär).</summary>
    public int Monitor { get; set; } = 0;
    public int OffsetX { get; set; } = 40;
    public int OffsetY { get; set; } = 40;
    /// <summary>Klick-Durchlässigkeit (WS_EX_TRANSPARENT).</summary>
    public bool ClickThrough { get; set; } = false;

    public WidgetConfig() { }
}

public class WidgetEntity
{
    public string EntityId { get; set; } = "";
    public string Name { get; set; } = "";

    public WidgetEntity() { }
    public WidgetEntity(string entityId, string name)
    {
        EntityId = entityId;
        Name = name;
    }

    public void Deconstruct(out string entityId, out string name)
    {
        entityId = EntityId;
        name = Name;
    }
}