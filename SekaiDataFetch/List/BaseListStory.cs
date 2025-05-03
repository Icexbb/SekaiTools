using SekaiDataFetch.Source;

namespace SekaiDataFetch.List;

public class BaseListStory
{
    protected Fetcher Fetcher { get; } = new();

    public void SetSource(SourceType sourceType)
    {
        Fetcher.SetSource(sourceType);
    }

    public void SetProxy(Proxy proxy)
    {
        Fetcher.SetProxy(proxy);
    }
}