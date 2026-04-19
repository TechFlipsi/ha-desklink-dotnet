# HA DeskLink v2.0

**Windows Companion App für Home Assistant** – nativ, schnell, zuverlässig.

Geschrieben in **C# / .NET 8** mit LibreHardwareMonitorLib für echte Hardware-Sensoren.

## Features
- 🌡️ **CPU & GPU Temperatur** – echte Werte dank LibreHardwareMonitorLib
- 📊 **Alle Sensoren** – CPU, RAM, alle Laufwerke (C:, D:, etc.), Battery, Uptime
- 🖥️ **Eingebettetes Dashboard** – WebView2 (funktioniert ohne Edge-Installation!)
- ⚡ **HA Commands** – Shutdown, Restart, Hibernate, Lock direkt aus HA
- 🔌 **mobile_app Protokoll** – identisch zur Handy-App, keine Extra-Konfiguration in HA
- 🚀 **Nativ** – kein Python, ~5MB statt ~25MB, Startup <1s
- 🔄 **Auto-Update** von GitHub Releases
- 📌 **System Tray** – läuft minimiert im Hintergrund

## Systemanforderungen
- Windows 10/11 (x64)
- .NET 8 Runtime (wird automatisch mitgeliefert als Self-Contained)

## Installation
1. `HA_DeskLink_Setup_2.0.0.exe` herunterladen
2. Installer ausführen
3. HA URL + Long-Lived Token eingeben
4. Fertig!

## Build
```bash
dotnet publish src/HaDeskLink -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
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

## Lizenz
MIT