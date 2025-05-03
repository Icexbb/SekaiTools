using SekaiDataFetch.Data;
using SekaiDataFetch.Source;

namespace SekaiDataFetch.Item;

public class AreaStory(ActionSet actionSet) : ICloneable
{
    public ActionSet ActionSet { get; } = actionSet;
    public string ScenarioId { get; } = actionSet.ScenarioId;
    public int Group { get; } = actionSet.Id / 100;

    public int[] CharacterIds { get; set; } = [];


    public object Clone()
    {
        return new AreaStory(ActionSet)
        {
            CharacterIds = CharacterIds
        };
    }

    public string Url(SourceType sourceType = SourceType.SiteBest)
    {
        
        return sourceType switch
        {
            SourceType.SiteBest =>
                $"https://storage.sekai.best/sekai-jp-assets/scenario/actionset" +
                $"/group{Group}_rip/{ScenarioId}.asset",
            SourceType.SiteHaruki =>
                $"https://storage.haruki.wacca.cn/assets/startapp/scenario/actionset/" +
                $"group{Group}/{ScenarioId}.json",
            _ => throw new ArgumentOutOfRangeException(nameof(sourceType), sourceType, null)
        };
    }
}