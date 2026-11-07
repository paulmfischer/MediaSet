using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace MediaSet.Api.Tests.Observability;

/// <summary>Local loopback HTTP listener that records the traceparent header of the last request.</summary>
public sealed class LoopbackHttpTarget : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _cts = new();
    private volatile string? _lastTraceparent;

    public LoopbackHttpTarget()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        Url = $"http://127.0.0.1:{port}/";
        _listener.Prefixes.Add(Url);
        _listener.Start();
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
