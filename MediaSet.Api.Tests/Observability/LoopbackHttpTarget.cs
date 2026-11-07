using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace MediaSet.Api.Tests.Observability;

/// <summary>Local loopback HTTP listener that records the traceparent header of the last request.</summary>
public sealed class LoopbackHttpTarget : IDisposable
{
    private readonly HttpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private volatile string? _lastTraceparent;

    public LoopbackHttpTarget()
    {
        // HttpListener cannot bind port 0, so pick a free port and retry if another process wins it.
        for (var attempt = 0; ; attempt++)
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            var url = $"http://127.0.0.1:{port}/";
            var listener = new HttpListener();
            listener.Prefixes.Add(url);
            try
            {
                listener.Start();
            }
            catch (HttpListenerException) when (attempt < 9)
            {
                listener.Close();
                continue;
            }

            _listener = listener;
            Url = url;
            break;
        }

        _ = Task.Run(ServeAsync);
    }

    public string Url { get; }

    public string? LastTraceparent => _lastTraceparent;

    private async Task ServeAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _lastTraceparent = context.Request.Headers["traceparent"];
                context.Response.StatusCode = 204;
                context.Response.Close();
            }
            catch (Exception) when (_cts.IsCancellationRequested || !_listener.IsListening)
            {
                return;
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Close();
        _cts.Dispose();
    }
}
