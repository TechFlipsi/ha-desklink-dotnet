## v5.0.5 (2026-09-10)

### 🎵 Music Assistant Integration (neu)
- **MA voll nativ in der App** – Music Assistant direkt in HA DeskLink steuern, ohne die MA-Web-UI: Suche über die komplette Bibliothek (Spotify, YouTube Music, TuneIn, lokale Dateien), Warteschlange anzeigen und umsortieren, Play/Pause/Next/Previous, Lautstärke, Shuffle/Repeat
- **Player-Auswahl in der Sidebar** – alle MA-Player live mit Abspielstatus
- **Verbindungstest** – Einstellungen → Music Assistant: Host + Port + Token eintragen, Button testet die Verbindung sofort (MA auf TrueNAS, HA-Add-on oder Standalone)
- **Token verschlüsselt gespeichert** (DPAPI) — wie beim HA-Token
- **Live-Updates per Push** – Player- und Queue-Änderungen erscheinen sofort

### 🧩 Desktop-Widgets (neu)
- **Sensor-Karten direkt am Desktop** – z.B. Büro-Temperatur als kleine Karte, live aktualisiert, hinter allen Fenstern
- **Schalter-Karten** – Einzeltoggle oder 2–4 Schalter in einer Karte
- **Vollbild-kompatibel** – Widgets liegen auf der Desktop-Ebene: beim Zocken am Hauptmonitor unsichtbar, auf Monitor 2/3 bleiben sie sichtbar und aktualisieren weiter
- **Klick-Durchlässigkeit** – reine Anzeige-Widgets blockieren keine Klicks (konfigurierbar)
- **Widget-Editor** – Einstellungen → Widgets: anlegen, positionieren, testen
- **DPI-PerMonitorV2** – saubere Skalierung auf Monitoren mit unterschiedlicher Skalierung

### 🌍 i18n
- Neue Keys (`ma_*`, `widget_*`, `stream_*`) in alle Sprachen übernommen


## v5.0.4 (2026-08-09)

### 🔧 Bugfixes
- **Installer-Versionierung repariert** – Die Setup-EXE hatte noch den hartcodierten Dateinamen `HA_DeskLink_Setup_4.4.2.exe` statt der korrekten Versionsnummer. Der Auto-Updater fand dadurch keine neue Version. Die Version wird jetzt dynamisch aus der VERSION-Datei in den Installer injiziert.
- **Auto-Updater API-URL korrigiert** – Veraltete Repo-URL `ha-desklink-dotnet` durch `ha-desklink-windows` ersetzt
- **Discord-Webhook Release-Link korrigiert**
- **Inno Setup URLs aktualisiert** – Alte `FKirchweger/ha-desklink-dotnet` URLs durch `TechFlipsi/ha-desklink-windows` ersetzt

## v5.0.3 (2026-08-08)

### 🐛 Bugfixes
- SplitContainer Absturz beim Öffnen der Einstellungen behoben – durch Dock=Left + Dock=Fill Panels ersetzt

## v5.0.2 (2026-08-08)

### 🐛 Bugfixes
- SplitContainer SplitterDistance Absturz beim Einstellungs-Dialog behoben

## v5.0.1 (2026-08-08)

### ✨ Neue Features
- **Desktop UI Redesign** – SplitContainer Sidebar (200px) + Inhaltsbereich, Bottom Bar
- **Einstellungen mit Erklärungen** – Jede Einstellung hat jetzt einen Beschreibungstext und Beispiel
- **24 neue `desc_*` i18n-Keys** in allen 15 Sprachen

## v5.0.0 (2026-08-08)

### 🔒 Security
- **DPAPI Token-Verschlüsselung** – HA-Token wird mit Windows DPAPI (CurrentUser) verschlüsselt, nie mehr im Klartext gespeichert
- **MQTT-Passwort-Verschlüsselung** – MQTT-Passwort ebenfalls DPAPI-verschlüsselt
- **Login-Retry-Limit** – Maximal 3 fehlgeschlagene Auth-Versuche

### ✨ Neue Features
- **WebView2 Dashboard** mit automatischer Installation
- **6 neue Sensoren**: idle_time, audio_volume, mic_active, webcam_active, gpu_memory, brightness
- **Neue Befehle**: lock_screen, sleep, hibernate, volume_up/down, media_play_pause/next/previous, brightness_up/down
- **Actionable Notifications** – Benachrichtigungen mit Aktions-Buttons
- **Quick Actions** – Konfigurierbare Hotkeys (Ctrl+Shift+H/D/S)
- **Custom Commands & App Launchers** – Eigene Skripte und App-Starts von HA aus
- **Notification Position & Monitor** – Konfigurierbare Position und Monitor für Toast-Notifications
- **Auto-Update von GitHub Releases**
- **Nativer Windows Tray**

