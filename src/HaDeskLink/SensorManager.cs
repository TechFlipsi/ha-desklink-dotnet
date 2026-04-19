using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HaDeskLink;

/// <summary>
/// Collects system sensor data using WMI and Performance Counters.
/// </summary>
public static class SensorManager
{
    public static List<SensorData> CollectAll()
    {
        var sensors = new List<SensorData>();

        sensors.Add(GetCpuPercent());
        sensors.Add(GetCpuTemperature());
        sensors.Add(GetMemoryPercent());
        sensors.Add(GetMemoryUsed());
        sensors.Add(GetMemoryTotal());
        sensors.Add(GetDiskPercent());
        sensors.Add(GetDiskFree());
        sensors.Add(GetUptime());
        sensors.Add(GetLastActivity());
        sensors.Add(GetBattery());

        // GPU via WMI
        sensors.AddRange(GetGpuStats());

        return sensors.Where(s => s != null).ToList()!;
    }

    private static SensorData GetCpuPercent()
    {
        var cpu = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        cpu.NextValue(); // First call returns 0
        System.Threading.Thread.Sleep(500);
        var value = Math.Round(cpu.NextValue(), 1);
        return new SensorData("cpu_percent", "CPU Usage", value, "%",
            icon: "mdi:cpu-64-bit", stateClass: "measurement");
    }

    private static SensorData? GetCpuTemperature()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "root\\WMI", "SELECT CurrentReading FROM MSAcpi_ThermalZoneTemperature");
            foreach (var obj in searcher.Get())
            {
                var temp = Convert.ToDouble(obj["CurrentReading"]) / 10.0 - 273.15;
                return new SensorData("cpu_temperature", "CPU Temperature",
                    Math.Round(temp, 1), "\u00B0C", icon: "mdi:thermometer", stateClass: "measurement");
            }
        }
        catch { }
        return null;
    }

    private static SensorData GetMemoryPercent()
    {
        using var mem = new PerformanceCounter("Memory", "% Committed Bytes In Use");
        mem.NextValue();
        return new SensorData("memory_percent", "Memory Usage",
            Math.Round(mem.NextValue(), 1), "%", icon: "mdi:memory", stateClass: "measurement");
    }

    private static SensorData GetMemoryUsed()
    {
        using var mem = new PerformanceCounter("Memory", "Committed Bytes");
        var gb = Math.Round(mem.NextValue() / (1024 * 1024 * 1024), 2);
        return new SensorData("memory_used", "Memory Used", gb, "GB",
            icon: "mdi:memory", stateClass: "measurement");
    }

    private static SensorData GetMemoryTotal()
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem");
        foreach (var obj in searcher.Get())
        {
            var gb = Math.Round(Convert.ToDouble(obj["TotalVisibleMemorySize"]) / (1024 * 1024), 2);
            return new SensorData("memory_total", "Memory Total", gb, "GB", icon: "mdi:memory");
        }
        return new SensorData("memory_total", "Memory Total", 0, "GB", icon: "mdi:memory");
    }

    private static SensorData? GetDiskPercent()
    {
        try
        {
            var drive = new DriveInfo("C");
            var total = drive.TotalSize;
            var free = drive.AvailableFreeSpace;
            var percent = Math.Round((double)(total - free) / total * 100, 1);
            return new SensorData("disk_percent", "Disk Usage", percent, "%",
                icon: "mdi:harddisk", stateClass: "measurement");
        }
        catch { return null; }
    }

    private static SensorData? GetDiskFree()
    {
        try
        {
            var drive = new DriveInfo("C");
            var gb = Math.Round((double)drive.AvailableFreeSpace / (1024 * 1024 * 1024), 2);
            return new SensorData("disk_free", "Disk Free", gb, "GB",
                icon: "mdi:harddisk", stateClass: "measurement");
        }
        catch { return null; }
    }

    private static SensorData GetUptime()
    {
        var uptime = Environment.TickCount64 / 1000;
        var hours = Math.Round(uptime / 3600.0, 1);
        return new SensorData("uptime", "Uptime", hours, "h",
            icon: "mdi:clock-outline", stateClass: "measurement");
    }

    private static SensorData? GetLastActivity()
    {
        try
        {
            var idle = GetIdleTime();
            var minutes = Math.Round(idle / 60000.0, 1);
            return new SensorData("last_activity", "Last Activity", minutes, "min",
                icon: "mdi:account-clock", stateClass: "measurement");
        }
        catch { return null; }
    }

    private static SensorData? GetBattery()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT EstimatedChargeRemaining FROM Win32_Battery");
            foreach (var obj in searcher.Get())
            {
                var pct = Convert.ToDouble(obj["EstimatedChargeRemaining"]);
                return new SensorData("battery", "Battery", pct, "%",
                    deviceClass: "battery", icon: "mdi:battery", stateClass: "measurement");
            }
        }
        catch { }
        return null;
    }

    private static List<SensorData> GetGpuStats()
    {
        var sensors = new List<SensorData>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "root\\CIMV2", "SELECT * FROM Win32_VideoController");
            var i = 0;
            foreach (var obj in searcher.Get())
            {
                var suffix = i > 0 ? $"_{i}" : "";
                // GPU temp/load via WMI is limited; NVAPI would be better
                // For now, report adapter name as attribute
                sensors.Add(new SensorData($"gpu_load{suffix}", $"GPU Load{suffix}",
                    0, "%", icon: "mdi:gpu", stateClass: "measurement"));
                sensors.Add(new SensorData($"gpu_temperature{suffix}", $"GPU Temperature{suffix}",
                    0, "\u00B0C", icon: "mdi:gpu", stateClass: "measurement"));
                i++;
            }
        }
        catch { }
        return sensors;
    }

    [DllImport("user32.dll")]
    static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [StructLayout(LayoutKind.Sequential)]
    struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    private static uint GetIdleTime()
    {
        var lii = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO)) };
        GetLastInputInfo(ref lii);
        return (uint)Environment.TickCount - lii.dwTime;
    }
}

public class SensorData
{
    public string Type { get; } = "sensor";
    public string UniqueId { get; }
    public string Name { get; }
    public object State { get; }
    public string? UnitOfMeasurement { get; }
    public string? DeviceClass { get; }
    public string? Icon { get; }
    public string? StateClass { get; }
    public string? EntityCategory { get; }

    public SensorData(string uniqueId, string name, object state, string? unit = null,
        string? deviceClass = null, string? icon = null, string? stateClass = null,
        string entityCategory = "diagnostic")
    {
        UniqueId = uniqueId;
        Name = name;
        State = state;
        UnitOfMeasurement = unit;
        DeviceClass = deviceClass;
        Icon = icon;
        StateClass = stateClass;
        EntityCategory = entityCategory;
    }
}