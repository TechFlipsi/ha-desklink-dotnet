using System;
using System.Diagnostics;
using System.IO;
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
            case "screenshot":
                TakeScreenshot();
                break;
            default:
                throw new NotSupportedException($"Unknown command: {command}");
        }
    }

    [DllImport("user32.dll")]
    static extern bool LockWorkStation();

    private static void TakeScreenshot()
    {
        // TODO: Implement screenshot capture and save
        // For now, just log
        Console.WriteLine("Screenshot command received (not yet implemented in C# version)");
    }
}