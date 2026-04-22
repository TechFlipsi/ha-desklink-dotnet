# HA DeskLink v3.0

[![Build](https://img.shields.io/github/actions/workflow/status/TechFlipsi/ha-desklink-dotnet/build.yml?branch=main&label=Build)](https://github.com/TechFlipsi/ha-desklink-dotnet/actions)
[![Version](https://img.shields.io/github/v/release/TechFlipsi/ha-desklink-dotnet?label=Version)](https://github.com/TechFlipsi/ha-desklink-dotnet/releases/latest)
[![License](https://img.shields.io/github/license/TechFlipsi/ha-desklink-dotnet?label=License)](https://github.com/TechFlipsi/ha-desklink-dotnet/LICENSE)
[![Downloads](https://img.shields.io/github/downloads/TechFlipsi/ha-desklink-dotnet/total?label=Downloads)](https://github.com/TechFlipsi/ha-desklink-dotnet/releases)
[![Discord](https://img.shields.io/discord/1496261911677894867?label=Discord)](https://discord.gg/HnCZY54U7)

**Windows Companion App für Home Assistant** – nativ, schnell, zuverlässig.

Geschrieben in **C# / .NET 8** mit LibreHardwareMonitorLib für echte Hardware-Sensoren.

## Features
- 🌡️ **CPU & GPU Temperatur** – echte Werte dank LibreHardwareMonitorLib
- 📊 **Alle Sensoren** – CPU, RAM, alle Laufwerke (C:, D:, etc.), Battery, Uptime
- 🖥️ **Eingebettetes Dashboard** – WebView2 (mit Auto-Install falls fehlend)
- ⚡ **PC-Befehle aus HA** – Shutdown, Restart, Hibernate, Lock, und mehr per Benachrichtigung
- 📬 **Benachrichtigungen** – HA sendet Toast-Notifications an den PC
- 🔔 **Actionable Notifications** – Benachrichtigungen mit Aktions-Buttons
- ⚡ **Quick Actions** – Globaler Hotkey (Ctrl+Shift+H) für HA-Entity-Toggles
- 📸 **Screenshot** – Echtes Bildschirmfoto + Upload als HA-Event
- 📷 **Webcam-Sensor** – Zeigt ob Webcam aktiv ist (on/off)
- 🔌 **mobile_app Protokoll** – identisch zur Handy-App, keine Extra-Konfiguration in HA nötig
- 🔄 **Auto-Update** von GitHub Releases
- 📌 **System Tray** – läuft minimiert im Hintergrund
- 🛡️ **Admin-Rechte** – automatisch angefordert für CPU/GPU-Temperatur

## Systemanforderungen
- Windows 10/11 (x64)
- Kein .NET Runtime nötig – alles im Installer enthalten

## Installation
1. Neueste `HA_DeskLink_Setup_x.x.x.exe` von [Releases](https://github.com/FKirchweger/ha-desklink-dotnet/releases/latest) herunterladen
2. Die `HA_DeskLink_Setup_x.x.x.exe` herunterladen, dann im Download-Ordner **Rechtsklick → „Als Administrator ausführen“** wählen. ⚠️ Ein normaler Doppelklick oder das Warten auf die UAC-Anfrage führt zu einer Fehlermeldung – bitte direkt per Rechtsklick als Administrator starten.
3. HA URL + Long-Lived Token eingeben
4. Fertig! 🎉

## PC-Befehle aus Home Assistant

HA DeskLink empfängt Befehle über **Benachrichtigungen** – genau wie die Handy-App. Keine Extra-Konfiguration in HA nötig!

### Alle verfügbaren Befehle

| Befehl | Schreibweise | Wirkung |
|---|---|---|
| Herunterfahren | `shutdown` | Fährt den PC in 30 Sekunden herunter |
| Neustarten | `restart` | Startet den PC in 30 Sekunden neu |
| Ruhezustand | `hibernate` | Versetzt den PC in den Ruhezustand |
| PC sperren | `lock` | Sperrt den Windows-Bildschirm |
| Lautstärke stumm | `mute` | Schaltet den Ton stumm |
| Lautstärke lauter | `volume_up` | Erhöht die Lautstärke um 10% |
| Lautstärke leiser | `volume_down` | Verringert die Lautstärke um 10% |
| Monitor an | `monitor_on` | Schaltet den Monitor an (wenn aus) |
| Monitor aus | `monitor_off` | Schaltet den Monitor aus |
| Bildschirmfoto | `screenshot` | Macht einen Screenshot und lädt ihn zu HA hoch |
| Bildschirmfoto speichern | `screenshot_save` | Macht einen Screenshot, speichert lokal und lädt zu HA hoch |
| Snipping Tool | `snipping_tool` | Öffnet das Windows Snipping Tool |
| Nachricht | *(kein command)* | Zeigt nur eine Benachrichtigung an |

> ⚠️ `mute`, `volume_up`, `volume_down`, `monitor_on`, `monitor_off` und `screenshot` sind ab v2.1.0 verfügbar!

### Beispiele

#### Actionable Notification (v3.0+)
```yaml
service: notify.mobile_app_ha_desklink
data:
  title: "PC herunterfahren?"
  message: "Der PC wird in 30 Sekunden heruntergefahren"
  data:
    actions:
      - action: SHUTDOWN
        title: "Ausschalten"
        command: shutdown
      - action: CANCEL
        title: "Abbrechen"
    command_on_action: shutdown
```

#### Herunterfahren
```yaml
service: notify.mobile_app_ha_desklink
data:
  title: "PC herunterfahren"
  message: "Der PC wird in 30 Sekunden heruntergefahren"
  data:
    command: "shutdown"
```

#### Neustarten
```yaml
service: notify.mobile_app_ha_desklink
data:
  title: "PC neustarten"
  message: "Der PC wird neu gestartet"
  data:
    command: "restart"
```

#### Ruhezustand
```yaml
service: notify.mobile_app_ha_desklink
data:
  title: "Ruhezustand"
  message: "PC geht in den Ruhezustand"
  data:
    command: "hibernate"
```

#### PC sperren
```yaml
service: notify.mobile_app_ha_desklink
data:
  title: "PC sperren"
  message: "Der PC wird gesperrt"
  data:
    command: "lock"
```

#### Einfache Benachrichtigung (ohne Befehl)
```yaml
service: notify.mobile_app_ha_desklink
data:
  title: "Erinnerung"
  message: "Müll rausbringen nicht vergessen!"
```

### Automatisierung in HA
```yaml
automation:
  - alias: "PC um 22 Uhr herunterfahren"
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
          title: "Gute Nacht!"
          message: "Der PC wird jetzt heruntergefahren."
          data:
            command: "shutdown"
```

### Dashboard-Button in HA
```yaml
type: button
name: "PC herunterfahren"
tap_action:
  action: call-service
  service: notify.mobile_app_ha_desklink
  service_data:
    title: "PC herunterfahren"
    message: "Wird heruntergefahren..."
    data:
      command: "shutdown"
```

## Sensoren in Home Assistant

HA DeskLink erstellt automatisch Sensoren in HA:

| Sensor | Beschreibung |
|---|---|
| `sensor.ha_desklink_cpu_usage` | CPU-Auslastung in % |
| `sensor.ha_desklink_cpu_temperature` | CPU-Temperatur in °C (braucht Admin) |
| `sensor.ha_desklink_cpu_clock` | CPU-Taktrate in MHz |
| `sensor.ha_desklink_gpu_load` | GPU-Auslastung in % |
| `sensor.ha_desklink_gpu_temperature` | GPU-Temperatur in °C |
| `sensor.ha_desklink_gpu_fan_speed` | GPU-Lüfter in RPM |
| `sensor.ha_desklink_memory_usage` | RAM-Auslastung in % |
| `sensor.ha_desklink_memory_used` | RAM verwendet in GB |
| `sensor.ha_desklink_memory_free` | RAM frei in GB |
| `sensor.ha_desklink_memory_total` | RAM gesamt in GB |
| `sensor.ha_desklink_disk_c_usage` | Laufwerk C: Auslastung in % |
| `sensor.ha_desklink_disk_c_free` | Laufwerk C: frei in GB |
| `sensor.ha_desklink_disk_c_used` | Laufwerk C: verwendet in GB |
| `sensor.ha_desklink_disk_c_total` | Laufwerk C: gesamt in GB |
| `sensor.ha_desklink_uptime` | PC-Laufzeit in Stunden |
| `sensor.ha_desklink_last_activity` | Letzte Maus/Tastatur-Aktivität in Minuten |
| `sensor.ha_desklink_battery` | Akkustand in % (nur Laptops) |
| `sensor.ha_desklink_ip_address` | Aktuelle IPv4-Adresse |
| `binary_sensor.ha_desklink_connectivity` | Online/Offline-Status (Ping zu 8.8.8.8) |
| `sensor.ha_desklink_process_count` | Anzahl laufende Prozesse |
| `sensor.ha_desklink_page_file_percent` | Auslagerungsdatei-Auslastung in % |
| `sensor.ha_desklink_wifi_ssid` | Verbundenes WiFi-Netzwerk (Name) |
| `sensor.ha_desklink_wifi_signal` | WiFi-Signalstärke in % |
| `sensor.ha_desklink_active_window` | Aktives Fenster/Titel |
| `sensor.ha_desklink_network_upload` | Upload-Geschwindigkeit in KB/s |
| `sensor.ha_desklink_network_download` | Download-Geschwindigkeit in KB/s |
| `sensor.ha_desklink_fan_*` | Lüfter-Drehzahlen in RPM (CPU, GPU, Mainboard) |
| `sensor.ha_desklink_webcam_active` | Webcam aktiv (on/off) |

> 💡 Weitere Laufwerke (D:, E: etc.) werden automatisch erkannt. GPU-Sensoren erscheinen nur wenn eine GPU vorhanden ist.

## Dashboard

Das integrierte Dashboard öffnet HA direkt in der App (WebView2). Falls WebView2 nicht installiert ist, wird automatisch angeboten es herunterzuladen. Alternativ öffnet sich HA im Standard-Browser.

## Build
```bash
dotnet publish src/HaDeskLink -c Release -r win-x64 --self-contained -o publish
iscc installer.iss
```

## Technologie
| Komponente | Library |
|---|---|
| Hardware-Sensoren | LibreHardwareMonitorLib |
| Dashboard | Microsoft.Web.WebView2 |
| UI | Windows Forms |
| HTTP | System.Net.Http |
| Config | System.Text.Json |

## v1.x (Python)
Die Python-Version ist abgeschlossen und archiviert: [ha-desklink](https://github.com/FKirchweger/ha-desklink)

## 📐 Versionierung
Ab v2.2.1 gelten **plattformunabhängige Versionsnummern**:

| Änderung | Beispiel | Erklärung |
|---|---|---|
| **Bug Fix** | 2.2.1 → 2.2.2 | Fehlerbehebung, nur betroffene Plattform |
| **Neue Funktionen** | 2.2.x → 3.0.0 | Neue Features, alle Plattformen gleichzeitig |

Jede Plattform (Windows, Linux, macOS) hat **eigene Versionsnummern**. Ein Bug-Fix unter Linux ändert nicht die Windows-Version – und umgekehrt. Große Funktionsupdates (Major) bekommen alle Plattformen gleichzeitig.

## Lizenz
GPL v3 – Copyright © 2026 Fabian Kirchweger

This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License v3.

**Important:** If you modify or distribute this software, you MUST release your changes under the same GPL v3 license. Closed-source or proprietary use is NOT permitted.

## macOS-Version
Es gibt jetzt eine macOS-Version von HA DeskLink! 🎉 Siehe [ha-desklink-mac](https://github.com/TechFlipsi/ha-desklink-mac) – ⚠️ Community Test Version, nicht vom Entwickler getestet.

## Community
💬 [Discord](https://discord.gg/HnCZY54U7) – Fragen, Feedback, Hilfe

## Erstellung
Dieses Projekt wurde unter Verwendung von KI-Unterstützung erstellt. Der gesamte Code wurde von **GLM-5.1** (via OpenClaw) geschrieben und entwickelt – von der Architektur über die Implementierung bis zum Debugging. Die englische Dokumentation wurde ebenfalls von der KI aus dem Deutschen ins Englische übersetzt. Die deutsche Dokumentation ist die Originalversion.