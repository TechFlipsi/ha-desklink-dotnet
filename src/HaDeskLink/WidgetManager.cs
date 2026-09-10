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
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace HaDeskLink;

/// <summary>
/// Verwaltet Desktop-Widgets: platziert WidgetWindows in die Desktop-Schicht
/// (WorkerW hinter den Icons bzw. Progman auf Win11 24H2), überwacht den
/// Explorer/WorkerW (Recovery-Polling alle 5s) und verteilt HA-Entity-States
/// an die Widgets (Fan-out vom geteilten WebSocket/Polling-Kanal).
///
/// Win32-Routine (Lively-Stil):
///   1. FindWindow("Progman")
///   2. SendMessageTimeout(Progman, 0x052C) → erzeugt WorkerW hinter den Icons
///   3. EnumWindows → SHELLDLL_DefView → WorkerW darunter
///   4. SetParent(widget, host) + SetWindowPos relativ zum Host (MapWindowPoints)
///
/// Win11 24H2-Weiche (Rainmeter System.cpp-Muster):
///   GetProcAddress("GetCurrentMonitorTopologyId") vorhanden → 24H2+,
///   WorkerW existiert dort nicht zuverlässig → Progman als Host verwenden.
/// </summary>
public class WidgetManager : IDisposable
{
    // ── Win32 P/Invoke ─────────────────────────────────────────
    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const int WS_CHILD = 0x40000000;
    private const int WS_POPUP = unchecked((int)0x80000000);
    private const int WS_CAPTION = 0x00C00000;
    private const int WS_THICKFRAME = 0x00040000;
    private const int WS_SYSMENU = 0x00080000;
    private const int WS_MINIMIZEBOX = 0x00020000;
    private const int WS_MAXIMIZEBOX = 0x00010000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;

    private const uint WM_SPAWNWORKERW = 0x052C; // 0x052C: "spawn WorkerW behind icons"
    private const uint SMTO_NORMAL = 0x0000;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const uint SWP_SHOWWINDOW = 0x0040;

