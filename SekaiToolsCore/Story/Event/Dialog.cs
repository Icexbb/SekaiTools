using SekaiToolsCore.Story.Game;

namespace SekaiToolsCore.Story.Event;

public class Dialog(
    int index,
    string bodyOriginal,
    int characterId,
    string characterOriginal,
    bool closeWindow,
    bool shake)
    : Event("Dialog", index, bodyOriginal)
{
    public readonly int CharacterId = characterId;
    public readonly string CharacterOriginal = characterOriginal;
    public readonly bool CloseWindow = closeWindow;
    public readonly bool Shake = shake;
    public string CharacterTranslated = "";

    public string FinalCharacter => CharacterTranslated.Length > 0 && CharacterTranslated != CharacterOriginal
        ? CharacterTranslated
        : CharacterOriginal;

    public void SetTranslation(string character, string body)
    {
        CharacterTranslated = character;
        BodyTranslated = body;
    }

    public void SetTranslationContent(string body)
    {
        BodyTranslated = body;
    }

    public static Dialog FromData(Talk talkData, int index = 0)
    {
        return new Dialog(
            index,
            talkData.Body,
            talkData.GetCharacterId(),
            talkData.WindowDisplayName,
            talkData.WhenFinishCloseWindow == 1,
            talkData.Shake
        );
    }

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