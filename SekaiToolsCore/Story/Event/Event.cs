namespace SekaiToolsCore.Story.Event;

public abstract class Event(string type, string bodyOriginal) : ICloneable
{
    public readonly string Type = type;
    public readonly string BodyOriginal = bodyOriginal;
    public string BodyTranslated = "";

    public string FinalContent => BodyTranslated.Length > 0 ? BodyTranslated : BodyOriginal;

    public abstract object Clone();
}