using SekaiToolsBase.GameScript;

namespace SekaiToolsBase.Story.StoryEvent;

public class DialogStoryEvent(
    int index,
    string bodyOriginal,
    int characterId,
    string characterOriginal,
    bool closeWindow,
    bool shake)
    : BaseStoryEvent("Dialog", index, bodyOriginal)
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

    public static DialogStoryEvent FromData(Talk talkData, int index = 0)
    {
        return new DialogStoryEvent(
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
        var cloned = new DialogStoryEvent(Index, BodyOriginal, CharacterId, CharacterOriginal, CloseWindow, Shake)
        {
            BodyTranslated = BodyTranslated,
            CharacterTranslated = CharacterTranslated
        };
        return cloned;
    }
}