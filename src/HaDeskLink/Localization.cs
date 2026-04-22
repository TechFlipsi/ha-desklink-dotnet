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
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace HaDeskLink;

/// <summary>
/// Simple localization system using JSON language files.
/// Users can add their own language by creating a new JSON file in the Lang/ directory.
/// </summary>
public static class Localization
{
    private static Dictionary<string, string> _strings = new();
    private static string _currentLanguage = "de";

    /// <summary>
    /// Available languages (auto-detected from Lang/ directory)
    /// </summary>
    public static List<string> AvailableLanguages { get; private set; } = new() { "de" };

    /// <summary>
    /// Friendly names for languages
    /// </summary>
    private static readonly Dictionary<string, string> LanguageNames = new()
    {
        { "de", "Deutsch" },
        { "en", "English" },
        { "es", "Español" },
        { "fr", "Français" },
        { "zh", "中文" },
        { "ja", "日本語" },
    };

    /// <summary>
    /// Get friendly name for a language code
    /// </summary>
    public static string GetLanguageName(string code) =>
        LanguageNames.TryGetValue(code, out var name) ? name : code.ToUpper();

    /// <summary>
    /// Load a language. Falls back to German if the file doesn't exist.
    /// </summary>
    public static void LoadLanguage(string languageCode)
    {
        _currentLanguage = languageCode;
        var langDir = Path.Combine(AppContext.BaseDirectory, "Lang");
        if (!Directory.Exists(langDir))
            langDir = Path.Combine(Path.GetDirectoryName(typeof(Localization).Assembly.Location)!, "Lang");

        // Scan available languages
        AvailableLanguages = new List<string>();
        if (Directory.Exists(langDir))
        {
            foreach (var file in Directory.GetFiles(langDir, "*.json"))
            {
                var code = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                AvailableLanguages.Add(code);
            }
        }
        if (AvailableLanguages.Count == 0)
            AvailableLanguages.Add("de");

        // Load requested language, fallback to German
        var langFile = Path.Combine(langDir, $"{languageCode}.json");
        if (!File.Exists(langFile))
            langFile = Path.Combine(langDir, "de.json");

        if (File.Exists(langFile))
        {
            try
            {
                var json = File.ReadAllText(langFile);
                _strings = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
            }
            catch
            {
                _strings = new();
            }
        }
    }

    /// <summary>
    /// Get a localized string by key. Falls back to key itself if not found.
    /// </summary>
    public static string Get(string key)
    {
        return _strings.TryGetValue(key, out var value) ? value : key;
    }

    /// <summary>
    /// Get a localized string with format arguments. E.g. Get("update_failed", ex.Message)
    /// </summary>
    public static string Get(string key, params object[] args)
    {
        var template = Get(key);
        try { return string.Format(template, args); }
        catch { return template; }
    }

    /// <summary>
    /// Current language code
    /// </summary>
    public static string CurrentLanguage => _currentLanguage;
}