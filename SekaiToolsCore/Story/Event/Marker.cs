namespace SekaiToolsCore.Story.Event;

public class Marker(string bodyOriginal, int index) : Event("Marker", index, bodyOriginal)
{
    public override object Clone()
    {
        var cloned = new Marker(BodyOriginal, Index) { BodyTranslated = BodyTranslated };
        return cloned;
    }
}