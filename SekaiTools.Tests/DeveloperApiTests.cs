using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using SekaiToolsCore.Process.Config;
using SekaiToolsInfrastructure.Development;

namespace SekaiTools.Tests;

public class DeveloperApiTests
{
    [Fact]
    public void ThresholdPatchPreservesOtherValues()
    {
        var result = DeveloperThresholds.Patch(new MatchingThreshold(), JsonSerializer.Deserialize<JsonElement>("{\"BannerNormal\":0.9}"));
        Assert.Equal(0.9, result.BannerNormal);
        Assert.Equal(0.8, result.DialogContentNormal);
    }

    [Theory]
    [InlineData("{\"Unknown\":0.9}")]
    [InlineData("{\"BannerNormal\":0}")]
    [InlineData("{\"BannerNormal\":1.1}")]
    [InlineData("{\"BannerNormal\":\"0.8\"}")]
    [InlineData("[]")]
    public void InvalidThresholdPatchIsRejected(string json)
    {
        Assert.Throws<ArgumentException>(() => DeveloperThresholds.Patch(new MatchingThreshold(), JsonSerializer.Deserialize<JsonElement>(json)));
    }

    [Fact]
    public async Task TransportAuthenticatesAndSurvivesInvalidRequests()
    {
        using var socket = new TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        var port = ((IPEndPoint)socket.LocalEndpoint).Port;
        socket.Stop();
        var calls = 0;
        using var server = new DeveloperApiServer(port, (_, _, _) =>
        {
            calls++;
            return Task.FromResult<object>(new { ok = true });
        });
        server.Start();
        using var client = new HttpClient { BaseAddress = new Uri(server.Address), Timeout = TimeSpan.FromSeconds(10) };
        using var unauthorized = await client.GetAsync("api/v1/status");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", server.Token);
        using var malformed = await client.PostAsync("api/v1/settings", new StringContent("{", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        using var oversized = await client.PostAsync("api/v1/settings", new StringContent(new string(' ', 17000)));
        Assert.Equal(HttpStatusCode.BadRequest, oversized.StatusCode);
        client.DefaultRequestHeaders.Add("Origin", "http://localhost");
        using var browser = await client.GetAsync("api/v1/status");
        Assert.Equal(HttpStatusCode.Unauthorized, browser.StatusCode);
        client.DefaultRequestHeaders.Remove("Origin");
        using var success = await client.GetAsync("api/v1/status");
        Assert.Equal(HttpStatusCode.OK, success.StatusCode);
        Assert.Equal(1, calls);
        Assert.Contains("true", await success.Content.ReadAsStringAsync());
        server.Dispose();
        Assert.Throws<ObjectDisposedException>(() => server.Start());
    }
}
