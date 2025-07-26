using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using SekaiToolsGUI.ViewModel;

namespace SekaiToolsGUI.Model;

public struct Setting
{
    public Setting()
    {
    }

    public string AppVersion { get; init; } = "1.0.0";
    public int CurrentApplicationTheme { get; init; } = 0;
    public string[] CustomSpecialCharacters { get; init; } = [];

    public int ProxyType { get; init; } = 0;
    public string ProxyHost { get; init; } = "127.0.0.1";
    public int ProxyPort { get; init; } = 1080;

    public int TypewriterFadeTime { get; init; } = 50;
    public int TypewriterCharTime { get; init; } = 80;
    public string DialogFontFamily { get; init; } = "思源黑体 CN Bold";
    public string BannerFontFamily { get; init; } = "思源黑体 Medium";
    public string MarkerFontFamily { get; init; } = "思源黑体 Medium";

    public bool ExportLine1 { get; init; } = true;
    public bool ExportLine2 { get; init; } = true;
    public bool ExportLine3 { get; init; } = true;
    public bool ExportCharacter { get; init; } = true;
    public bool ExportBannerMask { get; init; } = true;
    public bool ExportBannerText { get; init; } = true;
    public bool ExportMarkerMask { get; init; } = true;
    public bool ExportMarkerText { get; init; } = true;
    public bool ExportScreenComment { get; init; } = true;

    public static Setting Default { get; } = new()
    {
        ProxyType = 0,
        ProxyHost = "127.0.0.1",
        ProxyPort = 1080,

        TypewriterFadeTime = 50,
        TypewriterCharTime = 80,

        DialogFontFamily = "思源黑体 CN Bold",
        BannerFontFamily = "思源黑体 Medium",
        MarkerFontFamily = "思源黑体 Medium",

        ExportLine1 = true,
        ExportLine2 = true,
        ExportLine3 = true,
        ExportCharacter = true,
        ExportBannerMask = true,
        ExportBannerText = true,
        ExportMarkerMask = true,
        ExportMarkerText = true,
        ExportScreenComment = true
    };

    public static Setting FromModel(SettingPageModel model)
    {
        return new Setting
        {
            AppVersion = SettingPageModel.AppVersion,
            CurrentApplicationTheme = model.CurrentApplicationTheme,
            CustomSpecialCharacters = model.CustomSpecialCharacters.ToArray(),

            ProxyType = model.ProxyType,
            ProxyHost = model.ProxyHost,
            ProxyPort = model.ProxyPort,

            TypewriterFadeTime = model.TypewriterFadeTime,
            TypewriterCharTime = model.TypewriterCharTime,

            DialogFontFamily = model.DialogFontFamily,
            BannerFontFamily = model.BannerFontFamily,
            MarkerFontFamily = model.MarkerFontFamily,

            ExportLine1 = model.ExportLine1,
            ExportLine2 = model.ExportLine2,
            ExportLine3 = model.ExportLine3,
            ExportCharacter = model.ExportCharacter,
            ExportBannerMask = model.ExportBannerMask,
            ExportBannerText = model.ExportBannerText,
            ExportMarkerMask = model.ExportMarkerMask,
            ExportMarkerText = model.ExportMarkerText,
            ExportScreenComment = model.ExportScreenComment
        };
    }

    public string Dump()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            WriteIndented = true
        });
    }

    public static Setting Load(string filepath)
    {
        return !File.Exists(filepath)
            ? Default
            : JsonSerializer.Deserialize<Setting>(File.ReadAllText(filepath));
    }
}