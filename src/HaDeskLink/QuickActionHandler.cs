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
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace HaDeskLink;

/// <summary>
/// Registers a configurable global hotkey. Supports multiple instances with unique IDs.
/// Default: Ctrl+Shift+H. Configurable via HotkeyModifiers and HotkeyKey in settings.
/// </summary>
public class QuickActionHandler : IDisposable
{
    private static int _nextId = 0xC000;

    private readonly int _hotkeyId;
    private readonly IntPtr _hwnd;
    private readonly Action _onHotkey;
    private readonly string _modifiers;
    private readonly string _key;
    private readonly HotkeyMessageFilter _filter;
    private bool _registered;
    private bool _disposed;

    public QuickActionHandler(Action onHotkey, string modifiers = "ctrl_shift", string key = "H")
    {
        _onHotkey = onHotkey;
        _modifiers = modifiers;
        _key = key;
        _hotkeyId = _nextId++;

        // Use the main form's handle for WM_HOTKEY messages
        _hwnd = IntPtr.Zero;
        _filter = new HotkeyMessageFilter(_hotkeyId, _onHotkey);
    }

    public void Start()
    {
        if (_modifiers == "none")
        {
            // Hotkey disabled
            return;
        }

        // We need a window handle to register hotkeys
        // Use an invisible message-only window
        var helper = new NativeWindow();
        helper.AssignHandle(CreateMessageOnlyWindow());
        _hwnd = helper.Handle;

        Application.AddMessageFilter(_filter);

        uint mod = GetModifierFlags(_modifiers);
        uint vk = GetVirtualKey(_key);
        _registered = RegisterHotKey(_hwnd, _hotkeyId, mod, vk);

        if (!_registered)
        {
            // Hotkey may already be registered by another app
            System.Diagnostics.Debug.WriteLine($"[Hotkey] Failed to register hotkey ID {_hotkeyId} ({GetHotkeyDisplay()})");
        }
    }

    /// <summary>
    /// Get the display string for the current hotkey (e.g. "Ctrl+Shift+H").
    /// </summary>
    public string GetHotkeyDisplay()
    {
        var modStr = _modifiers switch
        {
            "ctrl_shift" => "Ctrl+Shift",
            "ctrl_alt" => "Ctrl+Alt",
            "ctrl" => "Ctrl",
            "alt" => "Alt",
            "shift" => "Shift",
            "none" => "",
            _ => "Ctrl+Shift"
        };
        return string.IsNullOrEmpty(modStr) ? _key.ToUpper() : $"{modStr}+{_key.ToUpper()}";
    }

    private static IntPtr CreateMessageOnlyWindow()
    {
        // HWND_MESSAGE = -3 creates a message-only window
        return CreateWindowExW(0, "STATIC", "", 0, 0, 0, 0, 0, new IntPtr(-3), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreateWindowExW(uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    private static uint GetModifierFlags(string modifiers)
    {
        return modifiers switch
        {
            "ctrl_shift" => 0x2 | 0x4,  // MOD_CONTROL | MOD_SHIFT
            "ctrl_alt" => 0x2 | 0x1,     // MOD_CONTROL | MOD_ALT
            "ctrl" => 0x2,                // MOD_CONTROL
            "alt" => 0x1,                 // MOD_ALT
            "shift" => 0x4,               // MOD_SHIFT
            "none" => 0x0,
            _ => 0x2 | 0x4               // Default: Ctrl+Shift
        };
    }

    private static uint GetVirtualKey(string key)
    {
        return key.ToUpper() switch
        {
            "A" => 0x41, "B" => 0x42, "C" => 0x43, "D" => 0x44,
            "E" => 0x45, "F" => 0x46, "G" => 0x47, "H" => 0x48,
            "I" => 0x49, "J" => 0x4A, "K" => 0x4B, "L" => 0x4C,
            "M" => 0x4D, "N" => 0x4E, "O" => 0x4F, "P" => 0x50,
            "Q" => 0x51, "R" => 0x52, "S" => 0x53, "T" => 0x54,
            "U" => 0x55, "V" => 0x56, "W" => 0x57, "X" => 0x58,
            "Y" => 0x59, "Z" => 0x5A,
            "0" => 0x30, "1" => 0x31, "2" => 0x32, "3" => 0x33,
            "4" => 0x34, "5" => 0x35, "6" => 0x36, "7" => 0x37,
            "8" => 0x38, "9" => 0x39,
            "F1" => 0x70, "F2" => 0x71, "F3" => 0x72, "F4" => 0x73,
            "F5" => 0x74, "F6" => 0x75, "F7" => 0x76, "F8" => 0x77,
            "F9" => 0x78, "F10" => 0x79, "F11" => 0x7A, "F12" => 0x7B,
            "SPACE" => 0x20,
            "ENTER" => 0x0D,
            "TAB" => 0x09,
            "ESC" => 0x1B,
            _ => 0x48  // Default: H
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_registered && _hwnd != IntPtr.Zero)
                UnregisterHotKey(_hwnd, _hotkeyId);
            Application.RemoveMessageFilter(_filter);
            _disposed = true;
        }
    }

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    /// <summary>
    /// Message filter to catch WM_HOTKEY (0x0312) in the WinForms message loop.
    /// </summary>
    private class HotkeyMessageFilter : IMessageFilter
    {
        private readonly int _id;
        private readonly Action _callback;
        private DateTime _lastTrigger = DateTime.MinValue;

        public HotkeyMessageFilter(int id, Action callback)
        {
            _id = id;
            _callback = callback;
        }

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg == 0x0312 && m.WParam.ToInt32() == _id)
            {
                // Debounce: ignore if triggered within 300ms
                if ((DateTime.UtcNow - _lastTrigger).TotalMilliseconds < 300)
                    return true;
                _lastTrigger = DateTime.UtcNow;
                _callback.Invoke();
                return true;
            }
            return false;
        }
    }
}