namespace SekaiToolsCore.Process.Model;

public struct TypewriterSetting()
{
    public int FadeTime { get; init; } = 50;
    public int CharTime { get; init; } = 80;
}

public struct MatchingThreshold()
{
    public double DialogNormal { get; init; } = 0.85;
    public double DialogSpecial { get; init; } = 0.75;

    public double BannerNormal { get; init; } = 0.75;

    public double MarkerNormal { get; init; } = 0.75;
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