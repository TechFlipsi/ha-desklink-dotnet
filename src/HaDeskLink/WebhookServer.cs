#nullable enable
using System;
using System.Net;
using System.Text.Json;
using System.Threading;

namespace HaDeskLink;

/// <summary>
/// HTTP listener for receiving commands from Home Assistant.
/// HA calls: http://PC-IP:59123/command?token=xxx&amp;action=shutdown
/// </summary>
public class WebhookServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly string _token;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    public int Port { get; } = 59123;

    public WebhookServer(string token, int port = 59123)
    {
        _token = token;
        Port = port;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://+:{port}/command/");
    }

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
            var response = JsonSerializer.Serialize(new { success = true, action });
            var buffer = System.Text.Encoding.UTF8.GetBytes(response);
            context.Response.ContentType = "application/json";
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
        }
        catch (Exception ex)
        {
            var response = JsonSerializer.Serialize(new { success = false, error = ex.Message });
            var buffer = System.Text.Encoding.UTF8.GetBytes(response);
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = 400;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
        }

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