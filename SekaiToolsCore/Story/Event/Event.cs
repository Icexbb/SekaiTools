namespace SekaiToolsCore.Story.Event;

public abstract class Event(string type, int index, string origin, string translated = "") : ICloneable
{
    public readonly string BodyOriginal = origin;
    public readonly int Index = index;

    public readonly string Type = type;
    public string BodyTranslated = translated;

    public string FinalContent => BodyTranslated.Length > 0 ? BodyTranslated : BodyOriginal;

    public abstract object Clone();
}