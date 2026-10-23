namespace SekaiToolsBase.Story.Translation;

public static class TranslationText
{
    /// <summary>Normalize physical line endings before splitting, then trim each line.</summary>
    public static string Normalize(string content) => string.Join("\n",
        content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Select(line => line.Trim())).Trim();
}
