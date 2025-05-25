using SekaiDataFetch.Data;

namespace SekaiDataFetch.Item;

public class AreaStorySet(ActionSet actionSet) : ICloneable
{
    public ActionSet ActionSet { get; } = actionSet;
    public string ScenarioId { get; } = actionSet.ScenarioId;
    public int Group { get; } = actionSet.Id / 100;

    public int[] CharacterIds { get; set; } = [];


    public object Clone()
    {
        return new AreaStorySet(ActionSet)
        {
            CharacterIds = CharacterIds
        };
    }
}