### 🌍 i18n
- **15 Sprachen** – Deutsch, Englisch, Spanisch, Französisch, Chinesisch, Japanisch + 9 weitere
- **Settings Redesign** – Moderne Sidebar-Navigation mit Beschreibungstexten

### 🏗️ Architektur
- **LibreHardwareMonitor** für CPU/GPU-Temperaturen (ersetzt WinRing0)
- **App-Manifest** mit `requireAdministrator` – App läuft immer als Admin
- **Task Scheduler Autostart** mit `HighestAvailable` – kein UAC-Prompt beim Autostart

## v4.4.0 (2026-05-24)

### MQTT Support (optional)
- MQTT support (optional, auto-configure from Home Assistant)
- Media Player entity (now playing info, play/pause/volume controls)
- PC Status binary sensor (instant online/offline detection)
- Zero-config MQTT setup wizard on first launch
- MQTT settings in main settings
- Smart routing: MQTT for sensors + commands, WebSocket for notifications
- Last Will Testament (instant offline detection)
- Auto-reconnect with exponential backoff

### 🌍 Neue Lokalisierungs-Keys
- 27 neue MQTT/MediaPlayer/PCStatus-Keys in allen 6 Sprachen

## v4.2.0 (2026-05-23)

### 📊 Neue Sensoren
- **idle_time** – Sekunden seit letzter Benutzereingabe
- **audio_volume** – System-Lautstärke 0-100%
- **audio_mute** – Stummschaltung (on/off)
- **mic_active** – Mikrofon in Benutzung (binary sensor)
- **webcam_active** – Webcam in Benutzung (binary sensor)
- **gpu_memory_used / gpu_memory_total** – GPU VRAM (NVIDIA/AMD)

### ⚡ Neue Befehle
- **lock_screen**, **sleep**, **hibernate** – PC-Energiebefehle
- **volume_up**, **volume_down**, **volume_mute** – Lautstärke-Steuerung
- **media_play_pause**, **media_next**, **media_previous** – Mediensteuerung

### 🌍 Lokalisierung
- 22 neue Lokalisierungs-Keys in allen 6 Sprachen (de, en, es, fr, zh, ja)

### 🐛 Bugfixes
- async void Crash-Prävention
- WebhookServer catch-all Exception-Handling
- NotificationToast Cross-Thread-Fix
- PerformanceCounter/Ping Dispose-Fix
- GPU Double-Pass-Logik korrigiert

## v4.1.0 (2026-05-23)

### 🎨 Einstellungen Redesign
- **Duplikat-Buttons entfernt**: Die zwei verwirrenden Reconnect-Buttons ("Verbindung wird erneut hergestellt" + "Erneut verbinden") sind jetzt ein einziger Button: **🔄 Neu verbinden** – der auch Login-Block zurücksetzt falls nötig
- **No-Op Button entfernt**: "💾 Quick Actions gespeichert!" war ein Button der nichts tat (Quick Actions speichern automatisch) – entfernt
- **Klarere Button-Namen**: "Gerät zurücksetzen" → "🔑 Geräte-ID zurücksetzen", "Sensoren neu registrieren" → "📊 Sensoren neu registrieren"
- **Tooltips auf ALLEN Buttons**: Jeder Button hat jetzt einen Tooltip der genau erklärt was er macht
- **URL & Token Validierung**: Leere oder ungültige Eingaben werden beim Speichern abgelehnt
- **Sensor-Intervall Minimum**: 10 Sekunden (statt 5) – schont Home Assistant
- **Button-Breite**: AutoSize statt starrer 155px – kein Abschneiden mehr
- **Neu verbinden** nutzt jetzt Task.Run statt .Wait() – kein UI-Freeze mehr

### 🔔 Benachrichtigung Redesign
- **Modernes Toast-Design**: P/Invoke (CreateRoundRectRgn) entfernt, jetzt .NET GraphicsPath für abgerundete Ecken
- **WebSocket-Verbindung**: Zeigt jetzt das moderne dunkle Toast statt Windows-BalloonTip
- **Login-Fehler**: Zeigt jetzt das dunkle Toast statt BalloonTip
- **Neue `ShowConnectionToast`-Methode**: Grüner Akzent für Verbindungsstatus
- **Timer-Dispose-Bug fix**: _autoCloseTimer wird jetzt ordentlich gestoppt vor Dispose

