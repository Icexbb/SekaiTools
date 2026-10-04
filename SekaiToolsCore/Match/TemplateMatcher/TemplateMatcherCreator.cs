using SekaiToolsCore.Abstractions;
using SekaiToolsCore.Process.Config;
using SekaiToolsCore.Process.Model;
using SekaiStory = SekaiToolsBase.Story.Story;

namespace SekaiToolsCore.Match.TemplateMatcher;

public class TemplateMatcherCreator : IDisposable
{
    private readonly ITemplateResourceProvider _resources;
    public TemplateMatcherCreator(Config config, ITemplateResourceProvider resourceProvider)
    {
        Config = config;
        _resources = resourceProvider;
        VInfo = new VideoInfo(Config.VideoFilePath);
        Story = SekaiStory.FromFile(Config.ScriptFilePath, Config.TranslateFilePath);

        Manager = new TemplateManager(VInfo.Resolution, resourceProvider);
        CachePool = new TemplateMatchCachePool();
    }

    private Config Config { get; }
    private VideoInfo VInfo { get; }
    public VideoInfo VideoInfo => VInfo;
    public SekaiStory Story { get; }
    public FrameRate FrameRate => VInfo.Fps;
    private TemplateManager Manager { get; }
    public TemplateMatchCachePool CachePool { get; }

    internal TemplatePreloader CreatePreloader(CancellationToken token) => new(Manager,
        new TemplateManager(VInfo.Resolution, _resources), Manager.GetFontSize(),
        Story.Dialogs().ToArray(), Story.Banners().ToArray(), Story.Markers().ToArray(), token);

    public void Dispose()
    {
        Manager.Dispose();
        CachePool.ResetAll();
    }

    public DialogTemplateMatcher DialogMatcher()
    {
        return new DialogTemplateMatcher(VInfo, Story, Manager, CachePool, Config);
    }

    public ContentTemplateMatcher ContentMatcher()
    {
        return new ContentTemplateMatcher(Manager, CachePool, Config);
    }

    public BannerTemplateMatcher BannerMatcher()
    {
        return new BannerTemplateMatcher(VInfo, Story, Manager, CachePool, Config);
    }

    public MarkerTemplateMatcher MarkerMatcher()
    {
        return new MarkerTemplateMatcher(VInfo, Story, Manager, CachePool, Config);
    }

    public SubtitleMaker SubtitleMaker()
    {
        return new SubtitleMaker(VInfo, Manager, Config);
    }
}
