namespace SekaiToolsCore.Story.Translation;

public abstract class Translation(string body)
{
    public readonly string Body = body.Trim();
}

public class DialogTranslate(string chara, string body) : Translation(body)
{
    public readonly string Chara = chara.Trim();
}

public class EffectTranslate(string body) : Translation(body)
{
}