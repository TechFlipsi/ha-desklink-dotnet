#nullable enable
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HaDeskLink;

/// <summary>
/// Execute system commands received from Home Assistant.
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
                Process.Start("shutdown", "/r /t 30 /c \"HA DeskLink: PC wird neu gestartet\"");
                break;
            case "hibernate":
                Process.Start("shutdown", "/h");
                break;
            case "lock":
                LockWorkStation();
                break;
            default:
                throw new NotSupportedException($"Unknown command: {command}");
        }
    }

    [DllImport("user32.dll")]
    private static extern bool LockWorkStation();
}