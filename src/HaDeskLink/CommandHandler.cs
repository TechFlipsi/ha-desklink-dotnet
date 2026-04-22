
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
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HaDeskLink;

/// <summary>
/// Execute system commands received from Home Assistant notifications.
/// </summary>
public static class CommandHandler
{
    public static void Execute(string command)
    {
        switch (command.ToLowerInvariant())
        {
            case "shutdown":
                Process.Start("shutdown", "/s /t 30 /c \"HA DeskLink: PC wird heruntergefahren\"");
                break;
            case "restart":
            case "reboot":
                Process.Start("shutdown", "/r /t 30 /c \"HA DeskLink: PC wird neu gestartet\"");
                break;
            case "hibernate":
            case "sleep":
                Process.Start("shutdown", "/h");
                break;
            case "lock":
                LockWorkStation();
                break;
            case "mute":
                ToggleMute();
                break;
            case "volume_up":
                VolumeUp();
                break;
            case "volume_down":
                VolumeDown();
                break;
            case "monitor_off":
                MonitorOff();
                break;
            case "monitor_on":
                MonitorOn();
                break;
            case "screenshot":
                TakeScreenshot();
                break;
            default:
                throw new NotSupportedException($"Unbekannter Befehl: {command}");
        }
    }

    [DllImport("user32.dll")]
    private static extern bool LockWorkStation();

    // Volume control via key simulation
    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

    private const byte VK_VOLUME_MUTE = 0xAD;
    private const byte VK_VOLUME_UP = 0xAF;
    private const byte VK_VOLUME_DOWN = 0xAE;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    private static void ToggleMute()
    {
        keybd_event(VK_VOLUME_MUTE, 0, 0, 0);
        keybd_event(VK_VOLUME_MUTE, 0, KEYEVENTF_KEYUP, 0);
    }

    private static void VolumeUp()
    {
        for (int i = 0; i < 5; i++) // 5 presses = ~10% increase
        {
            keybd_event(VK_VOLUME_UP, 0, 0, 0);
            keybd_event(VK_VOLUME_UP, 0, KEYEVENTF_KEYUP, 0);
        }
    }

    private static void VolumeDown()
    {
        for (int i = 0; i < 5; i++)
        {
            keybd_event(VK_VOLUME_DOWN, 0, 0, 0);
            keybd_event(VK_VOLUME_DOWN, 0, KEYEVENTF_KEYUP, 0);
        }
    }

    // Monitor control
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    private const uint WM_SYSCOMMAND = 0x0112;
    private readonly static IntPtr SC_MONITORPOWER = (IntPtr)0xF170;
    private readonly static IntPtr HWND_BROADCAST = (IntPtr)0xFFFF;

    private static void MonitorOff()
    {
        SendMessage(HWND_BROADCAST, WM_SYSCOMMAND, SC_MONITORPOWER, (IntPtr)2);
    }

    private static void MonitorOn()
    {
        SendMessage(HWND_BROADCAST, WM_SYSCOMMAND, SC_MONITORPOWER, (IntPtr)(-1));
        // Also move mouse to wake monitor
        keybd_event(0, 0, 0, 0);
    }

    private static void TakeScreenshot()
    {
        // Use built-in Windows screenshot (Win+Shift+S)
        keybd_event(0x5B, 0, 0, 0); // Win down
        keybd_event(0x10, 0, 0, 0); // Shift down
        keybd_event(0x53, 0, 0, 0); // S down
        keybd_event(0x53, 0, KEYEVENTF_KEYUP, 0); // S up
        keybd_event(0x10, 0, KEYEVENTF_KEYUP, 0); // Shift up
        keybd_event(0x5B, 0, KEYEVENTF_KEYUP, 0); // Win up
    }
}