
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
using System.Text.Json;

namespace HaDeskLink;

/// <summary>
/// Application configuration persisted as JSON.
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

    private string ConfigPath => Path.Combine(ConfigDir, "config.json");

    public static Config Load()
    {
        Directory.CreateDirectory(ConfigDir);
        var path = Path.Combine(ConfigDir, "config.json");
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Config>(json) ?? new Config();
        }
        return new Config();
    }

    public void Save()
    {
        Directory.CreateDirectory(ConfigDir);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }

    public static string GetConfigDir() => ConfigDir;
}