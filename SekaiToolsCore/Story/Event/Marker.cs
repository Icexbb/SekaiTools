namespace SekaiToolsCore.Story.Event;

public class Marker(string bodyOriginal, int index) : Event("Marker", bodyOriginal)
{
    public readonly int Index = index;

    public override object Clone()
    {
        var cloned = new Marker(BodyOriginal, Index) { BodyTranslated = BodyTranslated };
        return cloned;
    }
}