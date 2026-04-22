# HA DeskLink – Betriebsanleitung / User Manual

📝 **Sprache / Language:** [Deutsch → unten](#deutsch) | [English → below](#english)

---

<a id="deutsch"></a>

# 🇩🇪 Deutsch

## Inhaltsverzeichnis

1. [HASS.Agent vs. HA DeskLink – Vergleich](#hassagent-vs-ha-desklink--vergleich)
   - [Warum HA DeskLink?](#warum-ha-desklink)
   - [Funktionen im Vergleich](#funktionen-im-vergleich)
   - [Architektur](#architektur)
   - [Was HA DeskLink NICHT kann](#was-ha-desklink-nicht-kann)
   - [Migration von HASS.Agent](#migration-von-hassagent)
2. [Installation](#installation)
3. [Ersteinrichtung](#ersteinrichtung)
4. [Sensoren](#sensoren)
5. [Befehle aus Home Assistant](#befehle-aus-home-assistant)
6. [Actionable Notifications](#actionable-notifications)
7. [Quick Actions](#quick-actions)
8. [Screenshot-Funktion](#screenshot-funktion)
9. [Webcam-Sensor](#webcam-sensor)
10. [Einstellungen](#einstellungen)
11. [System Tray & Hintergrundbetrieb](#system-tray--hintergrundbetrieb)
12. [Auto-Update](#auto-update)
13. [Problembehebung](#problembehebung)
14. [Plattform-Vergleich (Windows / Linux / macOS)](#plattform-vergleich-windows--linux--macos)

---

## HASS.Agent vs. HA DeskLink – Vergleich

### Warum HA DeskLink?

HASS.Agent ist ein großartiges Projekt – aber es erfordert **MQTT** und eine **separate Integration** in Home Assistant. HA DeskLink geht einen anderen Weg: Es nutzt das **mobile_app-Protokoll**, das auch die offizielle Handy-App verwendet. Das bedeutet: **Keine Extra-Integration, kein MQTT-Broker nötig.** Einfach installieren, Token eingeben, fertig.

### Funktionen im Vergleich

| Funktion | HASS.Agent | HA DeskLink | Hinweis |
|---|:---:|:---:|---|
| **Verbindung** | MQTT | WebSocket (mobile_app) | Kein MQTT-Broker nötig |
| **Integration in HA** | Eigene HACS-Integration nötig | Automatisch (mobile_app) | Erscheint wie ein Handy in HA |
| **CPU-Temperatur** | ✅ | ✅ | |
| **CPU-Auslastung** | ✅ | ✅ | |
| **RAM** | ✅ | ✅ | |
| **Festplatte** | ✅ | ✅ | Alle Laufwerke |
| **Akku** | ✅ | ✅ | |
| **GPU-Temperatur** | ✅ | ✅ (Win/Linux) | macOS: keine öffentliche API |
| **GPU-Auslastung** | ✅ | ✅ (Win/Linux) | macOS: nur mit sudo |
| **Lüfter-Drehzahl** | ✅ | ✅ (Win/Linux) | |
| **WiFi-SSID** | ✅ | ✅ | |
| **Uptime** | ✅ | ✅ | |
| **Aktives Fenster** | ✅ | ✅ (Win) | Linux/macOS: begrenzt |
| **Webcam-Status** | ❌ | ✅ | v3.0+: Sensor ob Kamera aktiv |
| **Befehle von HA** | ✅ | ✅ | Shutdown, Restart, Lock, etc. |
| **Screenshot** | ✅ (Snipping Tool) | ✅ (Echt + Upload) | Direkt als HA-Event |
| **Benachrichtigungen** | ✅ | ✅ | |
| **Actionable Notifications** | ✅ | ✅ | v3.0+: Buttons in Notifications |
| **Quick Actions** | ✅ | ✅ | v3.0+: Hotkey + Popup |
| **Media Player** | ✅ | ❌ | Nicht geplant – WebSocket-only |
| **Dashboard eingebettet** | ✅ (WebView) | ✅ (Win: WebView2) | Linux/Mac: Browser |
| **Auto-Update** | ✅ | ✅ | |
| **System Tray** | ✅ | ✅ (Win) | Linux: Daemon, Mac: Dock |
| **Einstellungen** | GUI | GUI (Win) / Config (Linux/Mac) | |
| **Lokalisierung** | Englisch | 6 Sprachen (de, en, es, fr, zh, ja) | |
| **macOS** | ❌ | ✅ (Community Test) | |
| **Linux** | ❌ | ✅ | Headless möglich |
| **Lizenz** | MIT | GPL v3 | HA DeskLink ist Copyleft |

### Architektur

**HASS.Agent:** `App ←→ MQTT-Broker ←→ HA (+ HACS-Integration)`

**HA DeskLink:** `App ←→ Home Assistant (WebSocket + Webhook)` – keine Extra-Software nötig.

### Was HA DeskLink NICHT kann

| Feature | Warum nicht |
|---|---|
| **Media Player** | Würde eigene HA-Integration erfordern. mobile_app bietet keine Media-Player-Entity. |
| **MQTT** | Bewusst weggelassen. mobile_app ist einfacher. |
| **WebView auf Linux/macOS** | WebView2 nicht stabil verfügbar. Dashboard öffnet im Browser. |

### Migration von HASS.Agent

1. HA DeskLink installieren
2. HA-URL + Long-Lived Token eingeben
3. Gerät registriert sich automatisch in HA
4. HASS.Agent deinstallieren – alte Entities in HA bleiben bis man sie löscht
5. Automatisierungen auf `sensor.ha_desklink_*` anpassen

---

## Installation

### Windows
1. `HA_DeskLink_Setup_x.x.x.exe` von [Releases](https://github.com/TechFlipsi/ha-desklink-dotnet/releases/latest) herunterladen
2. **Rechtsklick → „Als Administrator ausführen"** ⚠️ Normaler Doppelklick funktioniert nicht!
3. Einrichtung folgt automatisch

### Linux
1. `ha-desklink-linux-x64.tar.gz` von [Releases](https://github.com/TechFlipsi/ha-desklink-linux/releases/latest) herunterladen
2. `tar xzf ha-desklink-linux-x64.tar.gz`
3. `./ha-desklink --setup`
4. Als Service: `sudo cp ha-desklink.service /etc/systemd/system/ && sudo systemctl enable --now ha-desklink`

### macOS
1. `.dmg` von [Releases](https://github.com/TechFlipsi/ha-desklink-mac/releases/latest) herunterladen
2. App in Programme-Ordner ziehen
3. Beim ersten Start: HA URL + Token eingeben

> ⚠️ macOS = Community Test – nicht vom Entwickler getestet

---

## Ersteinrichtung

Du brauchst:
1. **HA URL** – z.B. `https://homeassistant.local:8123`
2. **Long-Lived Token** – HA → Profil → Sicherheit → Long-Lived Access Tokens → Token erstellen

Token wird verschlüsselt gespeichert (Windows: DPAPI, macOS: Keychain, Linux: config.json).

---

## Sensoren

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

## Befehle aus Home Assistant

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

## Actionable Notifications

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

## Quick Actions

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

## Screenshot-Funktion

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

## Webcam-Sensor

Sensor `sensor.ha_desklink_webcam_active` – `on` wenn Kamera aktiv, `off` wenn nicht.

| Plattform | Erkennung |
|---|---|
| Windows | WMI Win32_PnPEntity Camera |
| Linux | /dev/video* + /proc/*/fd/* |
| macOS | ioreg + lsof |

---

## Einstellungen

| Plattform | Methode |
|---|---|
| Windows | Tray-Icon → Rechtsklick → Einstellungen |
| Linux | Dashboard → ⚙️ Einrichtung oder config.json |
| macOS | Dashboard → ⚙️ Einrichtung |

---

## System Tray & Hintergrundbetrieb

| Plattform | Verhalten |
|---|---|
| Windows | Minimiert zum System Tray. Hotkey Ctrl+Shift+H. |
| Linux | systemd-Daemon. Dashboard optional. |
| macOS | Reguläre App. Dashboard im Browser. |

---

## Auto-Update

| Plattform | Wann | Methode |
|---|---|---|
| Windows | Beim Start | Download + Installer |
| Linux | Alle 2h | Download + tar.gz |
| macOS | Beim Start | Download + DMG-Link |

---

## Problembehebung

| Problem | Lösung |
|---|---|
| Verbindung klappt nicht | HA URL prüfen, Token prüfen, Firewall Port 8123 |
| Sensoren fehlen in HA | 30-60s warten, Gerät in HA öffnen, Neustart |
| CPU-Temperatur leer (Win) | Als Administrator starten |
| Webcam immer "off" | Kamera vorhanden? Linux: `ls /dev/video*` |
| SSL-Fehler | SSL-Prüfung in Einstellungen deaktivieren |
| **Windows Defender: VulnerableDriver** | Siehe unten ⚠️ |

### ⚠️ Windows Defender – „Vulnerable Driver: WinNT/Winring0"

Windows Defender meldet möglicherweise **VulnerableDriver:WinNT/Winring0** für die Datei `HA_DeskLink.sys`.

**Das ist ein Fehlalarm!** Die Erklärung:

- HA DeskLink nutzt **LibreHardwareMonitorLib** für Hardware-Sensoren (CPU-Temperatur, GPU-Temperatur, Lüfter-Drehzahl)
- Diese Bibliothek verwendet den **WinRing0-Treiber**, um Hardware-Sensoren auf Kernel-Ebene auszulesen
- WinRing0 ist ein **legitimer, Open-Source-Treiber** – er braucht Kernel-Zugriff, um Temperatursensoren zu lesen
- Microsoft Defender flagt alle Kernel-Treiber als „potenziell gefährlich", auch wenn sie harmlos sind

**So lässt du den Treiber zu:**

1. Windows Defender öffnen → **Schutzverlauf**
2. Den Eintrag „VulnerableDriver:WinNT/Winring0" suchen
3. Auf **Aktionen** → **Zulassen** klicken

Oder alternativ:

1. Windows-Einstellungen → **Datenschutz & Sicherheit** → **Windows-Sicherheit**
2. **Viren- & Bedrohungsschutz** → **Schutzverlauf**
3. Den Treiber-Eintrag → **Zulassen**

**Ohne Admin-Rechte** funktioniert HA DeskLink auch – dann fehlen nur CPU/GPU-Temperatur und Lüfter-Drehzahl. Alle anderen Sensoren und Befehle funktionieren normal.

---

## Plattform-Vergleich (Windows / Linux / macOS)

| Feature | Windows | Linux | macOS | Erklärung |
|---|:---:|:---:|:---:|---|
| **GUI** | WinForms | Avalonia | Avalonia | |
| **Embedded Dashboard** | ✅ WebView2 | ❌ Browser | ❌ Browser | WebView2 nicht stabil auf Linux/Mac |
| **System Tray** | ✅ | ❌ Daemon | ❌ Dock | |
| **Quick Actions Hotkey** | ✅ Ctrl+Shift+H | ❌ Button | ❌ Button | Globale Hotkeys nur auf Windows |
| **Screenshot-Methode** | CopyFromScreen | gnome-screenshot | screencapture | |
| **Webcam-Erkennung** | WMI | /dev/video* | ioreg/lsof | |
| **Token-Speicher** | DPAPI | config.json | Keychain | |
| **Admin nötig** | Ja (HW-Sensoren) | Nein | Nein | |
| **Daemon-Modus** | ❌ | ✅ systemd | ❌ | |
| **Installer** | ✅ InnoSetup | tar.gz | DMG | |
| **Sensoren** | Alle + GPU/Fan | Alle + GPU/Fan | Alle - GPU/Fan | macOS hat keine öffentliche GPU-API |
| **Befehle** | Alle | Alle + suspend | Alle - suspend + brightness | |
| **Actionable Notifications** | Dialog mit Buttons | Auto-Execute | Auto-Execute | Linux/Mac: keine interaktiven Buttons |
| **Lokalisierung** | 6 Sprachen | 6 Sprachen | 6 Sprachen | de, en, es, fr, zh, ja |

---

<a id="english"></a>

# 🇬🇧 English

## Table of Contents

1. [HASS.Agent vs. HA DeskLink – Comparison](#hassagent-vs-ha-desklink--comparison)
   - [Why HA DeskLink?](#why-ha-desklink)
   - [Feature Comparison](#feature-comparison)
   - [Architecture](#architecture)
   - [What HA DeskLink Does NOT Support](#what-ha-desklink-does-not-support)
   - [Migrating from HASS.Agent](#migrating-from-hassagent)
2. [Installation](#installation-en)
3. [Initial Setup](#initial-setup-en)
4. [Sensors](#sensors-en)
5. [Commands from Home Assistant](#commands-en)
6. [Actionable Notifications](#actionable-notifications-en)
7. [Quick Actions](#quick-actions-en)
8. [Screenshot Function](#screenshot-en)
9. [Webcam Sensor](#webcam-sensor-en)
10. [Settings](#settings-en)
11. [System Tray & Background](#system-tray-en)
12. [Auto-Update](#auto-update-en)
13. [Troubleshooting](#troubleshooting-en)
14. [Platform Comparison (Windows / Linux / macOS)](#platform-comparison-windows--linux--macos)

---

## HASS.Agent vs. HA DeskLink – Comparison

### Why HA DeskLink?

HASS.Agent is a great project – but it requires **MQTT** and a **separate integration** in Home Assistant. HA DeskLink takes a different approach: it uses the **mobile_app protocol**, the same one the official mobile app uses. This means: **No extra integration, no MQTT broker needed.** Just install, enter your token, and you're done.

### Feature Comparison

| Feature | HASS.Agent | HA DeskLink | Notes |
|---|:---:|:---:|---|
| **Connection** | MQTT | WebSocket (mobile_app) | No MQTT broker needed |
| **HA Integration** | Custom HACS integration required | Automatic (mobile_app) | Appears like a phone in HA |
| **CPU Temperature** | ✅ | ✅ | |
| **CPU Usage** | ✅ | ✅ | |
| **RAM** | ✅ | ✅ | |
| **Disk** | ✅ | ✅ | All drives |
| **Battery** | ✅ | ✅ | |
| **GPU Temperature** | ✅ | ✅ (Win/Linux) | macOS: no public API |
| **GPU Usage** | ✅ | ✅ (Win/Linux) | macOS: only with sudo |
| **Fan Speed** | ✅ | ✅ (Win/Linux) | |
| **WiFi SSID** | ✅ | ✅ | |
| **Uptime** | ✅ | ✅ | |
| **Active Window** | ✅ | ✅ (Win) | Linux/macOS: limited |
| **Webcam Status** | ❌ | ✅ | v3.0+: sensor if camera is active |
| **Commands from HA** | ✅ | ✅ | Shutdown, Restart, Lock, etc. |
| **Screenshot** | ✅ (Snipping Tool) | ✅ (Real + Upload) | Directly as HA event |
| **Notifications** | ✅ | ✅ | |
| **Actionable Notifications** | ✅ | ✅ | v3.0+: buttons in notifications |
| **Quick Actions** | ✅ | ✅ | v3.0+: hotkey + popup |
| **Media Player** | ✅ | ❌ | Not planned – WebSocket-only |
| **Embedded Dashboard** | ✅ (WebView) | ✅ (Win: WebView2) | Linux/Mac: browser |
| **Auto-Update** | ✅ | ✅ | |
| **System Tray** | ✅ | ✅ (Win) | Linux: daemon, Mac: dock |
| **Settings** | GUI | GUI (Win) / Config (Linux/Mac) | |
| **Localization** | English | 6 languages (de, en, es, fr, zh, ja) | |
| **macOS** | ❌ | ✅ (Community Test) | |
| **Linux** | ❌ | ✅ | Headless available |
| **License** | MIT | GPL v3 | HA DeskLink is copyleft |

### Architecture

**HASS.Agent:** `App ←→ MQTT Broker ←→ HA (+ HACS Integration)`

**HA DeskLink:** `App ←→ Home Assistant (WebSocket + Webhook)` – no extra software needed.

### What HA DeskLink Does NOT Support

| Feature | Why Not |
|---|---|
| **Media Player** | Would require a custom HA integration. mobile_app doesn't offer a media player entity. |
| **MQTT** | Intentionally omitted. mobile_app is simpler. |
| **WebView on Linux/macOS** | WebView2 not stable on Linux/macOS. Dashboard opens in browser. |

### Migrating from HASS.Agent

1. Install HA DeskLink
2. Enter HA URL + Long-Lived Token
3. Device registers automatically in HA
4. Uninstall HASS.Agent – old entities remain in HA until manually deleted
5. Adjust automations to `sensor.ha_desklink_*`

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
| **Windows Defender: VulnerableDriver** | See below ⚠️ |

### ⚠️ Windows Defender – "Vulnerable Driver: WinNT/Winring0"

Windows Defender may report **VulnerableDriver:WinNT/Winring0** for the file `HA_DeskLink.sys`.

**This is a false positive!** Here's why:

- HA DeskLink uses **LibreHardwareMonitorLib** for hardware sensors (CPU temp, GPU temp, fan speed)
- This library uses the **WinRing0 driver** to read hardware sensors at kernel level
- WinRing0 is a **legitimate, open-source driver** – it needs kernel access to read temperature sensors
- Microsoft Defender flags all kernel drivers as "potentially dangerous", even when they're harmless

**How to allow the driver:**

1. Open Windows Defender → **Protection history**
2. Find the entry "VulnerableDriver:WinNT/Winring0"
3. Click **Actions** → **Allow**

Or alternatively:

1. Windows Settings → **Privacy & Security** → **Windows Security**
2. **Virus & threat protection** → **Protection history**
3. Find the driver entry → **Allow**

**Without admin rights** HA DeskLink still works – you'll just miss CPU/GPU temperature and fan speed. All other sensors and commands work normally.
| CPU temp empty (Win) | Run as Administrator |
| Webcam always "off" | Camera present? Linux: `ls /dev/video*` |
| SSL error | Disable SSL verification in settings |

---

<a id="platform-comparison-en"></a>

### Platform Comparison (Windows / Linux / macOS)

| Feature | Windows | Linux | macOS | Explanation |
|---|:---:|:---:|:---:|---|
| **GUI** | WinForms | Avalonia | Avalonia | |
| **Embedded Dashboard** | ✅ WebView2 | ❌ Browser | ❌ Browser | WebView2 not stable on Linux/Mac |
| **System Tray** | ✅ | ❌ Daemon | ❌ Dock | |
| **Quick Actions Hotkey** | ✅ Ctrl+Shift+H | ❌ Button | ❌ Button | Global hotkeys Win only |
| **Screenshot Method** | CopyFromScreen | gnome-screenshot | screencapture | |
| **Webcam Detection** | WMI | /dev/video* | ioreg/lsof | |
| **Token Storage** | DPAPI | config.json | Keychain | |
| **Admin Required** | Yes (HW sensors) | No | No | |
| **Daemon Mode** | ❌ | ✅ systemd | ❌ | |
| **Installer** | ✅ InnoSetup | tar.gz | DMG | |
| **Sensors** | All + GPU/Fan | All + GPU/Fan | All - GPU/Fan | macOS has no public GPU API |
| **Commands** | All | All + suspend | All - suspend + brightness | |
| **Actionable Notifications** | Dialog with buttons | Auto-execute | Auto-execute | Linux/Mac: no interactive buttons |
| **Localization** | 6 languages | 6 languages | 6 languages | de, en, es, fr, zh, ja |

---

**Idee / Idea:** Fabian Kirchweger | **Code:** GLM-5.1 (via OpenClaw) | **Lizenz / License:** GPL v3