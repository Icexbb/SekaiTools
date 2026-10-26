namespace SekaiToolsCore.Process.Config;

public struct ExportStyleConfig
{
    public bool ExportLine1 { get; init; } = true;
    public bool ExportLine2 { get; init; } = true;
    public bool ExportLine3 { get; init; } = true;
    // ExportCharacter 保留原配置键，用于与原文不同的说话人。
    public bool ExportUnchangedCharacter { get; init; } = false;
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
