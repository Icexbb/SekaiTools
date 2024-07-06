using Newtonsoft.Json.Linq;

namespace SekaiToolsCore.Story.Game;

public struct Snippet(int action, int index, int progressBehavior, int referenceIndex, int delay)
{
    /*
     * 1 TalkData
     * 2 LayoutData Type=*
     * 3
     * 4 LayoutData Type=0
     * 5
     * 6 SpecialEffectData
     * 7 SoundData
     * 8 ScenarioSnippetCharacterLayoutModes
     */
    public int Action = action;
    public int Index = index;
    public int ProgressBehavior = progressBehavior;
    public int ReferenceIndex = referenceIndex;
    public int Delay = delay;

    public static Snippet FromJObject(JObject json)
    {
        return new Snippet(
            json.Get("Action", 0),
            json.Get("Index", 0),
            json.Get("ProgressBehavior", 0),
            json.Get("ReferenceIndex", 0),
            json.Get("Delay", 0)
        );
    }
}