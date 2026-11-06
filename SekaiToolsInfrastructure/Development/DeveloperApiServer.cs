using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SekaiToolsBase;

namespace SekaiToolsInfrastructure.Development;

/// <summary>Opt-in loopback transport. The composition root owns the command allowlist.</summary>
public sealed class DeveloperApiServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly Func<string, string, JsonElement, Task<object>> _dispatch;
    public string Token { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public string Address { get; }

    public DeveloperApiServer(int port, Func<string, string, JsonElement, Task<object>> dispatch)
    {
        if (port is < 1024 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        Address = $"http://127.0.0.1:{port}/";
        _dispatch = dispatch;
        _listener.Prefixes.Add(Address);
    }

    public void Start()
    {
        _listener.Start();
        _ = Task.Run(RunAsync);
    }

    private async Task RunAsync()
    {
        try
        {
            while (_listener.IsListening)
            {
                var context = await _listener.GetContextAsync();
                // Sequential handling bounds concurrent work and preserves mutation order.
                try { await HandleAsync(context); }
                catch (Exception ex) when (ex is HttpListenerException or IOException or ObjectDisposedException)
                {
                    // A disconnected client must not stop the accept loop.
                }
            }
        }
        catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException)
        {
            // Closing the listener interrupts accept or response writes.
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        var response = context.Response;
        try
        {
            var request = context.Request;
            if (request.Headers["Origin"] != null || request.Headers["Authorization"] != $"Bearer {Token}")
            {
                response.StatusCode = 401;
                await WriteAsync(response, new { error = "需要本机客户端及有效 Bearer 令牌" });
                return;
            }

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var body = new MemoryStream();
            var buffer = new byte[4096];
            int count;
            while ((count = await request.InputStream.ReadAsync(buffer, timeout.Token)) > 0)
            {
                if (body.Length + count > 16384) throw new ArgumentException("请求体上限为 16 KiB");
                body.Write(buffer, 0, count);
            }

            using var json = JsonDocument.Parse(body.Length == 0 ? "{}" : Encoding.UTF8.GetString(body.ToArray()));
            var result = await _dispatch(request.HttpMethod, request.Url!.AbsolutePath, json.RootElement.Clone());
            await WriteAsync(response, result);
        }
        catch (Exception ex) when (ex is ArgumentException or JsonException or OperationCanceledException)
        {
            response.StatusCode = 400;
            await WriteAsync(response, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            Logger.Log($"开发者 API 请求失败: {ex.Message}", Microsoft.Extensions.Logging.LogLevel.Error);
            response.StatusCode = ex is KeyNotFoundException ? 404 : 500;
            await WriteAsync(response, new { error = ex is KeyNotFoundException ? ex.Message : "请求执行失败，请查看程序日志" });
        }
        finally
        {
            response.Close();
        }
    }

    private static async Task WriteAsync(HttpListenerResponse response, object value)
    {
        response.ContentType = "application/json; charset=utf-8";
        response.Headers["Cache-Control"] = "no-store";
        await JsonSerializer.SerializeAsync(response.OutputStream, value);
    }

    public void Dispose() => _listener.Close();
}
