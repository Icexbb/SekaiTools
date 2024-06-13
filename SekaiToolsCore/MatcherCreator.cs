using SekaiToolsCore.Process;
using SekaiStory = SekaiToolsCore.Story.Story;

namespace SekaiToolsCore;

public class MatcherCreator
{
    private Config Config { get; }
    private VideoInfo VInfo { get; }
    private SekaiStory Story { get; }
    private TemplateManager Manager { get; }

    public MatcherCreator(string videoFilePath, string scriptFilePath, string translateFilePath = "",
        string outputFilePath = "")
    {
        Config = new Config(videoFilePath, scriptFilePath, translateFilePath, outputFilePath);
        VInfo = new VideoInfo(videoFilePath);
        Story = SekaiStory.FromFile(scriptFilePath, translateFilePath);

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
        return new DialogMatcher(VInfo, Story, Manager);
    }

    public ContentMatcher ContentMatcher()
    {
        return new ContentMatcher(Manager);
    }

    public BannerMatcher BannerMatcher()
    {
        return new BannerMatcher(VInfo, Story, Manager);
    }

    public MarkerMatcher MarkerMatcher()
    {
        return new MarkerMatcher(VInfo, Story, Manager);
    }

    public SubtitleMaker SubtitleMaker()
    {
        return new SubtitleMaker(VInfo, Manager, Config.TyperSetting);
    }
}