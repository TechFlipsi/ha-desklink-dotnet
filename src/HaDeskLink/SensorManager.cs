using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using LibreHardwareMonitor.Hardware;

namespace HaDeskLink;

/// <summary>
/// Collects system sensor data using LibreHardwareMonitor for temperatures
/// and WMI/Performance Counters for everything else.
/// </summary>
public class SensorManager : IDisposable
{
    private readonly Computer _computer;
    private bool _disposed;

    public SensorManager()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsStorageEnabled = true,
            IsBatteryEnabled = true,
        };
        _computer.Open();
    }

    public List<SensorData> CollectAll()
    {
        var sensors = new List<SensorData>();

        // Update LibreHardwareMonitor
        _computer.Accept(new UpdateVisitor());

        sensors.AddRange(GetCpuSensors());
        sensors.AddRange(GetGpuSensors());
        sensors.AddRange(GetMemorySensors());
        sensors.AddRange(GetDiskSensors());
        sensors.Add(GetUptime());
        sensors.Add(GetLastActivity());
        sensors.Add(GetBattery());

        return sensors.Where(s => s != null).ToList()!;
    }

    private List<SensorData> GetCpuSensors()
    {
        var result = new List<SensorData>();
        var cpu = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
        if (cpu == null) return result;

        foreach (var sensor in cpu.Sensors)
        {
            if (sensor.Value == null) continue;

            switch (sensor.SensorType)
            {
                case SensorType.Load when sensor.Name.Contains("Total"):
                    result.Add(new SensorData("cpu_percent", "CPU Usage",
                        Math.Round(sensor.Value.Value, 1), "%",
                        icon: "mdi:cpu-64-bit", stateClass: "measurement"));
                    break;
                case SensorType.Temperature when sensor.Name.Contains("Core") || sensor.Name.Contains("Package"):
                    result.Add(new SensorData("cpu_temperature", "CPU Temperature",
                        Math.Round(sensor.Value.Value, 1), "\u00b0C",
                        icon: "mdi:thermometer", stateClass: "measurement"));
                    break;
            }
        }
        return result;
    }

    private List<SensorData> GetGpuSensors()
    {
        var result = new List<SensorData>();
        var gpu = _computer.Hardware.FirstOrDefault(h =>
            h.HardwareType == HardwareType.GpuNvidia ||
            h.HardwareType == HardwareType.GpuAmd ||
            h.HardwareType == HardwareType.GpuIntel);

        if (gpu == null) return result;

        var suffix = "";
        foreach (var sensor in gpu.Sensors)
        {
            if (sensor.Value == null) continue;

            switch (sensor.SensorType)
            {
                case SensorType.Load when sensor.Name.Contains("Core"):
                    result.Add(new SensorData($"gpu_load{suffix}", $"GPU Load{suffix}",
                        Math.Round(sensor.Value.Value, 1), "%",
                        icon: "mdi:gpu", stateClass: "measurement"));
                    break;
                case SensorType.Temperature:
                    result.Add(new SensorData($"gpu_temperature{suffix}", $"GPU Temperature{suffix}",
                        Math.Round(sensor.Value.Value, 1), "\u00b0C",
                        icon: "mdi:gpu", stateClass: "measurement"));
                    break;
            }
        }
        return result;
    }

    private List<SensorData> GetMemorySensors()
    {
        var result = new List<SensorData>();

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
            foreach (var obj in searcher.Get())
            {
                var totalKB = Convert.ToDouble(obj["TotalVisibleMemorySize"]);
                var freeKB = Convert.ToDouble(obj["FreePhysicalMemory"]);
                var usedGB = Math.Round((totalKB - freeKB) / 1048576.0, 2);
                var totalGB = Math.Round(totalKB / 1048576.0, 2);
                var freeGB = Math.Round(freeKB / 1048576.0, 2);
                var percent = Math.Round((1 - freeKB / totalKB) * 100, 1);

                result.Add(new SensorData("memory_percent", "Memory Usage", percent, "%",
                    icon: "mdi:memory", stateClass: "measurement"));
                result.Add(new SensorData("memory_used", "Memory Used", usedGB, "GB",
                    icon: "mdi:memory", stateClass: "measurement"));
                result.Add(new SensorData("memory_free", "Memory Free", freeGB, "GB",
                    icon: "mdi:memory", stateClass: "measurement"));
                result.Add(new SensorData("memory_total", "Memory Total", totalGB, "GB",
                    icon: "mdi:memory"));
            }
        }
        catch { }

        return result;
    }

    private List<SensorData> GetDiskSensors()
    {
        var result = new List<SensorData>();

        try
        {
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
            {
                var label = drive.Name.TrimEnd('\\');
                var driveKey = label.Replace(":", "").ToLower();

                var total = (double)drive.TotalSize / (1024 * 1024 * 1024);
                var free = (double)drive.AvailableFreeSpace / (1024 * 1024 * 1024);
                var used = total - free;
                var percent = Math.Round(used / total * 100, 1);

                result.Add(new SensorData($"disk_{driveKey}_percent", $"Disk {label} Usage",
                    percent, "%", icon: "mdi:harddisk", stateClass: "measurement"));
                result.Add(new SensorData($"disk_{driveKey}_free", $"Disk {label} Free",
                    Math.Round(free, 2), "GB", icon: "mdi:harddisk", stateClass: "measurement"));
                result.Add(new SensorData($"disk_{driveKey}_used", $"Disk {label} Used",
                    Math.Round(used, 2), "GB", icon: "mdi:harddisk", stateClass: "measurement"));
                result.Add(new SensorData($"disk_{driveKey}_total", $"Disk {label} Total",
                    Math.Round(total, 2), "GB", icon: "mdi:harddisk"));
            }
        }
        catch { }

        return result;
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
            var idle = GetIdleTimeMs();
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

    [DllImport("user32.dll")]
    static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [StructLayout(LayoutKind.Sequential)]
    struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    private static uint GetIdleTimeMs()
    {
        var lii = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO)) };
        GetLastInputInfo(ref lii);
        return (uint)Environment.TickCount - lii.dwTime;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _computer.Close();
            _disposed = true;
        }
    }
}

/// <summary>
/// Visitor that triggers a hardware update in LibreHardwareMonitor.
/// </summary>
public class UpdateVisitor : IVisitor
{
    public void VisitComputer(IComputer computer) { }
    public void VisitHardware(IHardware hardware) { hardware.Update(); }
    public void VisitSensor(ISensor sensor) { }
    public void VisitParameter(IParameter parameter) { }
}