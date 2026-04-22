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
    public bool VerifySsl { get; set; } = false;
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
    /// Encrypted HA token (DPAPI protected). When set, HaToken is cleared.
    /// If empty, HaToken is used (migration from old config).
    /// </summary>
    public string? HaTokenEncrypted { get; set; }

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

        // Create a copy for serialization that has HaToken cleared
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
            QuickActions = QuickActions
        };

        var json = JsonSerializer.Serialize(saveConfig, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }

    public static string GetConfigDir() => ConfigDir;
}