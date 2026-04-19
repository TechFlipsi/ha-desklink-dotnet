# HA DeskLink

Windows Companion App für Home Assistant.

Sendet PC-Sensordaten an Home Assistant (CPU, RAM, Disk, GPU, Battery, Uptime) und empfängt Befehle (Shutdown, Restart, Hibernate, Lock, Screenshot).

**v2.0 – Geschrieben in C# / .NET 8 für native Windows-Integration.**

## Features
- System-Tray App (minimiert zum Tray)
- PC-Sensoren in Home Assistant (via mobile_app Protokoll)
- Befehle von HA empfangen (Shutdown, Restart, Lock, etc.)
- Eingebettetes HA Dashboard (WebView2)
- Setup-Wizard bei erster Verbindung
- Autostart-Option
- Automatische Updates von GitHub Releases

## Installation
1. `HA_DeskLink_Setup_x.x.x.exe` herunterladen
2. Installer ausführen
3. HA URL + Long-Lived Token eingeben
4. Fertig!

## Systemanforderungen
- Windows 10/11 (x64)
- WebView2 Runtime (in Windows 10/11 vorinstalliert)

## Build
```bash
dotnet publish src/HaDeskLink -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

## Lizenz
MIT