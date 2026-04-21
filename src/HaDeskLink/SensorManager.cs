#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using LibreHardwareMonitor.Hardware;

namespace HaDeskLink;

/// <summary>
/// Collects system sensor data using LibreHardwareMonitor for temperatures
/// and WMI/DriveInfo for everything else.
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
        try { _computer.Open(); }
        catch { /* LibreHardwareMonitor native DLLs may fail in single-file mode */ }
    }

    public List<SensorData> CollectAll()
    {
        var sensors = new List<SensorData>();

        try { _computer.Accept(new UpdateVisitor()); }
        catch { }

        sensors.AddRange(GetCpuSensors());
        sensors.AddRange(GetGpuSensors());
        sensors.AddRange(GetMemorySensors());
        sensors.AddRange(GetDiskSensors());
        sensors.Add(GetUptime());

        var lastAct = GetLastActivity();
        if (lastAct != null) sensors.Add(lastAct);

        var battery = GetBattery();
        if (battery != null) sensors.Add(battery);

        sensors.Add(GetIpAddress());
        sensors.Add(GetConnectivity());
        sensors.Add(GetProcessCount());
        sensors.Add(GetPageFile());
        sensors.Add(GetActiveWindow());

        var wifi = GetWifiSsid();
        if (wifi != null) sensors.Add(wifi);
        var wifiSignal = GetWifiSignal();
        if (wifiSignal != null) sensors.Add(wifiSignal);

        // CPU clock speed and fan speeds from LibreHardwareMonitor
        sensors.AddRange(GetCpuClockSensors());
        sensors.AddRange(GetFanSensors());

        // Network throughput
        sensors.AddRange(GetNetworkSensors());

        return sensors;
    }

    private List<SensorData> GetCpuSensors()
    {
        var result = new List<SensorData>();
        var cpu = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
        if (cpu == null) return result;

        foreach (var sensor in cpu.Sensors)
        {
            if (sensor.Value == null) continue;

            if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("Total"))
            {
                result.Add(new SensorData("cpu_percent", "CPU Usage",
                    Math.Round(sensor.Value.Value, 1), "%",
                    icon: "mdi:cpu-64-bit", stateClass: "measurement"));
            }
            else if (sensor.SensorType == SensorType.Temperature &&
                     (sensor.Name.Contains("Core") || sensor.Name.Contains("Package")))
            {
                result.Add(new SensorData("cpu_temperature", "CPU Temperature",
                    Math.Round(sensor.Value.Value, 1), "\u00b0C",
                    icon: "mdi:thermometer", stateClass: "measurement"));
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

        foreach (var sensor in gpu.Sensors)
        {
            if (sensor.Value == null) continue;

            if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("Core"))
            {
                result.Add(new SensorData("gpu_load", "GPU Load",
                    Math.Round(sensor.Value.Value, 1), "%",
                    icon: "mdi:gpu", stateClass: "measurement"));
            }
            else if (sensor.SensorType == SensorType.Temperature)
            {
                result.Add(new SensorData("gpu_temperature", "GPU Temperature",
                    Math.Round(sensor.Value.Value, 1), "\u00b0C",
                    icon: "mdi:gpu", stateClass: "measurement"));
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
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    private static uint GetIdleTimeMs()
    {
        var lii = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        GetLastInputInfo(ref lii);
        return (uint)Environment.TickCount - lii.dwTime;
    }

    private static SensorData GetIpAddress()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT IPAddress FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = TRUE");
            foreach (var obj in searcher.Get())
            {
                var ips = obj["IPAddress"] as string[];
                if (ips != null)
                {
                    foreach (var ip in ips)
                    {
                        // Return first IPv4 address (skip IPv6)
                        if (ip.Contains("."))
                        {
                            return new SensorData("ip_address", "IP Address", ip,
                                icon: "mdi:ip-network");
                        }
                    }
                }
            }
        }
        catch { }
        return new SensorData("ip_address", "IP Address", "unavailable",
            icon: "mdi:ip-network-off");
    }

    private static SensorData GetConnectivity()
    {
        try
        {
            var ping = new System.Net.NetworkInformation.Ping();
            var reply = ping.Send("8.8.8.8", 2000);
            if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                return new SensorData("connectivity", "Connectivity", "on",
                    deviceClass: "connectivity", icon: "mdi:check-network");
        }
        catch { }
        return new SensorData("connectivity", "Connectivity", "off",
            deviceClass: "connectivity", icon: "mdi:close-network");
    }

    private static SensorData GetProcessCount()
    {
        try
        {
            var count = System.Diagnostics.Process.GetProcesses().Length;
            return new SensorData("process_count", "Running Processes", count, "",
                icon: "mdi:cog", stateClass: "measurement");
        }
        catch { return new SensorData("process_count", "Running Processes", 0, icon: "mdi:cog"); }
    }

    private static SensorData GetPageFile()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT CurrentUsage, AllocatedBaseSize FROM Win32_PageFileUsage");
            foreach (var obj in searcher.Get())
            {
                var usedMB = Convert.ToDouble(obj["CurrentUsage"]);
                var totalMB = Convert.ToDouble(obj["AllocatedBaseSize"]);
                var usedGB = Math.Round(usedMB / 1024.0, 2);
                var totalGB = Math.Round(totalMB / 1024.0, 2);
                var percent = Math.Round(usedMB / totalMB * 100, 1);
                // Return just the percent, we can't return multiple from here
                return new SensorData("page_file_percent", "Page File Usage", percent, "%",
                    icon: "mdi:harddisk", stateClass: "measurement");
            }
        }
        catch { }
        return new SensorData("page_file_percent", "Page File Usage", 0, "%",
            icon: "mdi:harddisk", stateClass: "measurement");
    }

    private static SensorData? GetWifiSsid()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT SSID FROM Win32_NetworkConnection WHERE ConnectionState = 'Connected'");
            foreach (var obj in searcher.Get())
            {
                var ssid = obj["SSID"]?.ToString();
                if (!string.IsNullOrEmpty(ssid))
                    return new SensorData("wifi_ssid", "WiFi Network", ssid,
                        icon: "mdi:wifi");
            }
        }
        catch { }
        return null;
    }

    private static SensorData? GetWifiSignal()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, Description FROM Win32_NetworkAdapter WHERE NetConnectionStatus = 2");
            // Signal strength requires netsh, WMI doesn't expose it directly
            // Use netsh as fallback
            var psi = new System.Diagnostics.ProcessStartInfo("netsh", "wlan show interfaces")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            var output = proc?.StandardOutput.ReadToEnd() ?? "";
            proc?.WaitForExit(3000);

            foreach (var line in output.Split('\n'))
            {
                if (line.Contains("Signal") && line.Contains("%"))
                {
                    var parts = line.Split(':');
                    if (parts.Length > 1)
                    {
                        var pctStr = parts[1].Trim().Replace("%", "").Trim();
                        if (int.TryParse(pctStr, out var pct))
                        {
                            return new SensorData("wifi_signal", "WiFi Signal", pct, "%",
                                icon: "mdi:wifi-strength-" + (pct > 75 ? "4" : pct > 50 ? "3" : pct > 25 ? "2" : "1"),
                                stateClass: "measurement");
                        }
                    }
                }
            }
        }
        catch { }
        return null;
    }

    private static SensorData GetActiveWindow()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            var title = new System.Text.StringBuilder(256);
            GetWindowText(hwnd, title, 256);
            var name = title.ToString();
            if (!string.IsNullOrEmpty(name))
                return new SensorData("active_window", "Active Window", name,
                    icon: "mdi:window-maximize");
        }
        catch { }
        return new SensorData("active_window", "Active Window", "unknown",
            icon: "mdi:window-maximize");
    }

    private List<SensorData> GetFanSensors()
    {
        var result = new List<SensorData>();

        // CPU fan
        var cpu = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
        if (cpu != null)
        {
            foreach (var sensor in cpu.Sensors)
            {
                if (sensor.Value == null) continue;
                if (sensor.SensorType == SensorType.Fan)
                {
                    var rpm = Math.Round(sensor.Value.Value, 0);
                    var name = sensor.Name.Contains("CPU") || sensor.Name.Contains("Fan")
                        ? "CPU Fan Speed"
                        : $"Fan ({sensor.Name})";
                    var uid = sensor.Name.ToLowerInvariant().Replace(" ", "_").Replace("#", "");
                    result.Add(new SensorData($"fan_{uid}", name, rpm, "RPM",
                        icon: "mdi:fan", stateClass: "measurement"));
                    break; // First fan only
                }
            }
        }

        // GPU fan
        var gpu = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.GpuNvidia || h.HardwareType == HardwareType.GpuAti || h.HardwareType == HardwareType.GpuIntel);
        if (gpu != null)
        {
            foreach (var sensor in gpu.Sensors)
            {
                if (sensor.Value == null) continue;
                if (sensor.SensorType == SensorType.Fan)
                {
                    var rpm = Math.Round(sensor.Value.Value, 0);
                    result.Add(new SensorData("gpu_fan_speed", "GPU Fan Speed", rpm, "RPM",
                        icon: "mdi:fan", stateClass: "measurement"));
                    break;
                }
            }
        }

        // SuperIO / Motherboard fans
        var superIO = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.SuperIO);
        if (superIO != null)
        {
            foreach (var sensor in superIO.Sensors)
            {
                if (sensor.Value == null) continue;
                if (sensor.SensorType == SensorType.Fan)
                {
                    var rpm = Math.Round(sensor.Value.Value, 0);
                    var label = sensor.Name.Trim();
                    var uid = label.ToLowerInvariant().Replace(" ", "_").Replace("#", "");
                    result.Add(new SensorData($"fan_{uid}", $"Fan: {label}", rpm, "RPM",
                        icon: "mdi:fan", stateClass: "measurement"));
                }
            }
        }

        return result;
    }

    private List<SensorData> GetCpuClockSensors()
    {
        var result = new List<SensorData>();
        var cpu = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
        if (cpu == null) return result;

        foreach (var sensor in cpu.Sensors)
        {
            if (sensor.Value == null) continue;
            if (sensor.SensorType == SensorType.Clock && sensor.Name.Contains("Core #1"))
            {
                var mhz = Math.Round(sensor.Value.Value, 0);
                result.Add(new SensorData("cpu_clock", "CPU Clock", mhz, "MHz",
                    icon: "mdi:speedometer", stateClass: "measurement"));
                break; // Only first core
            }
        }
        return result;
    }

    private List<SensorData> GetNetworkSensors()
    {
        var result = new List<SensorData>();
        try
        {
            var category = new System.Diagnostics.PerformanceCounterCategory("Network Interface");
            var instances = category.GetInstanceNames();
            // Find a real network adapter (skip loopback/ISATAP)
            foreach (var instance in instances)
            {
                if (instance.ToLowerInvariant().Contains("loopback") ||
                    instance.ToLowerInvariant().Contains("isatap") ||
                    instance.ToLowerInvariant().Contains("teredo") ||
                    instance.ToLowerInvariant().Contains("bluetooth"))
                    continue;

                try
                {
                    var sent = new System.Diagnostics.PerformanceCounter("Network Interface",
                        "Bytes Sent/sec", instance);
                    var recv = new System.Diagnostics.PerformanceCounter("Network Interface",
                        "Bytes Received/sec", instance);
                    // Need to read twice (first read = 0)
                    sent.NextValue(); recv.NextValue();
                    System.Threading.Thread.Sleep(100);
                    var uploadKbps = Math.Round(sent.NextValue() / 1024.0, 1);
                    var downloadKbps = Math.Round(recv.NextValue() / 1024.0, 1);

                    result.Add(new SensorData("network_upload", "Upload Speed", uploadKbps, "KB/s",
                        icon: "mdi:upload", stateClass: "measurement"));
                    result.Add(new SensorData("network_download", "Download Speed", downloadKbps, "KB/s",
                        icon: "mdi:download", stateClass: "measurement"));
                    break; // Only first real adapter
                }
                catch { }
            }
        }
        catch { }
        return result;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);

    public void Dispose()
    {
        if (!_disposed)
        {
            _computer.Close();
            _disposed = true;
        }
    }
}

public class UpdateVisitor : IVisitor
{
    public void VisitComputer(IComputer computer) { }
    public void VisitHardware(IHardware hardware) { hardware.Update(); }
    public void VisitSensor(ISensor sensor) { }
    public void VisitParameter(IParameter parameter) { }
}