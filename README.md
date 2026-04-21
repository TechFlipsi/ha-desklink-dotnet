# HA DeskLink v2.0

**Windows Companion App für Home Assistant** – nativ, schnell, zuverlässig.

Geschrieben in **C# / .NET 8** mit LibreHardwareMonitorLib für echte Hardware-Sensoren.

## Features
- 🌡️ **CPU & GPU Temperatur** – echte Werte dank LibreHardwareMonitorLib
- 📊 **Alle Sensoren** – CPU, RAM, alle Laufwerke (C:, D:, etc.), Battery, Uptime
- 🖥️ **Eingebettetes Dashboard** – WebView2 (mit Auto-Install falls fehlend)
- ⚡ **PC-Befehle aus HA** – Shutdown, Restart, Hibernate, Lock, und mehr per Benachrichtigung
- 📬 **Benachrichtigungen** – HA sendet Toast-Notifications an den PC
- 🔌 **mobile_app Protokoll** – identisch zur Handy-App, keine Extra-Konfiguration in HA nötig
- 🔄 **Auto-Update** von GitHub Releases
- 📌 **System Tray** – läuft minimiert im Hintergrund
- 🛡️ **Admin-Rechte** – automatisch angefordert für CPU/GPU-Temperatur

## Systemanforderungen
- Windows 10/11 (x64)
- Kein .NET Runtime nötig – alles im Installer enthalten

## Installation
1. Neueste `HA_DeskLink_Setup_x.x.x.exe` von [Releases](https://github.com/FKirchweger/ha-desklink-dotnet/releases/latest) herunterladen
2. Installer ausführen – ⚠️ **Admin-Rechte erforderlich!** Der Installer fragt automatisch nach erhöhten Rechten (UAC-Dialog). Ohne Admin-Rechte kommt es zu einer Fehlermeldung.
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
| Bildschirmfoto | `screenshot` | Macht einen Screenshot |
| Nachricht | *(kein command)* | Zeigt nur eine Benachrichtigung an |

> ⚠️ `mute`, `volume_up`, `volume_down`, `monitor_on`, `monitor_off` und `screenshot` kommen in künftigen Versionen!

### Beispiele

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
| `sensor.ha_desklink_cpu_clock` | CPU-Taktrate in MHz |
| `sensor.ha_desklink_network_upload` | Upload-Geschwindigkeit in KB/s |
| `sensor.ha_desklink_network_download` | Download-Geschwindigkeit in KB/s |
| `sensor.ha_desklink_fan_*` | Lüfter-Drehzahlen in RPM (CPU, GPU, Mainboard) |

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

## Lizenz
MIT