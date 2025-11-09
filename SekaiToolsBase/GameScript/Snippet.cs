namespace SekaiToolsBase.GameScript;

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

public struct Snippet()
{
    public Snippet(int action, int index, int progressBehavior, int referenceIndex, double delay) : this()
    {
        Action = action;
        Index = index;
        ProgressBehavior = progressBehavior;
        ReferenceIndex = referenceIndex;
        Delay = delay;
    }


    public int Action { get; set; } = 0;
    public int Index { get; set; } = 0;
    public int ProgressBehavior { get; set; } = 0;
    public int ReferenceIndex { get; set; } = 0;
    public double Delay { get; set; } = 0.0;
}