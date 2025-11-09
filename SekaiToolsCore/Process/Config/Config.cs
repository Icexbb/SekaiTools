namespace SekaiToolsCore.Process.Config;

public class Config
{
    public Config(
        string videoFilePath,
        string scriptFilePath,
        string translateFilePath,
        StyleFontConfig styleFontConfig = default,
        ExportStyleConfig exportStyleConfig = default,
        TypewriterSetting typerSetting = default,
        MatchingThreshold matchingThreshold = default
    )
    {
        if (!Path.Exists(videoFilePath))
            throw new FileNotFoundException("Video file not found.", videoFilePath);
        if (!Path.Exists(scriptFilePath))
            throw new FileNotFoundException("Script file not found.", scriptFilePath);
        if (translateFilePath != "" && !Path.Exists(translateFilePath))
            throw new FileNotFoundException("Translation file not found.", translateFilePath);

        VideoFilePath = videoFilePath;
        ScriptFilePath = scriptFilePath;
        TranslateFilePath = translateFilePath;

        StyleFontConfig = styleFontConfig;
        ExportStyleConfig = exportStyleConfig;

        TyperSetting = typerSetting;
        MatchingThreshold = matchingThreshold;
    }

    public string VideoFilePath { get; }
    public string ScriptFilePath { get; }
    public string TranslateFilePath { get; }

    public TypewriterSetting TyperSetting { get; }

    public MatchingThreshold MatchingThreshold { get; }

    public StyleFontConfig StyleFontConfig { get; }

    public ExportStyleConfig ExportStyleConfig { get; }
}