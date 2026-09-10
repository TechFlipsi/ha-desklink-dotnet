
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
using System.Reflection;
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

    public HaApiClient(string configDir, bool verifySsl = true)
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
            ["manufacturer"] = "HA DeskLink",
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
                ["manufacturer"] = "HA DeskLink",
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
            // Token is now loaded from encrypted Config, not from token.txt
            // Legacy migration: if token.txt still exists, remove it
            if (File.Exists(tokenPath))
            {
                try { File.Delete(tokenPath); } catch { }
            }
            return !string.IsNullOrEmpty(_webhookId);
        }
        catch { return false; }
    }

    public async Task RegisterSensorAsync(SensorData sensor)
    {
        var sensorDict = new Dictionary<string, object>
        {
            ["type"] = sensor.Type,
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
                ["type"] = s.Type,
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

    public async Task ToggleEntityAsync(string entityId)
    {
        if (string.IsNullOrEmpty(_haUrl) || string.IsNullOrEmpty(_token))
            throw new InvalidOperationException("Not connected to HA");

        var url = $"{_haUrl}/api/services/homeassistant/toggle";
        var payload = JsonSerializer.Serialize(new { entity_id = entityId });
        var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        req.Headers.Add("Authorization", $"Bearer {_token}");
        var resp = await _http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Get all entity states from Home Assistant.
    /// Returns a list of entity_id + friendly_name for Quick Actions.
    /// </summary>
    public async Task<List<(string entityId, string friendlyName)>> GetEntitiesAsync()
    {
        if (string.IsNullOrEmpty(_haUrl) || string.IsNullOrEmpty(_token))
            throw new InvalidOperationException("Not connected to HA");

        var url = $"{_haUrl}/api/states";
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("Authorization", $"Bearer {_token}");
        var resp = await _http.SendAsync(req);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync();
        var entities = new List<(string, string)>();
        using var doc = JsonDocument.Parse(json);
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var entityId = item.TryGetProperty("entity_id", out var eid) ? eid.GetString() ?? "" : "";
            var friendlyName = "";
            if (item.TryGetProperty("attributes", out var attrs))
            {
                if (attrs.TryGetProperty("friendly_name", out var fn))
                    friendlyName = fn.GetString() ?? "";
            }
            if (!string.IsNullOrEmpty(entityId))
                entities.Add((entityId, friendlyName));
        }
        return entities;
    }

    /// <summary>
    /// Einzelnen Entity-State abrufen (für Desktop-Widgets).
    /// Gibt (state, unit_of_measurement) zurück; null wenn Entity nicht existiert.
    /// </summary>
    public async Task<(string State, string? Unit)?> GetEntityStateAsync(string entityId)
    {
        if (string.IsNullOrEmpty(_haUrl) || string.IsNullOrEmpty(_token))
            throw new InvalidOperationException("Not connected to HA");

        var url = $"{_haUrl}/api/states/{entityId}";
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("Authorization", $"Bearer {_token}");
        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode) return null;

        var json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var state = root.TryGetProperty("state", out var st) ? st.GetString() ?? "unknown" : "unknown";
        string? unit = null;
        if (root.TryGetProperty("attributes", out var attrs) &&
            attrs.TryGetProperty("unit_of_measurement", out var u) &&
            u.ValueKind == JsonValueKind.String)
            unit = u.GetString();
        return (state, unit);
    }

    /// <summary>
    /// Upload a screenshot as a HA event with base64 image data.
    /// Event type: ha_desklink_screenshot
    /// </summary>
    public async Task UploadScreenshotAsync(string filePath)
    {
        if (string.IsNullOrEmpty(_haUrl) || string.IsNullOrEmpty(_token)) return;
        if (!File.Exists(filePath)) return;

        var imageBytes = await File.ReadAllBytesAsync(filePath);
        var base64 = Convert.ToBase64String(imageBytes);

        var payload = JsonSerializer.Serialize(new
        {
            type = "fire_event",
            data = new
            {
                event_type = "ha_desklink_screenshot",
                event_data = new
                {
                    device = Environment.MachineName,
                    image = $"data:image/png;base64,{base64}"
                }
            }
        });

        await _http.PostAsync(WebhookUrl, new StringContent(payload, Encoding.UTF8, "application/json"));
    }

    // Update loop protection: cache last offered version + timestamp
    private static string? _lastOfferedVersion = null;
    private static DateTime _lastCheckTime = DateTime.MinValue;
    private static readonly TimeSpan _checkCooldown = TimeSpan.FromHours(1);

    public async Task<string?> CheckForUpdateAsync(bool includePrerelease = false)
    {
        try
        {
            // Update loop protection: cooldown between checks
            if (DateTime.Now - _lastCheckTime < _checkCooldown && _lastOfferedVersion != null)
                return null;

            // Update loop protection: check if an update is already pending
            try
            {
                var pendingFile = Path.Combine(_configDir, ".update_pending");
                if (File.Exists(pendingFile))
                {
                    var pendingVer = File.ReadAllText(pendingFile).Trim();
                    if (!string.IsNullOrEmpty(pendingVer))
                    {
                        var pendingVersion = ParseVersion(pendingVer);
                        var installedVer = ParseVersion(GetVersion());
                        // If pending version is same or newer, don't offer update again
                        if (pendingVersion.CompareTo(installedVer) >= 0)
                            return null;
                    }
                }
            }
            catch { }

            using var ghClient = new HttpClient();
            ghClient.DefaultRequestHeaders.Add("User-Agent", "HA-DeskLink");

            var resp = await ghClient.GetAsync("https://api.github.com/repos/TechFlipsi/ha-desklink-windows/releases");
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

            // Update loop protection: skip if we already offered this version
            if (bestTag != null && bestTag == _lastOfferedVersion)
                return null;

            if (bestTag != null)
            {
                _lastOfferedVersion = bestTag;
                _lastCheckTime = DateTime.Now;
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
        // Token is now stored via encrypted Config only, never in plaintext
        // Delete legacy token.txt if it exists
        var tokenPath = Path.Combine(_configDir, "token.txt");
        if (File.Exists(tokenPath))
        {
            try { File.Delete(tokenPath); } catch { }
        }
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
        var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
        var fallbackVersion = assemblyVersion != null
            ? $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}"
            : "4.4.2";

        try
        {
            var vfile = Path.Combine(AppContext.BaseDirectory, "VERSION");
            if (File.Exists(vfile))
            {
                var fileVer = File.ReadAllText(vfile).Trim();
                if (!string.IsNullOrEmpty(fileVer) && fileVer.Length >= 5)
                    return fileVer;
            }
        }
        catch { }

        return fallbackVersion;
    }
}