### 🧹 Aufräumung
- 33 tote Localization-Keys entfernt (6 Sprachdateien)
- Tray-Menü: "Quick Actions (Ctrl+Shift+H)" → zeigt jetzt den tatsächlich konfigurierten Hotkey dynamisch
- `ShowWebSocketNotification` entfernt (unnötiger Wrapper)

# Changelog

## [v4.0.0] - 2026-05-23
- 🆕 **Neu:** Embedded HA Dashboard mit WebView2 (einmaliges Login mit Benutzername & Passwort, Session bleibt erhalten)
- 🎨 **Redesign:** Moderne Notification-Popups (Dark Theme, abgerundete Ecken)
- 🎨 **Redesign:** Modernisierte Einstellungen
- ⚠️ **Breaking:** LibreHardwareMonitorLib + WinRing0 komplett entfernt → treiberloser Sensor-Stack
- 🔒 **Security:** Keine Windows Defender "Vulnerable Driver" Warnung mehr
- 📊 **Sensoren:** WMI + PerformanceCounter + nvidia-smi + AMD ADLX (treiberlos)



## [v3.0.17] - 2026-04-23
- 🐛 **Bug Fix:** Quick Actions wurden nicht gespeichert – JSON-Serialisierung erzeugte PascalCase statt camelCase
- 🐛 **Bug Fix:** Config.Save() hat Theme und Hotkey-Einstellungen verloren (fehlten im saveConfig-Klon)

## [v3.0.16] - 2026-04-22
- 🎨 **Quick Actions komplett vereinfacht** – ListBox statt DataGridView, einfaches Hinzufügen/Bearbeiten/Entfernen per Dialog
- 🐛 **Bug Fix:** DataGridViewComboBox-Ausnahme behoben (Wert nicht in Items-Liste)
- 🐛 **Bug Fix:** Quick Actions Hotkey zeigt jetzt immer aktuelle Actions (statt alte geladene)
- ✨ **Neu:** Entity-Auswahl per Dropdown mit automatischer Friendly-Name-Übernahme
- ✨ **Neu:** Löschen-Button direkt im Bearbeiten-Dialog

## [v3.0.15] - 2026-04-22
- 🐛 Fix: readonly field assignment in QuickActionHandler
- 🐛 Fix: DataGridViewComboBoxColumn has no DropDownStyle property → set via EditingControlShowing event

## [v3.0.14] - 2026-04-22
- ✨ **Neu:** Drei konfigurierbare Hotkeys – Quick Actions (Ctrl+Shift+H), Dashboard (Ctrl+Shift+D), Einstellungen (Ctrl+Shift+S)
- 🐛 **Bug Fix:** Entity-Dropdown funktioniert jetzt mit ComboBox-Spalte
- 🐛 **Bug Fix:** Hotkey-IDs waren identisch (Kollision) – jetzt eindeutige IDs für jeden Hotkey
- 🎨 **Redesign:** Einstellungen nutzen volle Breite, scrollbar, responsive

## [v3.0.13] - 2026-04-22
- 🐛 **Bug Fix:** Entity-Dropdown im Quick Actions Editor – ComboBox statt TextBox für Entity-Auswahl

## [v3.0.12] - 2026-04-22
- 🎨 **Redesign:** Einstellungen komplett neu gestaltet – volle Breite, scrollbar, responsive Layout

## [v3.0.11] - 2026-04-22
- ✨ **Neu:** Konfigurierbarer Hotkey für Quick Actions (Modifier + Taste wählbar)
- 🎨 **Redesign:** Einstellungen neu gestaltet mit GroupBoxes und farbigen Buttons
- ✨ **Neu:** Entity-Suche im Quick Actions Editor (AutoComplete aus HA)
- ✨ **Neu:** JSON-Editor für Quick Actions

## [v3.0.10] - 2026-04-22
- ✨ **Neu:** JSON-Editor für Quick Actions – direkte Bearbeitung des JSON für Power-User

## [v3.0.9] - 2026-04-22
- ✨ **Neu:** Entity-Suche im Quick Actions Editor – Entities direkt aus HA laden und per AutoComplete auswählen

## [v3.0.8] - 2026-04-22
- ✨ **Neu:** Dark/Light/System-Theme in den Einstellungen
- ✨ **Neu:** Sensoren neu registrieren Button (fehlt Sensoren nach Update)
- ✨ **Neu:** Quick Actions direkt in den Einstellungen konfigurierbar
- ✨ **Neu:** Gerät zurücksetzen Button
- 🐛 **Bug Fix:** Brightness-Befehle nutzen jetzt Virtual Keys statt WMI

