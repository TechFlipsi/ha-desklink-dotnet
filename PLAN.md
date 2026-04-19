# HA DeskLink v2.0 - C# / .NET 8

Windows Companion App für Home Assistant – nativ, schnell, ohne Python.

## Warum C# statt Python?
- **Nativ**: Keine Python-Runtime, keine PyInstaller-Probleme
- **WebView2**: Eingebettetes Dashboard funktioniert immer (EdgeChromium ist in Windows 10/11 integriert)
- **Performance**: Startup in <1s, <30MB RAM
- **Single-File EXE**: `dotnet publish` erstellt direkt eine .exe – kein Inno Setup nötig für basics
- **Inno Setup optional**: Für schönen Installer weiterhin möglich

## Architektur

```
ha-desklink-dotnet/
├── src/
│   ├── HaDeskLink/              # Hauptprojekt
│   │   ├── HaDeskLink.csproj
│   │   ├── Program.cs            # Entry point, Tray, Startup
│   │   ├── Config.cs             # Settings (JSON)
│   │   ├── HaApiClient.cs       # mobile_app API Client
│   │   ├── SensorManager.cs     # psutil-Äquivalent (WMI + Performance Counters)
│   │   ├── SensorDefinitions.cs # Sensor-Metadaten
│   │   ├── CommandHandler.cs    # Commands von HA empfangen
│   │   ├── WebhookServer.cs     # HTTP Listener für HA-Commands
│   │   ├── DashboardWindow.cs   # WebView2 Dashboard
│   │   ├── SettingsWindow.cs    # WPF/Eto Settings GUI
│   │   └── Assets/
│   │       └── icon.ico
│   └── HaDeskLink.Tests/        # Unit Tests
│       └── HaDeskLink.Tests.csproj
├── .github/
│   └── workflows/
│       └── build.yml            # GitHub Actions: dotnet publish + release
├── installer.iss                # Inno Setup (optional)
├── VERSION                      # Zentrale Versionsdatei
├── README.md
└── LICENSE
```

## Key Libraries
- **System.Drawing**: Tray Icon (built-in)
- **Microsoft.Web.WebView2**: Embedded Dashboard
- **System.Management (WMI)**: Hardware-Sensoren
- **System.Net.Http**: API Client
- **System.Text.Json**: Config/Protocol

## mobile_app Protokoll
Identisch zur Python-Version – selbes Webhook-Protokoll.

## Build
```bash
dotnet publish src/HaDeskLink -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

## Status
🚧 In Entwicklung – v2.0.0