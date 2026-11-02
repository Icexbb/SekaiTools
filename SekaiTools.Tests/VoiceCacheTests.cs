using System.Net;
using SekaiDataFetch;
using Xunit;

namespace SekaiTools.Tests;

public class VoiceCacheTests
{
    private const long MiB = 1024 * 1024;
    private const long GiB = 1024 * MiB;

    [Fact]
    public void LimitAdaptsToCapacityAndFreeSpace()
    {
        Assert.Equal(128 * MiB, VoiceCache.CalculateLimit(64 * GiB, 32 * GiB));
        Assert.Equal(512 * GiB / 1000, VoiceCache.CalculateLimit(512 * GiB, 256 * GiB));
        Assert.Equal(2 * GiB, VoiceCache.CalculateLimit(4096 * GiB, 2048 * GiB));
        Assert.Equal(GiB / 20, VoiceCache.CalculateLimit(512 * GiB, GiB));
        Assert.Equal(0, VoiceCache.CalculateLimit(512 * GiB, 0));
    }

    [Fact]
    public void EvictsLeastRecentlyUsedButRetainsPartVoiceAndProtectedFile()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var oldest = Create(root, "voice", 'A', -5);
            var active = Create(root, "voice", 'B', -4);
            var recent = Create(root, "voice", 'C', -1);
            var permanent = Create(root, "partvoice", 'D', -10);
            var unrelated = Path.Combine(root, "voice", "unrelated.mp3");
            File.WriteAllText(unrelated, "keep");
            using (VoiceCache.Protect(active))
            {
                VoiceCache.Trim(root, 8);
                Assert.False(File.Exists(oldest));
                Assert.True(File.Exists(active));
                Assert.True(File.Exists(recent));
                VoiceCache.Trim(root, 0, recent);
                Assert.True(File.Exists(active));
                Assert.True(File.Exists(recent));
                Assert.True(File.Exists(permanent));
            }
            VoiceCache.Trim(root, 0);
            Assert.False(File.Exists(active));
            Assert.False(File.Exists(recent));
            Assert.True(File.Exists(permanent));
            Assert.True(File.Exists(unrelated));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task SeparatesCategoriesReusesLegacyAndRefreshesLruOnCacheHit()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var handler = new AudioHandler();
        using var fetcher = new Fetcher(_ => handler);
        try
        {
            var voice = ScenarioVoice.Locate("https://assets.example/", "story", "voice_01");
            var part = ScenarioVoice.Locate("https://assets.example/", "story", "partvoice_18_023");
            var path = await ScenarioVoice.LoadAsync(fetcher, voice, root, default);
            var permanent = await ScenarioVoice.LoadAsync(fetcher, part, root, default);
            Assert.Equal("voice", new DirectoryInfo(Path.GetDirectoryName(path)!).Name);
            Assert.Equal("partvoice", new DirectoryInfo(Path.GetDirectoryName(permanent)!).Name);
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-2));
            Assert.Equal(path, await ScenarioVoice.LoadAsync(fetcher, voice, root, default));
            Assert.True(File.GetLastWriteTimeUtc(path) > DateTime.UtcNow.AddMinutes(-1));
            File.Move(permanent, Path.Combine(root, Path.GetFileName(permanent)));
            Assert.Equal(permanent, await ScenarioVoice.LoadAsync(fetcher, part, root, default));
            Assert.Equal(2, handler.Requests);
            VoiceCache.Trim(root, 0);
            Assert.False(File.Exists(path));
            Assert.True(File.Exists(permanent));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static string Create(string root, string category, char key, int ageMinutes)
    {
        var directory = Path.Combine(root, category);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, new string(key, 64) + ".mp3");
        File.WriteAllBytes(path, [1, 2, 3, 4]);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(ageMinutes));
        return path;
    }

    private sealed class AudioHandler : HttpMessageHandler
    {
        public int Requests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3, 4]) });
        }
    }
}
