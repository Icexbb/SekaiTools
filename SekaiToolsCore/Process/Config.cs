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

public struct ExportStyleConfig
{
    public bool ExportLine1 { get; init; } = true;
    public bool ExportLine2 { get; init; } = true;
    public bool ExportLine3 { get; init; } = true;
    public bool ExportCharacter { get; init; } = true;
    public bool ExportBannerMask { get; init; } = true;
    public bool ExportBannerText { get; init; } = true;
    public bool ExportMarkerMask { get; init; } = true;
    public bool ExportMarkerText { get; init; } = true;
    public bool ExportScreenComment { get; init; } = true;

    public ExportStyleConfig()
    {
    }
}

public struct StyleFontConfig
{
    public string DialogFontFamily { get; init; } = "思源黑体 CN Bold";
    public string BannerFontFamily { get; init; } = "思源黑体 Medium";
    public string MarkerFontFamily { get; init; } = "思源黑体 Medium";

    public StyleFontConfig()
    {
    }
}

public class Config
{
    public Config(
        string videoFilePath,
        string scriptFilePath,
        string translateFilePath,
        StyleFontConfig styleFontConfig = default,
        ExportStyleConfig exportStyleConfig = default,
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
        StyleFontConfig = styleFontConfig;
        ExportStyleConfig = exportStyleConfig;

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

    public StyleFontConfig StyleFontConfig { get; }

    public ExportStyleConfig ExportStyleConfig { get; }
}