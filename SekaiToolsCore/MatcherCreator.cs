using SekaiToolsCore.Process;
using SekaiStory = SekaiToolsCore.Story.Story;

namespace SekaiToolsCore;

public class MatcherCreator
{
    private Config Config { get; }
    private VideoInfo VInfo { get; }
    public SekaiStory Story { get; }
    private TemplateManager Manager { get; }

    public MatcherCreator(Config config)
    {
        Config = config;
        VInfo = new VideoInfo(Config.VideoFilePath);
        Story = SekaiStory.FromFile(Config.ScriptFilePath, Config.TranslateFilePath);

        var names = Story.Dialogs().Select(dialog => dialog.CharacterOriginal).ToList();
        var dbs = new List<string>();
        foreach (var dialog in Story.Dialogs())
        {
            dbs.Add(dialog.BodyOriginal[..1]);
            dbs.Add(dialog.BodyOriginal[..2]);
            if (dialog.BodyOriginal.Length >= 3) dbs.Add(dialog.BodyOriginal[..3]);
        }

        Manager = new TemplateManager(VInfo.Resolution, dbs, names);
    }

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
        return new SubtitleMaker(VInfo, Manager, Config.TyperSetting);
    }
}