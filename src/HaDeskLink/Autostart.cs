using System;
using System.IO;
using Microsoft.Win32;

namespace HaDeskLink;

/// <summary>
/// Manages Windows autostart (Run registry key).
/// </summary>
public static class Autostart
{
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "HA_DeskLink";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
        return key?.GetValue(AppName) != null;
    }

    public static void Enable()
    {
        var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
        key?.SetValue(AppName, $"\"{exePath}\"");
    }

    public static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
        key?.DeleteValue(AppName, false);
    }
}