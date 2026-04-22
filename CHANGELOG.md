# Changelog


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