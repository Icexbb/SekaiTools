namespace SekaiToolsBase.Story.StoryEvent;

public class BannerStoryEvent(string bodyOriginal, int index, int totalIndex = -1)
    : BaseStoryEvent("Banner", index, bodyOriginal, storyIndex: totalIndex)
{
    // 保留旧字段以兼容字幕淡入逻辑；其值与全局 StoryIndex 相同。
    public readonly int TotalIndex = totalIndex;

    public override object Clone()
    {
        var cloned = new BannerStoryEvent(BodyOriginal, Index, StoryIndex) { BodyTranslated = BodyTranslated };
        return cloned;
    }
}
