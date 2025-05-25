using SekaiDataFetch.Source;

namespace SekaiDataFetch.List;

public class BaseListStory
{
    protected readonly Fetcher Fetcher = Fetcher.Instance;

    public void SetSource(SourceData sourceData)
    {
        Fetcher.SetSource(sourceData);
    }

    public void SetProxy(Proxy proxy)
    {
        Fetcher.SetProxy(proxy);
    }
}