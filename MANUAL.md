# HA DeskLink – Betriebsanleitung / User Manual

📝 **Sprache / Language:** [Deutsch → Seite 1](#deutsch) | [English → Page 6](#english)

---

<a id="deutsch"></a>

# Deutsch

## Inhaltsverzeichnis

1. [Installation](#installation)
2. [Ersteinrichtung](#ersteinrichtung)
3. [Sensoren](#sensoren)
4. [Befehle aus Home Assistant](#befehle-aus-home-assistant)
5. [Actionable Notifications](#actionable-notifications)
6. [Quick Actions](#quick-actions)
7. [Screenshot-Funktion](#screenshot-funktion)
8. [Webcam-Sensor](#webcam-sensor)
9. [Einstellungen](#einstellungen)
10. [System Tray & Hintergrundbetrieb](#system-tray--hintergrundbetrieb)
11. [Auto-Update](#auto-update)
12. [Problembehebung](#problembehebung)
13. [Plattform-Vergleich](#plattform-vergleich)

---

### Installation

**Windows:**
1. `HA_DeskLink_Setup_x.x.x.exe` von [Releases](https://github.com/TechFlipsi/ha-desklink-dotnet/releases/latest) herunterladen
2. **Rechtsklick → „Als Administrator ausführen"** ⚠️ Normaler Doppelklick funktioniert nicht!
3. Einrichtung folgt automatisch

**Linux:**
1. `ha-desklink-linux-x64.tar.gz` von [Releases](https://github.com/TechFlipsi/ha-desklink-linux/releases/latest) herunterladen
2. `tar xzf ha-desklink-linux-x64.tar.gz`
3. `./ha-desklink --setup`
4. Als Service: `sudo cp ha-desklink.service /etc/systemd/system/ && sudo systemctl enable --now ha-desklink`

**macOS:**
1. `.dmg` von [Releases](https://github.com/TechFlipsi/ha-desklink-mac/releases/latest) herunterladen
2. App in Programme-Ordner ziehen
3. Beim ersten Start: HA URL + Token eingeben
> ⚠️ macOS = Community Test – nicht vom Entwickler getestet

---

### Ersteinrichtung

Du brauchst:
1. **HA URL** – z.B. `https://homeassistant.local:8123`
2. **Long-Lived Token** – HA → Profil → Sicherheit → Long-Lived Access Tokens → Token erstellen

Token wird verschlüsselt gespeichert (Windows: DPAPI, macOS: Keychain, Linux: config.json).

---

### Sensoren

Alle Sensoren erscheinen als `sensor.ha_desklink_*` in Home Assistant.

| Sensor | Win | Linux | Mac | Beschreibung |
|---|:---:|:---:|:---:|---|
| `cpu_usage` | ✅ | ✅ | ✅ | CPU-Auslastung % |
| `cpu_temp` | ✅ | ✅ | ✅* | CPU-Temperatur °C |
| `cpu_clock` | ✅ | ✅ | ❌ | CPU-Taktrate MHz |
| `memory` | ✅ | ✅ | ✅ | RAM-Auslastung % |
| `memory_available` | ✅ | ✅ | ✅ | RAM verfügbar GB |
| `battery` | ✅ | ✅ | ✅ | Akku % |
| `disk_usage` | ✅ | ✅ | ✅ | Festplatte % |
| `uptime` | ✅ | ✅ | ✅ | Laufzeit |
| `ip_address` | ✅ | ✅ | ✅ | IP-Adresse |
| `wifi_ssid` | ✅ | ✅ | ✅ | WiFi-Name |
| `process_count` | ✅ | ✅ | ✅ | Anzahl Prozesse |
| `gpu_temp` | ✅ | ✅ | ❌ | GPU-Temperatur |
| `gpu_load` | ✅ | ✅ | ❌ | GPU-Auslastung |
| `fan_speed` | ✅ | ✅ | ❌ | Lüfter RPM |
| `active_window` | ✅ | ❌ | ❌ | Aktives Fenster |
| `webcam_active` | ✅ | ✅ | ✅ | Webcam aktiv on/off |
| `brightness` | ❌ | ❌ | ✅ | Bildschirmhelligkeit % |
| `keyboard_backlight` | ❌ | ❌ | ✅ | Tastaturbeleuchtung % |
| `battery_cycle_count` | ❌ | ❌ | ✅ | Akku-Ladezyklen |
| `power_adapter` | ❌ | ❌ | ✅ | Netzteil verbunden |
| `network_upload/download` | ✅ | ✅ | ❌ | Netzwerkgeschwindigkeit |

> *macOS CPU-Temp: braucht `brew install osx-cpu-temp` oder sudo

---

### Befehle aus Home Assistant

Befehle werden über **Benachrichtigungen** gesendet – wie bei der Handy-App.

| Befehl | Win | Linux | Mac | Wirkung |
|---|:---:|:---:|:---:|---|
| `shutdown` | ✅ | ✅ | ✅ | Herunterfahren |
| `restart` | ✅ | ✅ | ✅ | Neustarten |
| `hibernate` | ✅ | ✅ | ✅ | Ruhezustand |
| `suspend` | ❌ | ✅ | ❌ | Bereitschaft (Linux) |
| `lock` | ✅ | ✅ | ✅ | Bildschirm sperren |
| `mute` | ✅ | ✅ | ✅ | Ton stumm |
| `volume_up` | ✅ | ✅ | ✅ | Lauter +10% |
| `volume_down` | ✅ | ✅ | ✅ | Leiser -10% |
| `monitor_on` | ✅ | ✅ | ✅ | Monitor an |
| `monitor_off` | ✅ | ✅ | ✅ | Monitor aus |
| `screenshot` | ✅ | ✅ | ✅ | Screenshot + Upload |
| `screenshot_save` | ✅ | ✅ | ✅ | Screenshot lokal + Upload |
| `snipping_tool` | ✅ | ❌ | ❌ | Windows Snipping Tool |
| `brightness_up` | ❌ | ❌ | ✅ | Helligkeit +10% |
| `brightness_down` | ❌ | ❌ | ✅ | Helligkeit -10% |
| `brightness:N` | ❌ | ❌ | ✅ | Helligkeit auf N% |

**Beispiel:**
```yaml
service: notify.mobile_app_ha_desklink
data:
  title: "Gute Nacht!"
  message: "PC wird heruntergefahren."
  data:
    command: "shutdown"
```

---

### Actionable Notifications

Ab v3.0: Benachrichtigungen mit **Aktions-Buttons**.

| Plattform | Darstellung |
|---|---|
| Windows | WinForms-Dialog mit Buttons |
| Linux | notify-send + automatische `command_on_action` |
| macOS | osascript + automatische `command_on_action` |

**Beispiel:**
```yaml
service: notify.mobile_app_ha_desklink
data:
  title: "PC herunterfahren?"
  message: "Soll der PC heruntergefahren werden?"
  data:
    actions:
      - action: SHUTDOWN
        title: "Ausschalten"
        command: shutdown
      - action: CANCEL
        title: "Abbrechen"
    command_on_action: shutdown
```

- `actions`: Liste der Buttons
- `command`: Befehl bei Button-Klick
- `command_on_action`: Fallback-Befehl (automatisch auf Linux/macOS)

---

### Quick Actions

Ab v3.0: **HA-Entities per Hotkey/Button umschalten**.

**Konfiguration:**
- **Windows:** Einstellungen → Quick Actions → Entity-IDs hinzufügen
- **Linux/macOS:** `config.json` → `QuickActions`-Feld:
```json
{"QuickActions": "[{\"entityId\":\"light.wohnzimmer\",\"name\":\"Wohnzimmer\"}]"}
```

| Plattform | Aufruf |
|---|---|
| Windows | `Ctrl+Shift+H` oder Tray-Icon |
| Linux | Dashboard-Button ⚡ |
| macOS | Dashboard-Button ⚡ |

Beim Klick wird `homeassistant.toggle` an HA gesendet.

---

### Screenshot-Funktion

| Befehl | Wirkung |
|---|---|
| `screenshot` | Screenshot + HA-Event Upload |
| `screenshot_save` | Screenshot lokal speichern + HA-Event Upload |

| Plattform | Methode |
|---|---|
| Windows | Graphics.CopyFromScreen → PNG → Base64 |
| Linux | gnome-screenshot → scrot → grim |
| macOS | screencapture -x |

---

### Webcam-Sensor

Sensor `sensor.ha_desklink_webcam_active` – `on` wenn Kamera aktiv, `off` wenn nicht.

| Plattform | Erkennung |
|---|---|
| Windows | WMI Win32_PnPEntity Camera |
| Linux | /dev/video* + /proc/*/fd/* |
| macOS | ioreg + lsof |

---

### Einstellungen

| Plattform | Methode |
|---|---|
| Windows | Tray-Icon → Rechtsklick → Einstellungen |
| Linux | Dashboard → ⚙️ Einrichtung oder config.json |
| macOS | Dashboard → ⚙️ Einrichtung |

---

### System Tray & Hintergrundbetrieb

| Plattform | Verhalten |
|---|---|
| Windows | Minimiert zum System Tray. Hotkey Ctrl+Shift+H. |
| Linux | systemd-Daemon. Dashboard optional. |
| macOS | Reguläre App. Dashboard im Browser. |

---

### Auto-Update

| Plattform | Wann | Methode |
|---|---|---|
| Windows | Beim Start | Download + Installer |
| Linux | Alle 2h | Download + tar.gz |
| macOS | Beim Start | Download + DMG-Link |

---

### Problembehebung

| Problem | Lösung |
|---|---|
| Verbindung klappt nicht | HA URL prüfen, Token prüfen, Firewall Port 8123 |
| Sensoren fehlen in HA | 30-60s warten, Gerät in HA öffnen, Neustart |
| CPU-Temperatur leer (Win) | Als Administrator starten |
| Webcam immer "off" | Kamera vorhanden? Linux: `ls /dev/video*` |
| SSL-Fehler | SSL-Prüfung in Einstellungen deaktivieren |

---

### Plattform-Vergleich

| Feature | Windows | Linux | macOS | Erklärung |
|---|:---:|:---:|:---:|---|
| GUI | WinForms | Avalonia | Avalonia | |
| Embedded Dashboard | ✅ WebView2 | ❌ Browser | ❌ Browser | WebView2 nicht stabil |
| System Tray | ✅ | ❌ Daemon | ❌ Dock | |
| Quick Actions Hotkey | ✅ Ctrl+Shift+H | ❌ Button | ❌ Button | Globale Hotkeys nur Win |
| Screenshot-Methode | CopyFromScreen | gnome-screenshot | screencapture | |
| Webcam-Erkennung | WMI | /dev/video* | ioreg/lsof | |
| Token-Speicher | DPAPI | config.json | Keychain | |
| Admin nötig | Ja (HW-Sensoren) | Nein | Nein | |
| Daemon-Modus | ❌ | ✅ systemd | ❌ | |
| Installer | ✅ InnoSetup | tar.gz | DMG | |

---

<a id="english"></a>

# English

## Table of Contents

1. [Installation](#installation-en)
2. [Initial Setup](#initial-setup-en)
3. [Sensors](#sensors-en)
4. [Commands from Home Assistant](#commands-en)
5. [Actionable Notifications](#actionable-notifications-en)
6. [Quick Actions](#quick-actions-en)
7. [Screenshot Function](#screenshot-en)
8. [Webcam Sensor](#webcam-sensor-en)
9. [Settings](#settings-en)
10. [System Tray & Background](#system-tray-en)
11. [Auto-Update](#auto-update-en)
12. [Troubleshooting](#troubleshooting-en)
13. [Platform Comparison](#platform-comparison-en)

---

<a id="installation-en"></a>

### Installation

**Windows:**
1. Download `HA_DeskLink_Setup_x.x.x.exe` from [Releases](https://github.com/TechFlipsi/ha-desklink-dotnet/releases/latest)
2. **Right-click → "Run as Administrator"** ⚠️ Normal double-click won't work!
3. Setup follows automatically

**Linux:**
1. Download `ha-desklink-linux-x64.tar.gz` from [Releases](https://github.com/TechFlipsi/ha-desklink-linux/releases/latest)
2. `tar xzf ha-desklink-linux-x64.tar.gz`
3. `./ha-desklink --setup`
4. As service: `sudo cp ha-desklink.service /etc/systemd/system/ && sudo systemctl enable --now ha-desklink`

**macOS:**
1. Download `.dmg` from [Releases](https://github.com/TechFlipsi/ha-desklink-mac/releases/latest)
2. Drag app to Applications folder
3. On first launch: enter HA URL + Token
> ⚠️ macOS = Community Test – not tested by the developer

---

<a id="initial-setup-en"></a>

### Initial Setup

You need:
1. **HA URL** – e.g. `https://homeassistant.local:8123`
2. **Long-Lived Token** – HA → Profile → Security → Long-Lived Access Tokens → Create Token

Token is stored encrypted (Windows: DPAPI, macOS: Keychain, Linux: config.json).

---

<a id="sensors-en"></a>

### Sensors

All sensors appear as `sensor.ha_desklink_*` in Home Assistant.

| Sensor | Win | Linux | Mac | Description |
|---|:---:|:---:|:---:|---|
| `cpu_usage` | ✅ | ✅ | ✅ | CPU usage % |
| `cpu_temp` | ✅ | ✅ | ✅* | CPU temperature °C |
| `cpu_clock` | ✅ | ✅ | ❌ | CPU clock MHz |
| `memory` | ✅ | ✅ | ✅ | RAM usage % |
| `memory_available` | ✅ | ✅ | ✅ | RAM available GB |
| `battery` | ✅ | ✅ | ✅ | Battery % |
| `disk_usage` | ✅ | ✅ | ✅ | Disk usage % |
| `uptime` | ✅ | ✅ | ✅ | Uptime |
| `ip_address` | ✅ | ✅ | ✅ | IP address |
| `wifi_ssid` | ✅ | ✅ | ✅ | WiFi name |
| `process_count` | ✅ | ✅ | ✅ | Process count |
| `gpu_temp` | ✅ | ✅ | ❌ | GPU temperature |
| `gpu_load` | ✅ | ✅ | ❌ | GPU usage |
| `fan_speed` | ✅ | ✅ | ❌ | Fan RPM |
| `active_window` | ✅ | ❌ | ❌ | Active window title |
| `webcam_active` | ✅ | ✅ | ✅ | Webcam active on/off |
| `brightness` | ❌ | ❌ | ✅ | Display brightness % |
| `keyboard_backlight` | ❌ | ❌ | ✅ | Keyboard backlight % |
| `battery_cycle_count` | ❌ | ❌ | ✅ | Battery cycle count |
| `power_adapter` | ❌ | ❌ | ✅ | Power adapter connected |
| `network_upload/download` | ✅ | ✅ | ❌ | Network speed |

> *macOS CPU temp: needs `brew install osx-cpu-temp` or sudo

---

<a id="commands-en"></a>

### Commands from Home Assistant

Commands are sent via **notifications** – same as the mobile app.

| Command | Win | Linux | Mac | Effect |
|---|:---:|:---:|:---:|---|
| `shutdown` | ✅ | ✅ | ✅ | Shut down |
| `restart` | ✅ | ✅ | ✅ | Restart |
| `hibernate` | ✅ | ✅ | ✅ | Hibernate |
| `suspend` | ❌ | ✅ | ❌ | Suspend (Linux) |
| `lock` | ✅ | ✅ | ✅ | Lock screen |
| `mute` | ✅ | ✅ | ✅ | Mute volume |
| `volume_up` | ✅ | ✅ | ✅ | Volume +10% |
| `volume_down` | ✅ | ✅ | ✅ | Volume -10% |
| `monitor_on` | ✅ | ✅ | ✅ | Monitor on |
| `monitor_off` | ✅ | ✅ | ✅ | Monitor off |
| `screenshot` | ✅ | ✅ | ✅ | Screenshot + upload |
| `screenshot_save` | ✅ | ✅ | ✅ | Save locally + upload |
| `snipping_tool` | ✅ | ❌ | ❌ | Windows Snipping Tool |
| `brightness_up` | ❌ | ❌ | ✅ | Brightness +10% |
| `brightness_down` | ❌ | ❌ | ✅ | Brightness -10% |
| `brightness:N` | ❌ | ❌ | ✅ | Set brightness to N% |

**Example:**
```yaml
service: notify.mobile_app_ha_desklink
data:
  title: "Good night!"
  message: "PC will shut down."
  data:
    command: "shutdown"
```

---

<a id="actionable-notifications-en"></a>

### Actionable Notifications

Since v3.0: Notifications with **action buttons**.

| Platform | Presentation |
|---|---|
| Windows | WinForms dialog with buttons |
| Linux | notify-send + auto-execute `command_on_action` |
| macOS | osascript + auto-execute `command_on_action` |

**Example:**
```yaml
service: notify.mobile_app_ha_desklink
data:
  title: "Shut down PC?"
  message: "Should the PC be shut down?"
  data:
    actions:
      - action: SHUTDOWN
        title: "Shut down"
        command: shutdown
      - action: CANCEL
        title: "Cancel"
    command_on_action: shutdown
```

- `actions`: list of buttons to display
- `command`: command executed on button click
- `command_on_action`: fallback command (auto-executed on Linux/macOS)

---

<a id="quick-actions-en"></a>

### Quick Actions

Since v3.0: **Toggle HA entities via hotkey/button**.

**Configuration:**
- **Windows:** Settings → Quick Actions → Add entity IDs
- **Linux/macOS:** `config.json` → `QuickActions` field:
```json
{"QuickActions": "[{\"entityId\":\"light.living_room\",\"name\":\"Living Room\"}]"}
```

| Platform | Trigger |
|---|---|
| Windows | `Ctrl+Shift+H` or Tray icon |
| Linux | Dashboard button ⚡ |
| macOS | Dashboard button ⚡ |

Clicking sends `homeassistant.toggle` to HA.

---

<a id="screenshot-en"></a>

### Screenshot Function

| Command | Effect |
|---|---|
| `screenshot` | Screenshot + HA event upload |
| `screenshot_save` | Save locally + HA event upload |

| Platform | Method |
|---|---|
| Windows | Graphics.CopyFromScreen → PNG → Base64 |
| Linux | gnome-screenshot → scrot → grim |
| macOS | screencapture -x |

---

<a id="webcam-sensor-en"></a>

### Webcam Sensor

Sensor `sensor.ha_desklink_webcam_active` – `on` when camera is active, `off` when not.

| Platform | Detection |
|---|---|
| Windows | WMI Win32_PnPEntity Camera |
| Linux | /dev/video* + /proc/*/fd/* |
| macOS | ioreg + lsof |

---

<a id="settings-en"></a>

### Settings

| Platform | Method |
|---|---|
| Windows | Tray icon → Right-click → Settings |
| Linux | Dashboard → ⚙️ Setup or config.json |
| macOS | Dashboard → ⚙️ Setup |

---

<a id="system-tray-en"></a>

### System Tray & Background

| Platform | Behavior |
|---|---|
| Windows | Minimized to system tray. Hotkey Ctrl+Shift+H. |
| Linux | systemd daemon. Dashboard optional. |
| macOS | Regular app. Dashboard in browser. |

---

<a id="auto-update-en"></a>

### Auto-Update

| Platform | When | Method |
|---|---|---|
| Windows | On start | Download + installer |
| Linux | Every 2h | Download + tar.gz |
| macOS | On start | Download + DMG link |

---

<a id="troubleshooting-en"></a>

### Troubleshooting

| Problem | Solution |
|---|---|
| Can't connect | Check HA URL, token, firewall port 8123 |
| Sensors missing in HA | Wait 30-60s, open device in HA, restart |
| CPU temp empty (Win) | Run as Administrator |
| Webcam always "off" | Camera present? Linux: `ls /dev/video*` |
| SSL error | Disable SSL verification in settings |

---

<a id="platform-comparison-en"></a>

### Platform Comparison

| Feature | Windows | Linux | macOS | Explanation |
|---|:---:|:---:|:---:|---|
| GUI | WinForms | Avalonia | Avalonia | |
| Embedded Dashboard | ✅ WebView2 | ❌ Browser | ❌ Browser | WebView2 not stable |
| System Tray | ✅ | ❌ Daemon | ❌ Dock | |
| Quick Actions Hotkey | ✅ Ctrl+Shift+H | ❌ Button | ❌ Button | Global hotkeys Win only |
| Screenshot Method | CopyFromScreen | gnome-screenshot | screencapture | |
| Webcam Detection | WMI | /dev/video* | ioreg/lsof | |
| Token Storage | DPAPI | config.json | Keychain | |
| Admin Required | Yes (HW sensors) | No | No | |
| Daemon Mode | ❌ | ✅ systemd | ❌ | |
| Installer | ✅ InnoSetup | tar.gz | DMG | |

---

**Idee / Idea:** Fabian Kirchweger | **Code:** GLM-5.1 (via OpenClaw) | **Lizenz / License:** GPL v3