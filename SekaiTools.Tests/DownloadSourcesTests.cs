using System.Net;
using SekaiDataFetch;
using SekaiDataFetch.Source;

namespace SekaiTools.Tests;

public class DownloadSourcesTests
{
    [Fact]
    public async Task RanksReachableSourcesByResponseTime()
    {
        var slow = Source("slow");
        var fast = Source("fast");
        var unavailable = Source("unavailable");
        using var fetcher = new Fetcher(_ => new ProbeHandler());
        Assert.Equal(new[] { fast, slow, unavailable }, await DownloadSources.RankAsync(fetcher, [slow, unavailable, fast]));
    }

    [Fact]
    public async Task RanksHigherWeightAheadOfFasterSources()
    {
        var slow = Source("slow", usageWeight: 2);
        var fast = Source("fast");
        using var fetcher = new Fetcher(_ => new ProbeHandler());
        Assert.Equal(new[] { slow, fast }, await DownloadSources.RankAsync(fetcher, [fast, slow]));
    }

    [Fact]
    public async Task CancellingTheFirstAttemptDoesNotContactAnotherSource()
    {
        using var cancellation = new CancellationTokenSource();
        var attempts = new List<int>();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            DownloadSources.TryAsync<int, string>([1, 2], (source, _) =>
            {
                attempts.Add(source);
                cancellation.Cancel();
                throw new HttpRequestException("Fixture disconnect");
            }, cancellation.Token));
        Assert.Equal([1], attempts);
    }

    [Theory]
    [InlineData("home/banner/{assetbundleName}/{assetbundleName}.png", "home/banner/event_bundle/event_bundle.png")]
    [InlineData("/ondemand/event_story/{abName}/screen_image/banner_event_story.png", "ondemand/event_story/story_bundle/screen_image/banner_event_story.png")]
    [InlineData(null, null)]
    public void ResolvesEventBannerUsingTheBundleRequiredByTheSource(string? template, string? expectedPath)
    {
        var source = new SourceList(Source("banner", eventBannerUrl: template));
        var banner = source.EventBanner("event_bundle", "story_bundle");
        Assert.Equal(expectedPath == null ? null : "https://banner.example.test/storage/" + expectedPath,
            banner?.AbsoluteUri);
    }

    private static SourceData Source(string name, int usageWeight = 1, string? eventBannerUrl = null) => new()
    {
        SourceName = name,
        UsageWeight = usageWeight,
        SourceTemplate = $"https://{name}.example.test/{{type}}.json",
        StorageBaseUrl = $"https://{name}.example.test/storage/",
        ActionSetTemplate = "",
        MemberStoryTemplate = "",
        EventStoryTemplate = "",
        EventBannerUrl = eventBannerUrl,
        SpecialStoryTemplate = "",
        UnitStoryTemplate = ""
    };

    private sealed class ProbeHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Head, request.Method);
            var host = request.RequestUri!.Host;
            if (host.StartsWith("slow.", StringComparison.Ordinal))
                await Task.Delay(180, cancellationToken);
            return new HttpResponseMessage(host.StartsWith("unavailable.", StringComparison.Ordinal)
                ? HttpStatusCode.ServiceUnavailable
                : HttpStatusCode.OK);
        }
    }
}
