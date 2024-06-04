namespace SekaiDataFetch;

public static class Constants
{
    public static readonly Dictionary<string, string> UnitName = new()
    {
        { "light_sound", "Leo/need" },
        { "idol", "MORE MORE JUMP！" },
        { "street", "Vivid BAD SQUAD" },
        { "school_refusal", "25時、ナイトコードで。" },
        { "theme_park", "ワンダーランズ\u00d7ショウタイムストーリー" },
        { "piapro", "Piapro" }
    };

    public static readonly Dictionary<int, string> CharacterIdToName = new()
    {
        { 1, "一歌" }, { 2, "咲希" }, { 3, "穗波" }, { 4, "志步" },
        { 5, "实乃理" }, { 6, "遥" }, { 7, "爱莉" }, { 8, "雫" },
        { 9, "心羽" }, { 10, "杏" }, { 11, "彰人" }, { 12, "冬弥" },
        { 13, "司" }, { 14, "笑梦" }, { 15, "宁宁" }, { 16, "类" },
        { 17, "奏" }, { 18, "真冬" }, { 19, "绘名" }, { 20, "瑞希" },
        { 21, "MIKU" }, { 22, "RIN" }, { 23, "LEN" }, { 24, "LUKA" }, { 25, "MEIKO" }, { 26, "KAITO" },
        { 27, "MIKU_LN" }, { 28, "MIKU_MMJ" }, { 29, "MIKU_VBS" }, { 30, "MIKU_WS" }, { 31, "MIKU_25" }
    };
}