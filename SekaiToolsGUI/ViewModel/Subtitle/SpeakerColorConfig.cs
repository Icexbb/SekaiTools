using System.Windows.Media;

namespace SekaiToolsGUI.ViewModel.Subtitle;

internal sealed record SpeakerColorPalette(Brush Background, Brush Foreground);

internal static class SpeakerColorConfig
{
    // Project Sekai 剧本角色 ID -> UI 代表色。未配置的 ID 由界面回退到当前主题强调色。
    private static readonly IReadOnlyDictionary<int, string> SpeakerColors = new Dictionary<int, string>
    {
        [1] = "#33AAEE",  // 一歌
        [2] = "#FFDD44",  // 咲希
        [3] = "#EE6666",  // 穗波
        [4] = "#BBDD22",  // 志步
        [5] = "#FFCCAA",  // 实乃理
        [6] = "#99CCFF",  // 遥
        [7] = "#FFAACC",  // 爱莉
        [8] = "#99EEDD",  // 雫
        [9] = "#FF6699",  // 心羽
        [10] = "#00BBDD", // 杏
        [11] = "#FF7722", // 彰人
        [12] = "#0077DD", // 冬弥
        [13] = "#FFBB00", // 司
        [14] = "#FF66BB", // 笑梦
        [15] = "#33DD99", // 宁宁
        [16] = "#BB88EE", // 类
        [17] = "#BB6688", // 奏
        [18] = "#8888CC", // 真冬
        [19] = "#CCAA88", // 绘名
        [20] = "#DDAACC", // 瑞希
        [21] = "#33CCBB", // MIKU
        [22] = "#FFCC11", // RIN
        [23] = "#FFEE11", // LEN
        [24] = "#FFBBCC", // LUKA
        [25] = "#DD4444", // MEIKO
        [26] = "#3366CC", // KAITO
        [27] = "#33CCBB", // MIKU_LN
        [28] = "#33CCBB", // MIKU_MMJ
        [29] = "#33CCBB", // MIKU_VBS
        [30] = "#33CCBB", // MIKU_WS
        [31] = "#33CCBB"  // MIKU_25
    };

    private static readonly IReadOnlyDictionary<int, SpeakerColorPalette> Palettes =
        SpeakerColors.ToDictionary(pair => pair.Key, pair => CreatePalette(pair.Value));

    public static SpeakerColorPalette? Get(int speakerId)
    {
        return Palettes.GetValueOrDefault(speakerId);
    }

    private static SpeakerColorPalette CreatePalette(string colorValue)
    {
        var color = (Color)ColorConverter.ConvertFromString(colorValue)!;
        var background = new SolidColorBrush(color);
        background.Freeze();

        var luminance = color.R * 0.299 + color.G * 0.587 + color.B * 0.114;
        var foreground = luminance >= 160 ? Brushes.Black : Brushes.White;
        return new SpeakerColorPalette(background, foreground);
    }
}
