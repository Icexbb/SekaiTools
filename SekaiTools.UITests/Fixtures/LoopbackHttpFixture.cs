using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SekaiTools.UITests.Fixtures;

/// <summary>Bounded loopback HTTP fixture; unmapped requests receive HTTP 503.</summary>
public sealed class LoopbackHttpFixture : IDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _worker;
    public ConcurrentQueue<string> Requests { get; } = new();
    public int Port { get; }
    public Func<string, string?>? Response { get; set; }

    public LoopbackHttpFixture()
    {
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _worker = RunAsync();
    }

    private async Task RunAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                using var client = await _listener.AcceptTcpClientAsync(_stop.Token);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(5));
                await using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
                var line = await reader.ReadLineAsync(timeout.Token);
                Requests.Enqueue(line ?? "empty request");
                var body = Response?.Invoke(line ?? "");
                var content = Encoding.UTF8.GetBytes(body ?? "");
                var header = Encoding.ASCII.GetBytes($"HTTP/1.1 {(body is null ? "503 Service Unavailable" : "200 OK")}\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: {content.Length}\r\nConnection: close\r\n\r\n");
                var bytes = header.Concat(content).ToArray();
                await stream.WriteAsync(bytes, timeout.Token);
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (SocketException) when (_stop.IsCancellationRequested) { }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _listener.Stop();
        _worker.GetAwaiter().GetResult();
        _stop.Dispose();
    }
}
