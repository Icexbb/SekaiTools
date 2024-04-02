namespace SekaiToolsCore.Story.Event;

public class Dialog(
    int index,
    string bodyOriginal,
    int characterId,
    string characterOriginal,
    bool closeWindow,
    bool shake)
    : Event("Dialog", bodyOriginal)
{
    public readonly int Index = index;
    public readonly int CharacterId = characterId;
    public readonly string CharacterOriginal = characterOriginal;
    public string CharacterTranslated = "";
    public readonly bool CloseWindow = closeWindow;
    public readonly bool Shake = shake;

    public void SetTranslation(string character, string body)
    {
        CharacterTranslated = character;
        BodyTranslated = body;
    }

    public string FinalCharacter => CharacterTranslated.Length > 0 && CharacterTranslated != CharacterOriginal
        ? CharacterTranslated
        : CharacterOriginal;

    public override object Clone()
    {
        var cloned = new Dialog(Index, BodyOriginal, CharacterId, CharacterOriginal, CloseWindow, Shake)
        {
            BodyTranslated = BodyTranslated,
            CharacterTranslated = CharacterTranslated
        };
        return cloned;
    }
}