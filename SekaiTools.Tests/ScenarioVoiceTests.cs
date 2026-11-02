using System.Net;
using SekaiDataFetch;
using Xunit;

namespace SekaiTools.Tests;

public class ScenarioVoiceTests
{
    [Theory]
    [InlineData("https://storage.sekai.best/sekai-jp-assets/", "story_rip")]
    [InlineData("https://assets.example/jp-assets/", "story")]
    public void UsesSourceSpecificScenarioFolder(string root, string folder)
    {
        Assert.EndsWith("sound/scenario/voice/" + folder + "/voice_01.mp3", ScenarioVoice.Locate(root, "story", "voice_01").AbsoluteUri);
    }

    [Fact]
    public async Task CacheIsBinaryAndSeparatedBySourceAndScenario()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var handler = new VoiceHandler();
        using var fetcher = new Fetcher(_ => handler);
        try
        {
            var first = ScenarioVoice.Locate("https://assets.example/", "story", "voice");
            var path = await ScenarioVoice.LoadAsync(fetcher, first, directory, default);
            Assert.Equal(new byte[] { 0, 255, 128, 42 }, await File.ReadAllBytesAsync(path));
            Assert.Equal(path, await ScenarioVoice.LoadAsync(fetcher, first, directory, default));
            Assert.Equal(1, handler.Requests);
            Assert.NotEqual(path, await ScenarioVoice.LoadAsync(fetcher, ScenarioVoice.Locate("https://assets.example/", "other", "voice"), directory, default));
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ScenarioVoice.LoadAsync(fetcher,
                ScenarioVoice.Locate("https://assets.example/", "cancel", "voice"), directory, cancelled.Token));
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    private sealed class VoiceHandler : HttpMessageHandler
    {
        public int Requests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([0, 255, 128, 42]) });
        }
    }
}
