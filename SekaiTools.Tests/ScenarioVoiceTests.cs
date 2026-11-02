using System.Net;
using SekaiDataFetch;
using Xunit;

namespace SekaiTools.Tests;

public class ScenarioVoiceTests
{
    [Theory]
    [InlineData("https://storage.sekai.best/sekai-jp-assets/", "story")]
    [InlineData("https://assets.example/jp-assets/", "story")]
    public void UsesScenarioFolderWithoutRipSuffix(string root, string folder)
    {
        Assert.EndsWith("sound/scenario/voice/" + folder + "/voice_01.mp3", ScenarioVoice.Locate(root, "story", "voice_01").AbsoluteUri);
    }

    [Fact]
    public void PreservesOriginalVoiceIdInReportedScenario()
    {
        var uri = ScenarioVoice.Locate("https://storage.sekai.best/sekai-jp-assets/", "event_199_05", "voice_ev_night_25_05_06_140");
        Assert.Equal("/sekai-jp-assets/sound/scenario/voice/event_199_05/voice_ev_night_25_05_06_140.mp3", uri.AbsolutePath);
    }

    [Theory]
    [InlineData("partvoice_30_021_piapro", "21miku_piapro")]
    [InlineData("partvoice_18_023", "23len_piapro")]
    [InlineData("partvoice_28_021_band", "21miku_light_sound")]
    [InlineData("partvoice_28_021_idol", "21miku_idol")]
    [InlineData("partvoice_28_021_street", "21miku_street")]
    [InlineData("partvoice_28_021_wonder", "21miku_theme_park")]
    [InlineData("partvoice_28_021_night", "21miku_school_refusal")]
    [InlineData("partvoice_28_021_light_sound", "21miku_light_sound")]
    [InlineData("partvoice_18_022", "22rin_piapro")]
    [InlineData("partvoice_18_024", "24luka_piapro")]
    [InlineData("partvoice_18_025", "25meiko_piapro")]
    [InlineData("partvoice_18_026", "26kaito_piapro")]
    public void LocatesPartVoiceByCharacterAndGroup(string voiceId, string folder)
    {
        var uri = ScenarioVoice.Locate("https://storage.sekai.best/sekai-jp-assets/", "", voiceId);
        Assert.Equal("/sekai-jp-assets/sound/scenario/part_voice/" + folder + "/" + voiceId + ".mp3", uri.AbsolutePath);
    }

    [Theory]
    [InlineData("partvoice_18_999")]
    [InlineData("partvoice_18_021_unknown")]
    [InlineData("partvoice_")]
    public void RejectsUnknownPartVoiceFolder(string voiceId)
    {
        Assert.Throws<ArgumentException>(() => ScenarioVoice.Locate("https://assets.example/", "story", voiceId));
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