    // (Display-Änderungen deckt der 5s-Recovery-Timer ab — WorkerW/Progman
    //  werden bei jeder Prüfung neu validiert.)

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter, string? lpszClass, string? lpszWindow);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, uint fuFlags, uint uTimeout, out IntPtr result);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    /// <summary>SetWindowLong 64-bit-sicher (wie WidgetWindow): GWL_STYLE/GWL_EXSTYLE
    /// brauchen auf x64 den Pointer-Wert — SetWindowLong(W) truncat große Werte.</summary>
    private static int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong)
    {
        if (IntPtr.Size == 8)
        {
            var prev = SetWindowLongPtr64(hWnd, nIndex, (IntPtr)(long)(uint)dwNewLong);
            return unchecked((int)prev.ToInt64());
        }
        return SetWindowLong32(hWnd, nIndex, dwNewLong);
    }

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern int MapWindowPoints(IntPtr hWndFrom, IntPtr hWndTo, ref POINT lpPoint, uint cPoints);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string moduleName);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    // ── Zustand ────────────────────────────────────────────────
    private readonly Config _config;
    private readonly HaApiClient _api;
    private readonly SynchronizationContext? _uiContext;
    private readonly Dictionary<string, WidgetWindow> _widgets = new();
    private IntPtr _hostHwnd;            // WorkerW (pre-24H2) oder Progman (24H2+)
    private bool _useProgmanDirect;      // true = 24H2-Modus
    private System.Threading.Timer? _recoveryTimer;
    private bool _disposed;

    public WidgetManager(Config config, HaApiClient api)
    {
        _config = config;
        _api = api;
        _uiContext = SynchronizationContext.Current;
    }

    // ═════════════════════════════════════════════════════════
    // PUBLIC API
    // ═════════════════════════════════════════════════════════

    /// <summary>Alle Widgets aus Config.Widgets starten (beim App-Start).</summary>
    public void StartAll()
    {
        foreach (var wc in WidgetConfigStore.Parse(_config))
            ShowWidget(wc);
        StartRecoveryWatcher();
    }

    /// <summary>Ein Widget anzeigen (erzeugt Fenster + Attach an Desktop-Host).</summary>
    public void ShowWidget(WidgetConfig wc)
    {
        if (_widgets.TryGetValue(wc.Id, out var existing) && !existing.IsDisposed)
        {
            existing.Visible = true;
            return;
        }

        var window = new WidgetWindow(wc, _api);
        window.WidgetMoved += OnWidgetMoved;
        _widgets[wc.Id] = window;

        // Fenster muss erzeugt sein, bevor SetParent läuft
        window.CreateControl();
        if (!window.IsHandleCreated) _ = window.Handle;

        // Desktop-Host bestimmen (lazy — erst wenn ein Widget wirklich kommt)
        EnsureDesktopHost();

        window.Show();
        AttachToDesktop(window, wc);

        // Initiale States laden (Fire-and-forget)
        _ = LoadInitialStatesAsync(window, wc);
    }

    /// <summary>Ein Widget schließen und aus der Verwaltung entfernen.</summary>
    public void HideWidget(string widgetId)
    {
        if (_widgets.TryGetValue(widgetId, out var window))
        {
            window.WidgetMoved -= OnWidgetMoved;
            window.Close();
            _widgets.Remove(widgetId);
        }
    }

    /// <summary>Alle Widgets neu laden (nach Config-Änderung in Settings).</summary>
    public void ReloadAll()
    {
        foreach (var id in _widgets.Keys.ToList())
            HideWidget(id);
        _widgets.Clear();
        StartAll();
    }

    /// <summary>Alle Widgets sichtbar/unsichtbar schalten (z.B. Toggle im Tray).</summary>
    public void SetAllVisible(bool visible)
    {
        foreach (var w in _widgets.Values)
            w.Visible = visible;
    }

    /// <summary>Drag-Modus für alle Widgets (Positionierung; rechte Maus = Drag).</summary>
    public void SetDragMode(bool enabled)
    {
        foreach (var w in _widgets.Values)
            w.SetDragMode(enabled);
    }

    /// <summary>
    /// Entity-State-Fan-out: wird von DeskLinkApp (WebSocket entity_state_changed
    /// oder SensorInterval-Polling) aufgerufen; verteilt an alle betroffenen Widgets.
    /// Thread-sicher: marshalt auf den UI-Thread.
    /// </summary>
    public void OnEntityStateChanged(string entityId, string state, string? unit)
    {
        if (_uiContext != null)
        {
            _uiContext.Post(_ =>
            {
                try { DispatchState(entityId, state, unit); }
                catch { }
            }, null);
        }
        else
        {
            try { DispatchState(entityId, state, unit); }
            catch { }
        }
    }

    private void DispatchState(string entityId, string state, string? unit)
    {
        foreach (var w in _widgets.Values)
        {
            if (w.IsDisposed) continue;
            w.UpdateEntityState(entityId, state, unit);
        }
    }

    // ═════════════════════════════════════════════════════════
    // WORKERW / PROGMAN HOST (24H2-Weiche)
    // ═════════════════════════════════════════════════════════

    /// <summary>
    /// True wenn Win11 24H2+ ( GetCurrentMonitorTopologyId exportiert).
    /// Rainmeter-Referenz: System.cpp ShouldUseShellWindowAsDesktopIconsHost.
    /// </summary>
    private static bool IsWindows24H2OrLater()
    {
        try
        {
            var hUser32 = GetModuleHandle("user32.dll");
            if (hUser32 == IntPtr.Zero) return false;
            // 24H2 führt GetCurrentMonitorTopologyId ein — wenn vorhanden:
            // der neue Desktop-Icons-Host ist Progman selbst, WorkerW existiert nicht.
            return GetProcAddress(hUser32, "GetCurrentMonitorTopologyId") != IntPtr.Zero;
        }
        catch { return false; }
    }

    /// <summary>
    /// Host-Fenster für die Widget-Schicht bestimmen:
    /// - 24H2+: Progman direkt (WorkerW existiert nicht / ist unzuverlässig)
    /// - sonst: Progman → 0x052C → WorkerW hinter den Desktop-Icons
    /// </summary>
    private void EnsureDesktopHost()
    {
        if (IsWindow(_hostHwnd)) return;

        var progman = FindWindow("Progman", null);
        if (progman == IntPtr.Zero) return; // kein Desktop (Shell ersetzt) — Widgets bleiben top-level

        _useProgmanDirect = IsWindows24H2OrLater();
        if (_useProgmanDirect)
        {
            _hostHwnd = progman;
            return;
        }

        // WorkerW spawnen (Lively-Routine)
        _ = SendMessageTimeout(progman, WM_SPAWNWORKERW, IntPtr.Zero, IntPtr.Zero,
            SMTO_NORMAL, 1000, out _);

        // Top-Level-Fenster mit SHELLDLL_DefView suchen → WorkerW DARUNTER
        IntPtr shellView = IntPtr.Zero, workerW = IntPtr.Zero;
        EnumWindows((hWnd, lParam) =>
        {
            var defView = FindWindowEx(hWnd, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (defView != IntPtr.Zero)
            {
                shellView = hWnd;
                workerW = FindWindowEx(IntPtr.Zero, hWnd, "WorkerW", null);
                return false; // stop
            }
            return true;
        }, IntPtr.Zero);

        _hostHwnd = workerW != IntPtr.Zero ? workerW : progman;
    }

    /// <summary>
    /// Widget in die Desktop-Schicht attachen:
    /// Styles strippen (WS_CHILD), SetParent, WS_EX_TOOLWINDOW (alt-tab-frei),
    /// Position relativ zum Host aus Monitor+Offset (MapWindowPoints).
    /// </summary>
    private void AttachToDesktop(WidgetWindow window, WidgetConfig wc)
    {
        EnsureDesktopHost();
        if (_hostHwnd == IntPtr.Zero) return;

        var hwnd = window.Handle;

        // 1) Styles: Rahmen/Caption weg, WS_CHILD rein
        var style = GetWindowLong(hwnd, GWL_STYLE) & ~(WS_CAPTION | WS_THICKFRAME | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX);
        SetWindowLong(hwnd, GWL_STYLE, (style | WS_CHILD) & ~WS_POPUP);

        // 2) Ex-Styles: ToolWindow (alt-tab-frei) + NoActivate
        var ex = GetWindowLong(hwnd, GWL_EXSTYLE) | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
        SetWindowLong(hwnd, GWL_EXSTYLE, ex);

        // 3) In Host parenten
        SetParent(hwnd, _hostHwnd);

        // 4) Position: Monitor-Arbeitsbereich → Offset → Host-Koordinaten
        var (x, y) = ComputeHostRelativePosition(wc, window.Size);

        // 5) Frame neu berechnen + zeigen (SWP_FRAMECHANGED nach Style-Änderung!)
        SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0,
            SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED | SWP_SHOWWINDOW);

        // 6) Click-Through NACH SetParent neu anwenden (Styles können zurückgesetzt sein)
        window.ApplyClickThrough(wc.ClickThrough);
    }

    /// <summary>
    /// Berechnet die Widget-Position im Host-Koordinatensystem:
   /// Monitor-Rect (screen) → Offset (monitorId, offsetX, offsetY) →
    /// in Host-Koordinaten (MapWindowPoints), da WorkerW bei Multi-Monitor
    /// meist bei (0,0) des virtuellen Desktops liegt, aber nicht muss.
    /// </summary>
    private (int x, int y) ComputeHostRelativePosition(WidgetConfig wc, System.Drawing.Size size)
    {
        var screens = Screen.AllScreens;
        var monitorIndex = Math.Max(0, Math.Min(wc.Monitor, screens.Length - 1));
        var bounds = screens[monitorIndex].Bounds;

        int x = bounds.X + wc.OffsetX;
        int y = bounds.Y + wc.OffsetY;

        // In Host-Koordinaten umrechnen (Host ≠ Screen bei WorkerW)
        if (IsWindow(_hostHwnd) && GetWindowRect(_hostHwnd, out var hostRect))
        {
            var pt = new POINT { X = x, Y = y };
            MapWindowPoints(IntPtr.Zero, _hostHwnd, ref pt, 1); // screen → host
            x = pt.X; y = pt.Y;
        }

        // Grobe Clamp: Widget soll sichtbar bleiben
        var hostWidth = SystemInformation.VirtualScreen.Width;
        var hostHeight = SystemInformation.VirtualScreen.Height;
        if (x < -size.Width + 40) x = -size.Width + 40;
        if (y < 0) y = 0;

        return (x, y);
    }

    // ═════════════════════════════════════════════════════════
    // RECOVERY: EXPLORER-RESTART / WORKERW-WATCHER
    // ═════════════════════════════════════════════════════════

    /// <summary>
    /// Polling-Watcher: prüft alle 5s ob der Host (WorkerW) noch existiert.
    /// Explorer-Restart zerstört WorkerW → alle Widgets fallen mit raus.
    /// Dann: Host neu bestimmen und alle Widgets RE-ATTACHEN
    /// (Styles/Click-Through müssen nach SetParent erneut gesetzt werden).
    /// </summary>
    private void StartRecoveryWatcher()
    {
        _recoveryTimer?.Dispose();
        _recoveryTimer = new System.Threading.Timer(_ =>
        {
            try
            {
                if (_widgets.Count == 0) return;

                if (_useProgmanDirect)
                {
                    // 24H2: Progman überlebt Explorer-Restarts — nur Existenz prüfen
                    var progman = FindWindow("Progman", null);
                    if (progman == IntPtr.Zero) return;
                    if (_hostHwnd != progman)
                    {
                        _hostHwnd = progman;
                        ReattachAll();
                    }
                    return;
                }

                // Pre-24H2: WorkerW noch da?
                if (IsWindow(_hostHwnd)) return;

                // WorkerW weg (Explorer-Restart) → neu spawnen + Re-Attach
                _hostHwnd = IntPtr.Zero;
                EnsureDesktopHost();
                if (_hostHwnd != IntPtr.Zero)
                    ReattachAll();
            }
            catch { }
        }, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    private void ReattachAll()
    {
        // Läuft direkt auf dem Recovery-Timer-Thread: FindWindow/SetParent/
        // SetWindowPos arbeiten nur auf HWNDs und sind thread-sicher — ein
        // Marshaling auf den UI-Thread (uiContext.Post) würde stattdessen die
        // WinForms-Message-Loop für die Dauer des kompletten Re-Attach blockieren
        // (Explorer-Restart = mehrere Fenster, spürbarer UI-Freeze).
        List<KeyValuePair<string, WidgetWindow>> snapshot;
        try { snapshot = _widgets.ToList(); }
        catch { return; } // Snapshot kollidierte mit Mutation — nächster Tick erneut
        foreach (var kv in snapshot)
        {
            try
            {
                if (kv.Value.IsDisposed) continue;
                AttachToDesktop(kv.Value, FindWidgetConfig(kv.Key));
            }
            catch { }
        }
    }

    private WidgetConfig FindWidgetConfig(string widgetId)
    {
        return WidgetConfigStore.Parse(_config).FirstOrDefault(w => w.Id == widgetId)
               ?? new WidgetConfig { Id = widgetId };
    }

    // ═════════════════════════════════════════════════════════
    // DATEN: INITIALE STATES + BEWEGUNGEN SPEICHERN
    // ═════════════════════════════════════════════════════════

    /// <summary>
    /// Initiale Entity-States für ein neues Widget via REST laden
    /// (danach kommt alles weitere über den WebSocket-Fan-out).
    /// </summary>
    private async System.Threading.Tasks.Task LoadInitialStatesAsync(WidgetWindow window, WidgetConfig wc)
    {
        try
        {
            var entityIds = new List<string>();
            if (!string.IsNullOrEmpty(wc.EntityId)) entityIds.Add(wc.EntityId);
            entityIds.AddRange(wc.Entities.Select(e => e.EntityId));

            foreach (var entityId in entityIds.Distinct())
            {
                var state = await _api.GetEntityStateAsync(entityId);
                if (state.HasValue)
                {
                    var (st, unit) = state.Value;
                    window.UpdateEntityState(entityId, st, unit);
                }
            }
            window.RefreshAllToggles();
        }
        catch { }
    }

    /// <summary>
    /// Drag-Ende: Position als Monitor+Offset zurück in Config speichern.
    /// </summary>
    private void OnWidgetMoved(string widgetId, int newLeft, int newTop)
    {
        // Left/Top des Forms sind HOST-relativ (Fenster ist in WorkerW geparentet) —
        // erst nach Screen konvertieren, dann Monitor+Offset ableiten.
        System.Drawing.Point screenPoint;
        if (IsWindow(_hostHwnd))
        {
            var pt = new POINT { X = newLeft, Y = newTop };
            MapWindowPoints(_hostHwnd, IntPtr.Zero, ref pt, 1); // host → screen
            screenPoint = new System.Drawing.Point(pt.X, pt.Y);
        }
        else
        {
            screenPoint = new System.Drawing.Point(newLeft, newTop);
        }

        var monitor = Screen.FromPoint(screenPoint);
        var bounds = monitor.Bounds;

        var offsetX = screenPoint.X - bounds.X;
        var offsetY = screenPoint.Y - bounds.Y;

        // Monitor-Index bestimmen
        var screens = Screen.AllScreens;
        int monitorIndex = 0;
        for (int i = 0; i < screens.Length; i++)
        {
            if (screens[i].Bounds == bounds) { monitorIndex = i; break; }
        }

        WidgetConfigStore.UpdatePosition(_config, widgetId, monitorIndex, offsetX, offsetY);
    }

    // ═════════════════════════════════════════════════════════
    // DISPOSE
    // ═════════════════════════════════════════════════════════

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _recoveryTimer?.Dispose();
        foreach (var id in _widgets.Keys.ToList())
        {
            try { _widgets[id].Close(); }
            catch { }
        }
        _widgets.Clear();
    }
}

