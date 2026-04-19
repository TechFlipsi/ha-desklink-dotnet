#nullable enable
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows.Forms;

namespace HaDeskLink;

/// <summary>
/// HTTP listener for receiving commands and notifications from Home Assistant.
/// Commands: http://PC-IP:59123/command?token=xxx&amp;action=shutdown
/// Notifications come via the mobile_app webhook protocol.
/// </summary>
public class WebhookServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly string _token;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;
    private NotifyIcon? _trayIcon;

    public int Port { get; } = 59123;

    public WebhookServer(string token, int port = 59123)
    {
        _token = token;
        Port = port;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://+:{port}/command/");
        _listener.Prefixes.Add($"http://+:{port}/webhook/");
    }

    public void SetTrayIcon(NotifyIcon? trayIcon) => _trayIcon = trayIcon;

    public void Start()
    {
        _listener.Start();
        ThreadPool.QueueUserWorkItem(_ => Listen());
    }

    private void Listen()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                var context = _listener.GetContext();
                ProcessRequest(context);
            }
            catch (HttpListenerException) when (_cts.Token.IsCancellationRequested) { break; }
            catch { }
        }
    }

    private void ProcessRequest(HttpListenerContext context)
    {
        var path = context.Request.Url?.AbsolutePath ?? "";

        if (path.Contains("/webhook"))
        {
            // HA mobile_app notification webhook
            try
            {
                using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
                var body = reader.ReadToEnd();

                if (NotificationHandler.TryHandleNotification(body, _trayIcon))
                {
                    RespondJson(context, new { success = true });
                }
                else
                {
                    RespondJson(context, new { success = true, note = "unknown webhook type" });
                }
            }
            catch (Exception ex)
            {
                RespondJson(context, new { success = false, error = ex.Message }, 400);
            }
            return;
        }

        // Command endpoint: /command?token=xxx&action=shutdown
        var query = context.Request.QueryString;
        var action = query["action"] ?? "";
        var token = query["token"] ?? "";

        if (token != _token)
        {
            context.Response.StatusCode = 401;
            context.Response.Close();
            return;
        }

        try
        {
            CommandHandler.Execute(action);
            RespondJson(context, new { success = true, action });
        }
        catch (Exception ex)
        {
            RespondJson(context, new { success = false, error = ex.Message }, 400);
        }
    }

    private static void RespondJson(HttpListenerContext context, object data, int statusCode = 200)
    {
        var json = JsonSerializer.Serialize(data);
        var buffer = Encoding.UTF8.GetBytes(json);
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;
        context.Response.OutputStream.Write(buffer, 0, buffer.Length);
        context.Response.Close();
    }

    public void Stop() => _cts.Cancel();

    public void Dispose()
    {
        if (!_disposed)
        {
            _cts.Cancel();
            _listener.Stop();
            _cts.Dispose();
            _disposed = true;
        }
    }
}