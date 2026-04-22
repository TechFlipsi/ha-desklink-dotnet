# HASS.Agent vs. HA DeskLink

📝 **Sprache / Language:** [Deutsch](#deutsch) | [English](#english)

---

<a id="deutsch"></a>

# Deutsch

Du überlegst, von HASS.Agent zu HA DeskLink zu wechseln? Oder suchst die passende Companion-App für Home Assistant? Hier findest du alle Unterschiede auf einen Blick.

## Warum HA DeskLink?

HASS.Agent ist ein großartiges Projekt – aber es erfordert **MQTT** und eine **separate Integration** in Home Assistant. HA DeskLink geht einen anderen Weg: Es nutzt das **mobile_app-Protokoll**, das auch die offizielle Handy-App verwendet. Das bedeutet: **Keine Extra-Integration, kein MQTT-Broker nötig.** Einfach installieren, Token eingeben, fertig.

## Funktionen im Vergleich

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

## Architektur

**HASS.Agent:** `App ←→ MQTT-Broker ←→ HA (+ HACS-Integration)`

**HA DeskLink:** `App ←→ Home Assistant (WebSocket + Webhook)` – keine Extra-Software nötig.

## Was HA DeskLink NICHT kann

| Feature | Warum nicht |
|---|---|
| **Media Player** | Würde eigene HA-Integration erfordern. mobile_app bietet keine Media-Player-Entity. |
| **MQTT** | Bewusst weggelassen. mobile_app ist einfacher. |
| **WebView auf Linux/macOS** | WebView2 nicht stabil verfügbar. Dashboard öffnet im Browser. |

## Migration von HASS.Agent

1. HA DeskLink installieren
2. HA-URL + Long-Lived Token eingeben
3. Gerät registriert sich automatisch in HA
4. HASS.Agent deinstallieren – alte Entities in HA bleiben bis man sie löscht
5. Automatisierungen auf `sensor.ha_desklink_*` anpassen

---

<a id="english"></a>

# English

Thinking about switching from HASS.Agent to HA DeskLink? Or looking for the right companion app for Home Assistant? Here are all the differences at a glance.

## Why HA DeskLink?

HASS.Agent is a great project – but it requires **MQTT** and a **separate integration** in Home Assistant. HA DeskLink takes a different approach: it uses the **mobile_app protocol**, the same one the official mobile app uses. This means: **No extra integration, no MQTT broker needed.** Just install, enter your token, and you're done.

## Feature Comparison

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

## Architecture

**HASS.Agent:** `App ←→ MQTT Broker ←→ HA (+ HACS Integration)`

**HA DeskLink:** `App ←→ Home Assistant (WebSocket + Webhook)` – no extra software needed.

## What HA DeskLink Does NOT Support

| Feature | Why Not |
|---|---|
| **Media Player** | Would require a custom HA integration. mobile_app doesn't offer a media player entity. |
| **MQTT** | Intentionally omitted. mobile_app is simpler. |
| **WebView on Linux/macOS** | WebView2 not stable on Linux/macOS. Dashboard opens in browser. |

## Migrating from HASS.Agent

1. Install HA DeskLink
2. Enter HA URL + Long-Lived Token
3. Device registers automatically in HA
4. Uninstall HASS.Agent – old entities remain in HA until manually deleted
5. Adjust automations to `sensor.ha_desklink_*`

---

**Idee / Idea:** Fabian Kirchweger | **Code:** GLM-5.1 (via OpenClaw) | **Lizenz / License:** GPL v3