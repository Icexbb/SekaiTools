using SekaiToolsCore.Process;
using SekaiToolsCore.Process.Config;
using SekaiToolsCore.Process.Model;
using SekaiStory = SekaiToolsBase.Story.Story;

namespace SekaiToolsCore.Match.TemplateMatcher;

public class TemplateMatcherCreator
{
    public TemplateMatcherCreator(Config config)
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

    public DialogTemplateMatcher DialogMatcher()
    {
        return new DialogTemplateMatcher(VInfo, Story, Manager, Config);
    }

    public ContentTemplateMatcher ContentMatcher()
    {
        return new ContentTemplateMatcher(Manager, Config);
    }

    public BannerTemplateMatcher BannerMatcher()
    {
        return new BannerTemplateMatcher(VInfo, Story, Manager, Config);
    }

    public MarkerTemplateMatcher MarkerMatcher()
    {
        return new MarkerTemplateMatcher(VInfo, Story, Manager, Config);
    }

    public SubtitleMaker SubtitleMaker()
    {
        return new SubtitleMaker(VInfo, Manager, Config);
    }
}