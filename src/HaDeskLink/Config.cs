// HA DeskLink - Home Assistant Companion App
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
#nullable enable
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HaDeskLink;

/// <summary>
/// Application configuration persisted as JSON.
/// HA Token is encrypted with DPAPI (user-level) for security.
/// If a hacker gains access to the PC, the token cannot be decrypted
/// by a different user or on a different machine.
/// </summary>
public class Config
{
    private static readonly string AppName = "HA_DeskLink";
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);

    public string HaUrl { get; set; } = "";
    public string HaToken { get; set; } = "";
    public bool VerifySsl { get; set; } = true;
    public bool Autostart { get; set; } = true;
    public int SensorInterval { get; set; } = 30;
    /// <summary>
    /// Update channel: "stable" = only stable releases, "prerelease" = includes beta/pre-release versions
    /// </summary>
    public string UpdateChannel { get; set; } = "stable";
    /// <summary>
    /// UI language code (de, en, es, fr, zh, ja). Default: de
    /// </summary>
    public string Language { get; set; } = "de";
    /// <summary>
    /// Quick Actions: JSON array of { entityId, name } objects.
    /// Example: [{"entityId":"light.living_room","name":"Wohnzimmer"}]
    /// </summary>
    public string QuickActions { get; set; } = "[]";
    /// <summary>
    /// UI theme: "system" (follow Windows), "light", or "dark". Default: system
    /// </summary>
    public string Theme { get; set; } = "system";
    /// <summary>
    /// Quick Actions hotkey modifiers. Default: Ctrl+Shift
    /// Values: "ctrl_shift", "ctrl_alt", "ctrl", "alt", "shift", "none"
    /// </summary>
    public string HotkeyModifiers { get; set; } = "ctrl_shift";
    /// <summary>
    /// Quick Actions hotkey key. Default: H (0x48)
    /// </summary>
    public string HotkeyKey { get; set; } = "H";
    /// <summary>
    /// Dashboard hotkey modifiers. Default: Ctrl+Shift
    /// </summary>
    public string HotkeyDashboardModifiers { get; set; } = "ctrl_shift";
    /// <summary>
    /// Dashboard hotkey key. Default: D
    /// </summary>
    public string HotkeyDashboardKey { get; set; } = "D";
    /// <summary>
    /// Settings hotkey modifiers. Default: Ctrl+Shift
    /// </summary>
    public string HotkeySettingsModifiers { get; set; } = "ctrl_shift";
    /// <summary>
    /// Settings hotkey key. Default: S
    /// </summary>
    public string HotkeySettingsKey { get; set; } = "S";
    /// <summary>
    /// Encrypted HA token (DPAPI protected). When set, HaToken is cleared.
    /// If empty, HaToken is used (migration from old config).
    /// </summary>
    public string? HaTokenEncrypted { get; set; }

    // MQTT Configuration (optional, auto-configured)
    public bool MqttEnabled { get; set; } = false;
    public string MqttBroker { get; set; } = "";
    public int MqttPort { get; set; } = 1883;
    public string MqttUsername { get; set; } = "";
    public string MqttPassword { get; set; } = "";           // runtime only, never saved to config file
    public string MqttPasswordEncrypted { get; set; } = "";  // encrypted version for persistence
    public bool MqttUseSsl { get; set; } = false;
    public bool MqttAutoConfigured { get; set; } = false;    // set by auto-setup
    /// <summary>
    /// Fallback MQTT broker address for auto-configuration (e.g., local network IP
    /// when HA URL is a domain name that may not resolve MQTT correctly).
    /// </summary>
    public string MqttBrokerFallback { get; set; } = "";

    // Music Assistant (optional)
    /// <summary>MA host/IP (empty = MA integration disabled).</summary>
    public string MaHost { get; set; } = "";
    /// <summary>MA API port (default 8095; TrueNAS app: 30278).</summary>
    public int MaPort { get; set; } = 8095;
    /// <summary>MA long-lived access token (runtime only, never saved to config file).</summary>
    public string MaToken { get; set; } = "";
    public string? MaTokenEncrypted { get; set; } = "";

    // Sendspin streaming (optional)
    /// <summary>Sendspin streaming enabled (player role on this PC). Default: false.</summary>
    public bool SendspinEnabled { get; set; } = false;
    /// <summary>Player name advertised to Music Assistant. Default: 'HA DeskLink PC'.</summary>
    public string SendspinPlayerName { get; set; } = "HA DeskLink PC";

    /// <summary>
    /// Webhook server bind address. "+" = all interfaces (default), "localhost" = local only.
    /// Set to "localhost" for better security when HA is on the same machine.
    /// </summary>
    public string WebhookBindAddress { get; set; } = "+";

    /// <summary>
    /// Notification position: "bottom_left", "bottom_right", "top_left", "top_right". Default: bottom_left
    /// </summary>
    public string NotificationPosition { get; set; } = "bottom_left";

    /// <summary>
    /// Monitor index for notifications (0 = primary, 1+ = specific monitor). Default: 0
    /// </summary>
    public int NotificationMonitor { get; set; } = 0;

    /// <summary>
    /// Custom Commands: JSON-Array von benutzerdefinierten Skripten/Befehlen
    /// die von Home Assistant getriggert werden können.
    /// Format: [{"command":"start_streaming","script":"C:\\Scripts\\stream.bat","name":"Start Streaming"}]
    /// </summary>
    public string CustomCommands { get; set; } = "[]";

    /// <summary>
    /// App Launchers: JSON-Array von Apps die von HA gestartet werden können.
    /// Format: [{"command":"launch_spotify","path":"spotify","name":"Spotify"}]
    /// </summary>
    public string AppLaunchers { get; set; } = "[]";

    /// <summary>
    /// Desktop-Widgets: JSON-Array von Widget-Definitionen.
    /// Format: [{"id":"ab12cd34","type":"sensorCard","entityId":"sensor.cpu_temp","name":"CPU",
    ///           "monitor":0,"offsetX":40,"offsetY":40,"clickThrough":false,
    ///           "entities":[{"entityId":"light.living","name":"Wohnzimmer"}]}]
    /// type: "sensorCard" | "toggleCard" | "multiToggleCard"
    /// </summary>
    public string Widgets { get; set; } = "[]";

    private string ConfigPath => Path.Combine(ConfigDir, "config.json");

    /// <summary>
    /// Encrypt a string using DPAPI (Current User scope).
    /// Only the same Windows user on the same machine can decrypt it.
    /// </summary>
    private static string EncryptString(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return "";
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encryptedBytes);
    }

    /// <summary>
    /// Decrypt a string using DPAPI (Current User scope).
    /// </summary>
    private static string DecryptString(string encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText)) return "";
        try
        {
            var encryptedBytes = Convert.FromBase64String(encryptedText);
            var plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            // Decryption failed (different user, different machine, corrupted data)
            return "";
        }
    }

    /// <summary>
    /// Load config and automatically migrate plaintext tokens to encrypted storage.
    /// </summary>
    public static Config Load()
    {
        Directory.CreateDirectory(ConfigDir);
        var path = Path.Combine(ConfigDir, "config.json");
        Config config;

        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            config = JsonSerializer.Deserialize<Config>(json) ?? new Config();
        }
        else
        {
            config = new Config();
        }

        // Migration: if HaTokenEncrypted is empty but HaToken has a value,
        // encrypt HaToken and clear the plaintext
        if (string.IsNullOrEmpty(config.HaTokenEncrypted) && !string.IsNullOrEmpty(config.HaToken))
        {
            config.HaTokenEncrypted = EncryptString(config.HaToken);
            config.HaToken = ""; // Clear plaintext
            config.Save(); // Save encrypted version immediately
        }
        else if (!string.IsNullOrEmpty(config.HaTokenEncrypted))
        {
            // Decrypt the token for use in the app
            var decrypted = DecryptString(config.HaTokenEncrypted);
            if (!string.IsNullOrEmpty(decrypted))
                config.HaToken = decrypted;
            // If decryption fails, HaToken remains empty – app will show error
        }

        // Migration: if MqttPasswordEncrypted is empty but MqttPassword has a value,
        // encrypt MqttPassword and clear the plaintext
        if (string.IsNullOrEmpty(config.MqttPasswordEncrypted) && !string.IsNullOrEmpty(config.MqttPassword))
        {
            config.MqttPasswordEncrypted = EncryptString(config.MqttPassword);
            config.MqttPassword = ""; // Clear plaintext
            config.Save(); // Save encrypted version immediately
        }
        else if (!string.IsNullOrEmpty(config.MqttPasswordEncrypted))
        {
            // Decrypt the MQTT password for use in the app
            var decrypted = DecryptString(config.MqttPasswordEncrypted);
            if (!string.IsNullOrEmpty(decrypted))
                config.MqttPassword = decrypted;
        }

        // Migration: if MaTokenEncrypted is empty but MaToken has a value,
        // encrypt MaToken and clear the plaintext
        if (string.IsNullOrEmpty(config.MaTokenEncrypted) && !string.IsNullOrEmpty(config.MaToken))
        {
            config.MaTokenEncrypted = EncryptString(config.MaToken);
            config.MaToken = ""; // Clear plaintext
            config.Save(); // Save encrypted version immediately
        }
        else if (!string.IsNullOrEmpty(config.MaTokenEncrypted))
        {
            // Decrypt the MA token for use in the app
            var decrypted = DecryptString(config.MaTokenEncrypted);
            if (!string.IsNullOrEmpty(decrypted))
                config.MaToken = decrypted;
        }

        return config;
    }

    /// <summary>
    /// Save config with encrypted token. Never saves HaToken in plaintext.
    /// </summary>
    public void Save()
    {
        Directory.CreateDirectory(ConfigDir);

        // Always encrypt the token before saving
        if (!string.IsNullOrEmpty(HaToken))
        {
            HaTokenEncrypted = EncryptString(HaToken);
        }

        // Always encrypt the MQTT password before saving
        if (!string.IsNullOrEmpty(MqttPassword))
        {
            MqttPasswordEncrypted = EncryptString(MqttPassword);
        }

        // Always encrypt the MA token before saving
        if (!string.IsNullOrEmpty(MaToken))
        {
            MaTokenEncrypted = EncryptString(MaToken);
        }
        else
        {
            // Token was cleared (or decryption failed on load) — also drop the
            // stale ciphertext so a cleared token really stays cleared.
            MaTokenEncrypted = null;
        }

        // Create a copy for serialization that has HaToken and MqttPassword cleared
        var saveConfig = new Config
        {
            HaUrl = HaUrl,
            HaToken = "", // NEVER save plaintext token
            VerifySsl = VerifySsl,
            Autostart = Autostart,
            SensorInterval = SensorInterval,
            UpdateChannel = UpdateChannel,
            Language = Language,
            HaTokenEncrypted = HaTokenEncrypted,
            QuickActions = QuickActions,
            Theme = Theme,
            HotkeyModifiers = HotkeyModifiers,
            HotkeyKey = HotkeyKey,
            HotkeyDashboardModifiers = HotkeyDashboardModifiers,
            HotkeyDashboardKey = HotkeyDashboardKey,
            HotkeySettingsModifiers = HotkeySettingsModifiers,
            HotkeySettingsKey = HotkeySettingsKey,
            MqttEnabled = MqttEnabled,
            MqttBroker = MqttBroker,
            MqttPort = MqttPort,
            MqttUsername = MqttUsername,
            MqttPassword = "", // NEVER save plaintext password
            MqttPasswordEncrypted = MqttPasswordEncrypted,
            MqttUseSsl = MqttUseSsl,
            MqttAutoConfigured = MqttAutoConfigured,
            MqttBrokerFallback = MqttBrokerFallback,
            MaHost = MaHost,
            MaPort = MaPort,
            MaToken = "", // NEVER save plaintext token
            MaTokenEncrypted = MaTokenEncrypted,
            CustomCommands = CustomCommands,
            AppLaunchers = AppLaunchers,
            WebhookBindAddress = WebhookBindAddress,
            NotificationPosition = NotificationPosition,
            NotificationMonitor = NotificationMonitor,
            Widgets = Widgets,
            SendspinEnabled = SendspinEnabled,
            SendspinPlayerName = SendspinPlayerName
        };

        var json = JsonSerializer.Serialize(saveConfig, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }

    public static string GetConfigDir() => ConfigDir;
}