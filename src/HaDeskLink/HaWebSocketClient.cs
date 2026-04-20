#nullable enable
using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HaDeskLink;

/// <summary>
/// Connects to Home Assistant via WebSocket to receive push notifications.
/// Uses the mobile_app push_notification_channel protocol.
/// Works for all users regardless of network setup - no inbound port needed.
/// </summary>
public class HaWebSocketClient : IDisposable
{
    private readonly string _haUrl;
    private readonly string _token;
    private readonly string _webhookId;
    private readonly NotifyIcon? _trayIcon;
    private readonly Action<string>? _onCommand;
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private bool _disposed;
    private int _msgId = 1;
    private bool _connected;

    public bool IsConnected => _connected;

    public HaWebSocketClient(string haUrl, string token, string webhookId, NotifyIcon? trayIcon, Action<string>? onCommand = null)
    {
        _haUrl = haUrl.TrimEnd('/');
        _token = token;
        _webhookId = webhookId;
        _trayIcon = trayIcon;
        _onCommand = onCommand;
    }

    public async Task ConnectAsync()
    {
        _cts = new CancellationTokenSource();
        var wsUrl = _haUrl.Replace("https://", "wss://").Replace("http://", "ws://") + "/api/websocket";

        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                _ws = new ClientWebSocket();

                // Ignore SSL errors for self-signed certs
                _ws.Options.RemoteCertificateValidationCallback = (sender, cert, chain, errors) => true;

                await _ws.ConnectAsync(new Uri(wsUrl), _cts.Token);

                // Step 1: Receive auth_required
                var msg = await ReceiveMessage();
                if (msg == null || !msg.Contains("auth_required"))
                    throw new Exception("Expected auth_required from HA");

                // Step 2: Send auth
                await SendMessage(new { type = "auth", access_token = _token });

                // Step 3: Receive auth_ok
                msg = await ReceiveMessage();
                if (msg == null || !msg.Contains("auth_ok"))
                    throw new Exception("Auth failed: " + (msg ?? "no response"));

                _connected = true;

                // Step 4: Subscribe to push notification channel
                await SendMessage(new
                {
                    id = _msgId++,
                    type = "mobile_app/push_notification_channel",
                    webhook_id = _webhookId,
                    support_confirm = false
                });

                // Notify user that WebSocket is connected
                _trayIcon?.ShowBalloonTip(3000, "HA DeskLink", "Verbunden mit Home Assistant (WebSocket)", ToolTipIcon.Info);

                // Step 5: Listen for notifications
                await ListenLoop();
            }
            catch (OperationCanceledException) { break; }
            catch (Exception)
            {
                _connected = false;
            }

            if (!_cts.Token.IsCancellationRequested)
            {
                try { await Task.Delay(5000, _cts.Token); } catch { break; }
            }
        }

        _connected = false;
    }

    private async Task<string?> ReceiveMessage()
    {
        if (_ws?.State != WebSocketState.Open) return null;

        var buffer = new byte[16384];
        var sb = new StringBuilder();
        WebSocketReceiveResult result;

        do
        {
            result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts?.Token ?? CancellationToken.None);
            sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
        }
        while (!result.EndOfMessage);

        return sb.ToString();
    }

    private async Task SendMessage(object data)
    {
        if (_ws?.State != WebSocketState.Open) return;
        var json = JsonSerializer.Serialize(data);
        var bytes = Encoding.UTF8.GetBytes(json);
        await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts?.Token ?? CancellationToken.None);
    }

    private async Task ListenLoop()
    {
        while (_ws?.State == WebSocketState.Open && !_cts!.Token.IsCancellationRequested)
        {
            try
            {
                var msg = await ReceiveMessage();
                if (msg == null) break;

                ProcessMessage(msg);
            }
            catch (OperationCanceledException) { break; }
            catch (WebSocketException) { break; }
            catch { break; }
        }
    }

    private void ProcessMessage(string msg)
    {
        try
        {
            var doc = JsonDocument.Parse(msg);
            var root = doc.RootElement;

            // HA sends push notifications via WebSocket events
            if (root.TryGetProperty("type", out var typeEl) && typeEl.GetString() == "event")
            {
                if (root.TryGetProperty("event", out var eventEl))
                {
                    // Push notification event
                    string title = "HA DeskLink";
                    string message = "";
                    string? command = null;

                    if (eventEl.TryGetProperty("title", out var t))
                        title = t.GetString() ?? title;
                    if (eventEl.TryGetProperty("message", out var m))
                        message = m.GetString() ?? "";
                    if (eventEl.TryGetProperty("data", out var data))
                    {
                        if (data.TryGetProperty("command", out var c))
                            command = c.GetString();
                        if (data.TryGetProperty("title", out var dt))
                            title = dt.GetString() ?? title;
                        if (data.TryGetProperty("message", out var dm))
                            message = dm.GetString() ?? message;
                    }

                    // Execute command if present
                    if (!string.IsNullOrEmpty(command))
                    {
                        try { _onCommand?.Invoke(command!); }
                        catch { }
                    }

                    // Show notification if there's a message
                    if (!string.IsNullOrEmpty(message))
                    {
                        NotificationHandler.ShowNotification(title, message, _trayIcon);
                    }
                    else if (!string.IsNullOrEmpty(command))
                    {
                        // Command without message - still show what happened
                        NotificationHandler.ShowNotification("HA DeskLink", $"Befehl ausgeführt: {command}", _trayIcon);
                    }
                }
            }
        }
        catch { }
    }

    public void Stop()
    {
        _cts?.Cancel();
        try
        {
            if (_ws?.State == WebSocketState.Open)
                _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Stopping", CancellationToken.None).Wait(2000);
        }
        catch { }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();
            _ws?.Dispose();
            _cts?.Dispose();
            _disposed = true;
        }
    }
}