using SekaiToolsCore.Process;
using SekaiToolsCore.Process.Model;
using SekaiStory = SekaiToolsCore.Story.Story;

namespace SekaiToolsCore;

public class MatcherCreator
{
    public MatcherCreator(Config config)
    {
        Config = config;
        VInfo = new VideoInfo(Config.VideoFilePath);
        Story = SekaiStory.FromFile(Config.ScriptFilePath, Config.TranslateFilePath);

        Manager = new TemplateManager(VInfo.Resolution);
    }

    private Config Config { get; }
    private VideoInfo VInfo { get; }
    public SekaiStory Story { get; }
    private TemplateManager Manager { get; }

    public DialogMatcher DialogMatcher()
    {
        return new DialogMatcher(VInfo, Story, Manager, Config);
    }

    public ContentMatcher ContentMatcher()
    {
        return new ContentMatcher(Manager, Config);
    }

    public BannerMatcher BannerMatcher()
    {
        return new BannerMatcher(VInfo, Story, Manager, Config);
    }

    public MarkerMatcher MarkerMatcher()
    {
        return new MarkerMatcher(VInfo, Story, Manager, Config);
    }

    public SubtitleMaker SubtitleMaker()
    {
        return new SubtitleMaker(VInfo, Manager, Config);
    }
}