/// <summary>
/// (De)Serialisierung der Widget-Liste in Config.Widgets (JSON-Array).
/// Format: [{id, type, entityId, entities, name, monitor, offsetX, offsetY, clickThrough}]
/// </summary>
public static class WidgetConfigStore
{
    private static readonly System.Text.Json.JsonSerializerOptions _opts = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase) }
    };

    public static List<WidgetConfig> Parse(Config config)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(config.Widgets)) return new();
            var doc = System.Text.Json.JsonDocument.Parse(config.Widgets);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array) return new();

            var result = new List<WidgetConfig>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var wc = new WidgetConfig
                {
                    Id = GetString(el, "id") ?? Guid.NewGuid().ToString("N")[..8],
                    Type = ParseType(GetString(el, "type")),
                    EntityId = GetString(el, "entityId") ?? "",
                    Name = GetString(el, "name") ?? "",
                };
                if (el.TryGetProperty("monitor", out var mon) && mon.ValueKind == System.Text.Json.JsonValueKind.Number)
                    wc.Monitor = mon.GetInt32();
                if (el.TryGetProperty("offsetX", out var ox) && ox.ValueKind == System.Text.Json.JsonValueKind.Number)
                    wc.OffsetX = ox.GetInt32();
                if (el.TryGetProperty("offsetY", out var oy) && oy.ValueKind == System.Text.Json.JsonValueKind.Number)
                    wc.OffsetY = oy.GetInt32();
                if (el.TryGetProperty("clickThrough", out var ct) && ct.ValueKind == System.Text.Json.JsonValueKind.True)
                    wc.ClickThrough = true;

                if (el.TryGetProperty("entities", out var ents) && ents.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var e in ents.EnumerateArray())
                    {
                        var eid = GetString(e, "entityId") ?? "";
                        if (!string.IsNullOrEmpty(eid))
                            wc.Entities.Add(new WidgetEntity(eid, GetString(e, "name") ?? eid));
                    }
                }
                result.Add(wc);
            }
            return result;
        }
        catch
        {
            return new();
        }
    }

    public static void Save(Config config, List<WidgetConfig> widgets)
    {
        config.Widgets = System.Text.Json.JsonSerializer.Serialize(widgets, _opts);
        config.Save();
    }

    /// <summary>Position eines Widgets aktualisieren (nach Drag) und persistieren.</summary>
    public static void UpdatePosition(Config config, string widgetId, int monitor, int offsetX, int offsetY)
    {
        var widgets = Parse(config);
        var w = widgets.FirstOrDefault(x => x.Id == widgetId);
        if (w == null) return;
        w.Monitor = monitor;
        w.OffsetX = offsetX;
        w.OffsetY = offsetY;
        Save(config, widgets);
    }

    private static string? GetString(System.Text.Json.JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String
            ? v.GetString()
            : null;

    private static WidgetType ParseType(string? s) => s switch
    {
        "sensor" or "sensorCard" => WidgetType.SensorCard,
        "toggle" or "toggleCard" => WidgetType.ToggleCard,
        "multiToggle" or "multiToggleCard" => WidgetType.MultiToggleCard,
        _ => WidgetType.SensorCard
    };
}