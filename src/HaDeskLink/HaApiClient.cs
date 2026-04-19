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
        _http.DefaultRequestHeaders.Clear();
        _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
    }

    public async Task RegisterAsync(string haUrl, string token)
    {
        _haUrl = haUrl.TrimEnd('/');
        SetToken(token);

        var is64 = Environment.Is64BitOperatingSystem;
        var payload = new Dictionary<string, object>
        {
            ["app_id"] = "ha_desklink",
            ["app_name"] = "HA DeskLink",
            ["app_version"] = GetVersion(),
            ["device_name"] = Environment.MachineName,
            ["device_id"] = Guid.NewGuid().ToString(),
            ["os_name"] = "Windows",
            ["os_version"] = Environment.OSVersion.VersionString,
            ["manufacturer"] = "Custom",
            ["model"] = $"PC ({(is64 ? "x64" : "x86")})",
            ["supports_encryption"] = false,
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
            return !string.IsNullOrEmpty(_webhookId);
        }
        catch { return false; }
    }

    /// <summary>Register a sensor entity.</summary>
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

    /// <summary>Register a button entity (for commands like shutdown, restart).</summary>
    public async Task RegisterButtonAsync(string uniqueId, string name, string icon = "")
    {
        var payload = new Dictionary<string, object>
        {
            ["type"] = "register_sensor",
            ["data"] = new Dictionary<string, object>
            {
                ["type"] = "button",
                ["unique_id"] = uniqueId,
                ["name"] = name,
                ["state"] = "unavailable",
            }
        };
        if (!string.IsNullOrEmpty(icon)) ((Dictionary<string, object>)payload["data"])["icon"] = icon;
        var json = JsonSerializer.Serialize(payload);
        await _http.PostAsync(WebhookUrl, new StringContent(json, Encoding.UTF8, "application/json"));
    }

    /// <summary>Register all command buttons.</summary>
    public async Task RegisterCommandButtonsAsync()
    {
        var buttons = new[]
        {
            ("shutdown", "Herunterfahren", "mdi:power"),
            ("restart", "Neustart", "mdi:restart"),
            ("hibernate", "Ruhezustand", "mdi:sleep"),
            ("lock", "Sperren", "mdi:lock"),
        };

        foreach (var (id, name, icon) in buttons)
        {
            try { await RegisterButtonAsync(id, name, icon); }
            catch { }
        }
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

    /// <summary>Check GitHub for newer version. Returns download URL if update available, null otherwise.</summary>
    public async Task<string?> CheckForUpdateAsync()
    {
        try
        {
            var resp = await _http.GetAsync("https://api.github.com/repos/FKirchweger/ha-desklink-dotnet/releases/latest");
            if (!resp.IsSuccessStatusCode) return null;

            var data = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var tagName = data.RootElement.GetProperty("tag_name").GetString() ?? "";
            if (tagName.StartsWith("v")) tagName = tagName[1..];

            var currentVersion = GetVersion();
            if (tagName != currentVersion && !string.IsNullOrEmpty(tagName))
            {
                // Find the exe asset
                foreach (var asset in data.RootElement.GetProperty("assets").EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString() ?? "";
                    if (name.EndsWith(".exe"))
                    {
                        return asset.GetProperty("browser_download_url").GetString();
                    }
                }
            }
        }
        catch { }
        return null;
    }

    private void SaveRegistration(string haUrl, string token)
    {
        Directory.CreateDirectory(_configDir);
        var data = new { ha_url = haUrl, webhook_id = _webhookId, cloud_url = _cloudUrl, device_id = _deviceId };
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(_configDir, "registration.json"), json);
        File.WriteAllText(Path.Combine(_configDir, "token.txt"), token);
    }

    private static string GetVersion()
    {
        try
        {
            var vfile = Path.Combine(AppContext.BaseDirectory, "VERSION");
            if (File.Exists(vfile)) return File.ReadAllText(vfile).Trim();
        }
        catch { }
        return "2.0.0";
    }
}