## [v3.0.7] - 2026-04-22
- 🐛 **Bug Fix:** brightness_up/down verwenden jetzt Virtual Keys (wie volume) statt WMI – funktioniert auf allen Monitoren!

## [v3.0.6] - 2026-04-22
- 🐛 **Bug Fix:** Notification-Parsing – unterstützt jetzt verschachteltes data.data.command Format (HA mobile_app)
- 🐛 **Bug Fix:** Brightness-Befehl – PowerShell-Fallback wenn WMI nicht funktioniert (Windows)
- 🐛 **Bug Fix:** fullscreen_app Sensor entfernt (Duplikat) auf Linux + Mac
- ✨ **Neu:** ha_desklink_version Sensor auf allen 3 Plattformen

## [v3.0.5] - 2026-04-22
- ✨ **Neu:** ha_desklink_version Sensor – zeigt die aktuelle App-Version in Home Assistant

## [v3.0.4] - 2026-04-22
- 🐛 **Bug Fix:** Fullscreen-Erkennung für Multi-Monitor verbessert (WorkArea-basiert)
- 🐛 **Bug Fix:** fullscreen_app Sensor entfernt (duplikat von active_window)
- 🐛 **Bug Fix:** Webcam-Sensor entfernt (unzuverlässig auf Windows)

## [v3.0.3] - 2026-04-22
- 🐛 **Bug Fix:** Fullscreen-Erkennung für Multi-Monitor-Setups verbessert (WorkArea statt Monitor-Rect)
- 🐛 **Bug Fix:** Webcam-Sensor prüft jetzt WMI-Status-Änderung + Videokonferenz-Prozesse (statt nur Installations-Status)

## [v3.0.2] - 2026-04-22
- 🐛 **Bug Fix:** Webcam-Sensor zeigt jetzt korrekt "on/off" (prüft ob Kamera in Benutzung, nicht nur ob installiert)
- 🐛 **Bug Fix:** Fullscreen-Erkennung verbessert – erkennt jetzt auch Browser mit F11 und maximierte Fenster

## [v3.0.1] - 2026-04-22
- 🐛 **Bug Fix:** Token-Entschlüsselung gibt leeren String zurück → keine HA-Verbindung mehr (verhindert IP-Sperre durch zu viele fehlgeschlagene Auth-Versuche)
- 🐛 **Bug Fix:** WebhookServer-Crash durch disposed CancellationTokenSource (Windows)
Alle nennenswerten Änderungen an diesem Projekt werden hier dokumentiert.

## [v3.0.0] - 2026-04-22
- 🔔 **Actionable Notifications** – Benachrichtigungen mit Aktions-Buttons (z.B. "Ausschalten", "Ignorieren"). Unterstützt Windows Toast Notifications mit Fallback auf Dialog. HA sendet `actions`-Array in Notification-Data.
- ⚡ **Quick Actions** – Globaler Hotkey (Ctrl+Shift+H) öffnet Popup mit HA-Entity-Toggle-Buttons. Konfigurierbar in Einstellungen (Entity-ID + Name).
- 📸 **Screenshot-Verbesserung** – Echte Bildschirmfoto-Funktion (Graphics.CopyFromScreen), speichert PNG und sendet als HA-Event (`ha_desklink_screenshot`). Neuer Befehl `screenshot_save`, alter `screenshot` → `snipping_tool`.
- 📷 **Webcam-Sensor** – Neuer Sensor `webcam_active` (on/off) zeigt ob eine Webcam aktiv ist.
- 🌍 **Neue Lokalisierungs-Keys** für alle 6 Sprachen (de, en, es, fr, zh, ja)

## [v2.2.0] - 2026-04-22
- 🖥️ **Vollbild-Sensor** – zeigt welches Programm im Vollbild läuft (`sensor.ha_desklink_fullscreen_app`) + Ja/Nein (`binary_sensor.ha_desklink_fullscreen`)
- 📺 **Monitor-Layout-Sensor** – aktives Monitor-Layout (`sensor.ha_desklink_monitor_layout`, z.B. "1+2")
- ☀️ **Helligkeit steuern** – neue Befehle `brightness_up` (+10%), `brightness_down` (-10%), `brightness:50` (absolut) + Sensor `sensor.ha_desklink_brightness`
- 🌍 **Mehrsprachigkeit** – Deutsch (Standard), Englisch, Spanisch, Französisch, Chinesisch, Japanisch – umschaltbar in Einstellungen
- 🌍 Community kann eigene Sprachdateien hinzufügen (JSON im Lang/-Ordner)

