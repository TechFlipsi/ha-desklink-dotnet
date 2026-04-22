# HA DeskLink v2.2

**Windows Companion App for Home Assistant** – native, fast, reliable.

Written in **C# / .NET 8** with LibreHardwareMonitorLib for real hardware sensors.


## Features
- 🌡️ **CPU & GPU Temperature** – real values thanks to LibreHardwareMonitorLib
- 📊 **All Sensors** – CPU, RAM, all drives (C:, D:, etc.), Battery, Uptime
- 🖥️ **Embedded Dashboard** – WebView2 (with auto-install if missing)
- ⚡ **PC Commands from HA** – Shutdown, Restart, Hibernate, Lock, and more via notifications
- 📬 **Notifications** – HA sends toast notifications to your PC
- 🔌 **mobile_app Protocol** – identical to the mobile app, no extra HA configuration needed
- 🔄 **Auto-Update** from GitHub Releases
- 📌 **System Tray** – runs minimized in the background
- 🛡️ **Admin Rights** – automatically requested for CPU/GPU temperature

## System Requirements
- Windows 10/11 (x64)
- No .NET Runtime required – everything included in the installer

## Installation
1. Download the latest `HA_DeskLink_Setup_x.x.x.exe` from [Releases](https://github.com/FKirchweger/ha-desklink-dotnet/releases/latest)
2. **Right-click → "Run as Administrator"** ⚠️ A normal double-click or waiting for UAC will cause an error – please start directly via right-click as administrator.
3. Enter HA URL + Long-Lived Token
4. Done! 🎉

## PC Commands from Home Assistant

HA DeskLink receives commands via **notifications** – just like the mobile app. No extra HA configuration needed!

### All Available Commands

| Command | Value | Effect |
|---|---|---|
| Shutdown | `shutdown` | Shuts down the PC in 30 seconds |
| Restart | `restart` | Restarts the PC in 30 seconds |
| Hibernate | `hibernate` | Puts the PC into hibernation |
| Lock PC | `lock` | Locks the Windows screen |
| Mute | `mute` | Mutes the audio |
| Volume Up | `volume_up` | Increases volume by 10% |
| Volume Down | `volume_down` | Decreases volume by 10% |
| Monitor On | `monitor_on` | Turns the monitor on |
| Monitor Off | `monitor_off` | Turns the monitor off |
| Screenshot | `screenshot` | Takes a screenshot |
| Message | *(no command)* | Shows a notification only |

> ⚠️ `mute`, `volume_up`, `volume_down`, `monitor_on`, `monitor_off`, and `screenshot` are available from v2.1.0!

### Examples

#### Shutdown
```yaml
service: notify.mobile_app_ha_desklink
data:
  title: "Shutdown PC"
  message: "PC will shut down in 30 seconds"
  data:
    command: "shutdown"
```

#### Restart
```yaml
service: notify.mobile_app_ha_desklink
data:
  title: "Restart PC"
  message: "PC will restart"
  data:
    command: "restart"
```

#### Hibernate
```yaml
service: notify.mobile_app_ha_desklink
data:
  title: "Hibernate"
  message: "PC going into hibernation"
  data:
    command: "hibernate"
```

#### Lock PC
```yaml
service: notify.mobile_app_ha_desklink
data:
  title: "Lock PC"
  message: "PC will be locked"
  data:
    command: "lock"
```

#### Simple Notification (no command)
```yaml
service: notify.mobile_app_ha_desklink
data:
  title: "Reminder"
  message: "Don't forget to take out the trash!"
```

### Automation in HA
```yaml
automation:
  - alias: "Shutdown PC at 10 PM"
    trigger:
      - platform: time
        at: "22:00:00"
    condition:
      - condition: state
        entity_id: binary_sensor.ha_desklink_connectivity
        state: "on"
    action:
      - service: notify.mobile_app_ha_desklink
        data:
          title: "Good night!"
          message: "PC is shutting down now."
          data:
            command: "shutdown"
```

### Dashboard Button in HA
```yaml
type: button
name: "Shutdown PC"
tap_action:
  action: call-service
  service: notify.mobile_app_ha_desklink
  service_data:
    title: "Shutdown PC"
    message: "Shutting down..."
    data:
      command: "shutdown"
```

## Sensors in Home Assistant

HA DeskLink automatically creates sensors in HA:

| Sensor | Description |
|---|---|
| `sensor.ha_desklink_cpu_usage` | CPU usage in % |
| `sensor.ha_desklink_cpu_temperature` | CPU temperature in °C (requires Admin) |
| `sensor.ha_desklink_cpu_clock` | CPU clock speed in MHz |
| `sensor.ha_desklink_gpu_load` | GPU usage in % |
| `sensor.ha_desklink_gpu_temperature` | GPU temperature in °C |
| `sensor.ha_desklink_gpu_fan_speed` | GPU fan in RPM |
| `sensor.ha_desklink_memory_usage` | RAM usage in % |
| `sensor.ha_desklink_memory_used` | RAM used in GB |
| `sensor.ha_desklink_memory_free` | RAM free in GB |
| `sensor.ha_desklink_memory_total` | RAM total in GB |
| `sensor.ha_desklink_disk_c_usage` | Drive C: usage in % |
| `sensor.ha_desklink_disk_c_free` | Drive C: free in GB |
| `sensor.ha_desklink_disk_c_used` | Drive C: used in GB |
| `sensor.ha_desklink_disk_c_total` | Drive C: total in GB |
| `sensor.ha_desklink_uptime` | PC uptime in hours |
| `sensor.ha_desklink_last_activity` | Last mouse/keyboard activity in minutes |
| `sensor.ha_desklink_battery` | Battery level in % (laptops only) |
| `sensor.ha_desklink_ip_address` | Current IPv4 address |
| `binary_sensor.ha_desklink_connectivity` | Online/Offline status (ping to 8.8.8.8) |
| `sensor.ha_desklink_process_count` | Number of running processes |
| `sensor.ha_desklink_page_file_percent` | Page file usage in % |
| `sensor.ha_desklink_wifi_ssid` | Connected WiFi network (name) |
| `sensor.ha_desklink_wifi_signal` | WiFi signal strength in % |
| `sensor.ha_desklink_active_window` | Active window/title |
| `sensor.ha_desklink_network_upload` | Upload speed in KB/s |
| `sensor.ha_desklink_network_download` | Download speed in KB/s |
| `sensor.ha_desklink_fan_*` | Fan speeds in RPM (CPU, GPU, Motherboard) |

> 💡 Additional drives (D:, E:, etc.) are detected automatically. GPU sensors only appear if a GPU is present.

## Dashboard

The integrated dashboard opens HA directly in the app (WebView2). If WebView2 is not installed, it automatically offers to download it. Alternatively, HA opens in the default browser.

## Build
```bash
dotnet publish src/HaDeskLink -c Release -r win-x64 --self-contained -o publish
iscc installer.iss
```

## Technology
| Component | Library |
|---|---|
| Hardware Sensors | LibreHardwareMonitorLib |
| Dashboard | Microsoft.Web.WebView2 |
| UI | Windows Forms |
| HTTP | System.Net.Http |
| Config | System.Text.Json |

## v1.x (Python)
The Python version is completed and archived: [ha-desklink](https://github.com/FKirchweger/ha-desklink)

## License
GPL v3 – Copyright © 2026 Fabian Kirchweger

This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License v3.

**Important:** If you modify or distribute this software, you MUST release your changes under the same GPL v3 license. Closed-source or proprietary use is NOT permitted.

## macOS Version
There is currently no macOS version of HA DeskLink. Unfortunately, I don't have Mac hardware for testing. If you have a Mac and would like to help, see [Issue #1 in the Linux repo](https://github.com/TechFlipsi/ha-desklink-linux/issues/1).

## Community
💬 [Discord](https://discord.gg/HnCZY54U7) – Questions, Feedback, Help

## Attribution
This project was created with AI assistance. All code was written and developed by **GLM-5.1** (via OpenClaw) – from architecture to implementation to debugging. This English documentation was also translated from German by AI. The German documentation is the original version.