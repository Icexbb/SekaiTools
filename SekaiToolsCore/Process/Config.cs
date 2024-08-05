namespace SekaiToolsCore.Process;

public struct TypewriterSetting(int fadeTime, int charTime)
{
    public readonly int FadeTime = fadeTime;
    public readonly int CharTime = charTime;
}

public struct MatchingThreshold(double normal, double special)
{
    public readonly double Normal = normal;
    public readonly double Special = special;
}

public class Config
{
    public Config(
        string videoFilePath,
        string scriptFilePath,
        string translateFilePath,
        string? fontName = null,
        bool exportComment = false,
        TypewriterSetting? typerSetting = null,
        MatchingThreshold? matchingThreshold = null
    )
    {
        if (!Path.Exists(videoFilePath))
            throw new FileNotFoundException("Video file not found.", videoFilePath);
        if (!Path.Exists(scriptFilePath))
            throw new FileNotFoundException("Script file not found.", scriptFilePath);

        VideoFilePath = videoFilePath;
        ScriptFilePath = scriptFilePath;
        FontName = fontName ?? "思源黑体 CN Bold";
        ExportComment = exportComment;

        if (translateFilePath != "" && !Path.Exists(translateFilePath))
            throw new FileNotFoundException("Translation file not found.", translateFilePath);
        TranslateFilePath = translateFilePath;

        TyperSetting = typerSetting ?? new TypewriterSetting(50, 80);
        MatchingThreshold = matchingThreshold ?? new MatchingThreshold(0.8, 0.65);
    }

    public string VideoFilePath { get; }
    public string ScriptFilePath { get; }
    public string TranslateFilePath { get; }


    public TypewriterSetting TyperSetting { get; }

    public MatchingThreshold MatchingThreshold { get; }

    public string FontName { get; }

    public bool ExportComment { get; }
}