## [v2.1.1] - 2026-04-22
- Task Scheduler Autostart mit hoher Priorität, keine Verzögerung, läuft auch im Akkubetrieb
- Discord-Community-Link im Tray-Menü
- Lizenz auf GPL v3 geändert (Closed-Source-Nutzung nicht mehr erlaubt)
- CREDITS.md hinzugefügt (KI-Attribution)
- Englische README hinzugefügt (Deutsch = Original)
- macOS-Hinweis: Keine Mac-Hardware zum Testen verfügbar

## [v2.1.0] - 2026-04-21
- Task Scheduler für Autostart (höchste Privilegien, kein UAC-Prompt)
- requireAdministrator-Manifest für automatische Admin-Rechte
- Regelmäßige Update-Prüfung alle 2 Stunden
- CPU-Temperatur: Nur Core-Sensoren (nicht Package/TCTL – zu hohe Werte auf AMD)
- GPU-Sensoren: Alle GPU-Hardware durchlaufen, GPU-Lüfter hinzugefügt, SuperIO-Controller aktiviert

## [v2.0.9] - 2026-04-21
- CPU-Temperatur-Fix: Nur Core-Sensoren verwendet (Package/TCTL auf AMD zu hoch)
- Hardware-Update erzwungen für frische Sensor-Werte
- Akkustand auf ganze Prozent gerundet

## [v2.0.8] - 2026-04-21
- Lüfter-Drehzahlen (CPU, GPU, Mainboard) als Sensoren hinzugefügt
- Downgrade-Schutz: Nur Upgrades erlaubt
- Nur-Änderungs-Updates für Sensoren (reduziert HA-Traffic)
- Persistente Geräte-ID mit Reset-Button

## [v2.0.7] - 2026-04-20
- WebSocket Push-Notifications korrigiert (Auth, Auto-Reconnect, SSL-Bypass für selbstsignierte Zertifikate)
- Toast-Benachrichtigung bei WebSocket-Verbindung
- Auto-Update: Nur Upgrades, kein Downgrade mehr
- Pre-Release-Warnung entfernt – v2.0.7 ist stabil

## [v2.0.6] - 2026-04-20
- WebSocket-basierte Push-Notifications (kein lokaler IP-Port nötig)
- Update-Kanal-Wähler (stable/pre-release)
- Auto-Updater lädt herunter und installiert automatisch
- Admin-Only-Installer

## [v2.0.5] - 2026-04-20
- Weitere Sensoren: CPU-Takt, WiFi-SSID+Signal, Prozessanzahl, Auslagerungsdatei, aktives Fenster, Netzwerk-Upload/Download

## [v2.0.4] - 2026-04-19
- PC-Befehle über HA-Benachrichtigungen (keine extra HA-Konfiguration nötig)
- Lautstärke-Steuerung (mute/lauter/leiser)
- Monitor an/aus
- Screenshot-Befehl
- IP-Adresse und Online/Offline-Sensor
- Detaillierte README mit Befehls-Referenz

## [v2.0.3] - 2026-04-19
- WebView2-Datenverzeichnis-Fehler behoben
- HA-Benachrichtigungen auf PC empfangbar
- Webhook-Server für Befehle und Benachrichtigungen

## [v2.0.2] - 2026-04-19
- Inno-Setup-Installer-Fix (hardcoded Version)

## [v2.0.1] - 2026-04-19
- Proper Inno Setup Installer (Program Files, Startmenü, Desktop-Verknüpfung, Deinstaller)
- WebView2 Auto-Install-Prompt
- Startmenü-Verknüpfung bei Autostart
- Globaler Exception-Handler + LibreHardwareMonitor-Fallback
- Icon für App, Tray und Installer

## [v2.0.0] - 2026-04-18
- Kompletter Rewrite von Python auf C# / .NET 8
- LibreHardwareMonitorLib für echte CPU/GPU-Temperaturen
- WebView2-Dashboard (eingebettetes HA-Dashboard)
- System Tray mit Status und Steuerung
- Sensoren: CPU, RAM, Laufwerke, Akku, Uptime
- Autostart-Funktion
- Setup-Wizard für HA-URL und Token

---

Das Format basiert auf [Keep a Changelog](https://keepachangelog.com/de/).