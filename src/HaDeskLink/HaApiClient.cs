
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
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace HaDeskLink;

/// <summary>
/// Home Assistant mobile_app API client.
/// </summary>
public class HaApiClient
{
    private readonly HttpClient _http;
    private string _haUrl = "";
    private string _webhookId = "";
    private string _cloudUrl = "";
    private string _deviceId = "";
    private string _token = "";
    private readonly string _configDir;

    private string WebhookUrl => string.IsNullOrEmpty(_cloudUrl)
        ? $"{_haUrl}/api/webhook/{_webhookId}"
        : _cloudUrl;

    public HaApiClient(string configDir, bool verifySsl = false)
    {
        _configDir = configDir;
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (msg, cert, chain, errors) => !verifySsl || errors == System.Net.Security.SslPolicyErrors.None
        };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
    }

    public void SetToken(string token)
    {
        _token = token;
        _http.DefaultRequestHeaders.Clear();
        _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
    }

    public async Task RegisterAsync(string haUrl, string token)
    {
        _haUrl = haUrl.TrimEnd('/');
        SetToken(token);

        // Reuse existing device_id if we have one (prevents duplicate devices in HA)
        var existingDeviceId = LoadDeviceId();
        var deviceId = !string.IsNullOrEmpty(existingDeviceId) ? existingDeviceId : Guid.NewGuid().ToString();

        var is64 = Environment.Is64BitOperatingSystem;
        var payload = new Dictionary<string, object>
        {
            ["app_id"] = "ha_desklink",
            ["app_name"] = "HA DeskLink",
            ["app_version"] = GetVersion(),
            ["device_name"] = Environment.MachineName,
            ["device_id"] = deviceId,
            ["os_name"] = "Windows",
            ["os_version"] = Environment.OSVersion.VersionString,
            ["manufacturer"] = "Custom",
            ["model"] = $"PC ({(is64 ? "x64" : "x86")})",
            ["supports_encryption"] = false,
            ["app_data"] = new Dictionary<string, object>
            {
                ["push_websocket_channel"] = true,
            },
        };

        var json = JsonSerializer.Serialize(payload);
        var resp = await _http.PostAsync($"{_haUrl}/api/mobile_app/registrations",
            new StringContent(json, Encoding.UTF8, "application/json"));
        resp.EnsureSuccessStatusCode();

        var data = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        _webhookId = data.RootElement.GetProperty("webhook_id").GetString() ?? "";
        _cloudUrl = data.RootElement.TryGetProperty("cloudhook_url", out var cu) ? cu.GetString() ?? "" : "";
        _deviceId = data.RootElement.TryGetProperty("device_id", out var di) ? di.GetString() ?? "" : "";

        SaveRegistration(haUrl, token);
    }

    public string GetWebhookId() => _webhookId;

    /// <summary>
    /// Update registration with push_websocket_channel so HA knows this device
    /// receives notifications via WebSocket (not push_url).
    /// Called after registration.
    /// </summary>
    public async Task UpdateRegistrationAsync()
    {
        if (string.IsNullOrEmpty(_webhookId)) return;

        var is64 = Environment.Is64BitOperatingSystem;
        var payload = new Dictionary<string, object>
        {
            ["type"] = "update_registration",
            ["data"] = new Dictionary<string, object>
            {
                ["app_version"] = GetVersion(),
                ["device_name"] = Environment.MachineName,
                ["manufacturer"] = "Custom",
                ["model"] = $"PC ({(is64 ? "x64" : "x86")})",
                ["os_version"] = Environment.OSVersion.VersionString,
                ["app_data"] = new Dictionary<string, object>
                {
                    ["push_websocket_channel"] = true,
                },
            },
        };
        var json = JsonSerializer.Serialize(payload);
        await _http.PostAsync(WebhookUrl, new StringContent(json, Encoding.UTF8, "application/json"));
    }

    public bool LoadRegistration()
    {
        var path = Path.Combine(_configDir, "registration.json");
        if (!File.Exists(path)) return false;

        try
        {
            var data = JsonDocument.Parse(File.ReadAllText(path));
            _haUrl = data.RootElement.GetProperty("ha_url").GetString() ?? "";
            _webhookId = data.RootElement.GetProperty("webhook_id").GetString() ?? "";
            _cloudUrl = data.RootElement.TryGetProperty("cloud_url", out var cu) ? cu.GetString() ?? "" : "";
            var tokenPath = Path.Combine(_configDir, "token.txt");
            if (File.Exists(tokenPath)) _token = File.ReadAllText(tokenPath).Trim();
            return !string.IsNullOrEmpty(_webhookId);
        }
        catch { return false; }
    }

    public async Task RegisterSensorAsync(SensorData sensor)
    {
        var sensorDict = new Dictionary<string, object>
        {
            ["type"] = "sensor",
            ["unique_id"] = sensor.UniqueId,
            ["name"] = sensor.Name,
            ["state"] = sensor.State,
        };
        if (!string.IsNullOrEmpty(sensor.Icon)) sensorDict["icon"] = sensor.Icon;
        if (!string.IsNullOrEmpty(sensor.UnitOfMeasurement)) sensorDict["unit_of_measurement"] = sensor.UnitOfMeasurement;
        if (!string.IsNullOrEmpty(sensor.DeviceClass)) sensorDict["device_class"] = sensor.DeviceClass;
        if (!string.IsNullOrEmpty(sensor.StateClass)) sensorDict["state_class"] = sensor.StateClass;
        if (!string.IsNullOrEmpty(sensor.EntityCategory)) sensorDict["entity_category"] = sensor.EntityCategory;

        var payload = new { type = "register_sensor", data = sensorDict };
        var json = JsonSerializer.Serialize(payload);
        await _http.PostAsync(WebhookUrl, new StringContent(json, Encoding.UTF8, "application/json"));
    }

    public async Task UpdateSensorStatesAsync(List<SensorData> sensors)
    {
        var clean = new List<Dictionary<string, object>>();
        foreach (var s in sensors)
        {
            var entry = new Dictionary<string, object>
            {
                ["type"] = "sensor",
                ["unique_id"] = s.UniqueId,
                ["state"] = s.State,
            };
            if (!string.IsNullOrEmpty(s.Icon)) entry["icon"] = s.Icon;
            clean.Add(entry);
        }

        var payload = new { type = "update_sensor_states", data = clean };
        var json = JsonSerializer.Serialize(payload);
        await _http.PostAsync(WebhookUrl, new StringContent(json, Encoding.UTF8, "application/json"));
    }

    public async Task SendLocationAsync()
    {
        var payload = new Dictionary<string, object>
        {
            ["type"] = "update_location",
            ["data"] = new Dictionary<string, object?>
            {
                ["gps"] = new object?[] { null, null },
                ["gps_accuracy"] = 0,
                ["battery"] = null
            }
        };
        var json = JsonSerializer.Serialize(payload);
        await _http.PostAsync(WebhookUrl, new StringContent(json, Encoding.UTF8, "application/json"));
    }

    public async Task<string?> CheckForUpdateAsync(bool includePrerelease = false)
    {
        try
        {
            using var ghClient = new HttpClient();
            ghClient.DefaultRequestHeaders.Add("User-Agent", "HA-DeskLink");

            var resp = await ghClient.GetAsync("https://api.github.com/repos/TechFlipsi/ha-desklink-dotnet/releases");
            if (!resp.IsSuccessStatusCode) return null;
            var data = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var currentVersion = GetVersion();

            // Find the highest version that is newer than current
            string? bestTag = null;
            Version? bestVersion = null;
            string? bestUrl = null;
            var currentVer = ParseVersion(currentVersion);

            foreach (var release in data.RootElement.EnumerateArray())
            {
                // Skip pre-releases for stable channel
                if (!includePrerelease && release.TryGetProperty("prerelease", out var pre) && pre.GetBoolean())
                    continue;

                var tagName = release.GetProperty("tag_name").GetString() ?? "";
                if (tagName.StartsWith("v")) tagName = tagName[1..];
                var releaseVer = ParseVersion(tagName);
                if (releaseVer == null) continue;

                // Only consider versions NEWER than current (no downgrades!)
                if (releaseVer.CompareTo(currentVer) <= 0) continue;

                if (bestVersion == null || releaseVer.CompareTo(bestVersion) > 0)
                {
                    bestVersion = releaseVer;
                    bestTag = tagName;
                    foreach (var asset in release.GetProperty("assets").EnumerateArray())
                    {
                        var name = asset.GetProperty("name").GetString() ?? "";
                        if (name.EndsWith(".exe"))
                            bestUrl = asset.GetProperty("browser_download_url").GetString();
                    }
                }
            }

            return bestUrl;
        }
        catch { }
        return null;
    }

    private static Version ParseVersion(string version)
    {
        // Handle versions like "2.0.7" → Version(2, 0, 7)
        var parts = version.Split('.');
        try
        {
            var major = parts.Length > 0 ? int.Parse(parts[0]) : 0;
            var minor = parts.Length > 1 ? int.Parse(parts[1]) : 0;
            var build = parts.Length > 2 ? int.Parse(parts[2]) : 0;
            return new Version(major, minor, build);
        }
        catch { return new Version(0, 0, 0); }
    }

    private void SaveRegistration(string haUrl, string token)
    {
        Directory.CreateDirectory(_configDir);
        var data = new { ha_url = haUrl, webhook_id = _webhookId, cloud_url = _cloudUrl, device_id = _deviceId };
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(_configDir, "registration.json"), json);
        File.WriteAllText(Path.Combine(_configDir, "token.txt"), token);
    }

    private string? LoadDeviceId()
    {
        try
        {
            // Check for reset device_id first
            var resetPath = Path.Combine(_configDir, "device_id.txt");
            if (File.Exists(resetPath))
            {
                var id = File.ReadAllText(resetPath).Trim();
                File.Delete(resetPath); // One-time use
                return id;
            }
            // Then check existing registration
            var path = Path.Combine(_configDir, "registration.json");
            if (File.Exists(path))
            {
                var data = JsonDocument.Parse(File.ReadAllText(path));
                if (data.RootElement.TryGetProperty("device_id", out var di))
                    return di.GetString();
            }
        }
        catch { }
        return null;
    }

    public void ResetDeviceId()
    {
        try
        {
            Directory.CreateDirectory(_configDir);
            File.WriteAllText(Path.Combine(_configDir, "device_id.txt"), Guid.NewGuid().ToString());
        }
        catch { }
    }

    private static string GetVersion()
    {
        try
        {
            var vfile = Path.Combine(AppContext.BaseDirectory, "VERSION");
            if (File.Exists(vfile)) return File.ReadAllText(vfile).Trim();
        }
        catch { }
        return "2.1.1";
    }
}