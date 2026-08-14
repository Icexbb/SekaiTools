namespace SekaiToolsGUI.ViewModel.Download;

public static class CharacterFilterOptions
{
    private static readonly IReadOnlyDictionary<int, string> CharacterNames = new Dictionary<int, string>
    {
        [1] = "星乃一歌", [2] = "天马咲希", [3] = "望月穗波", [4] = "日野森志步",
        [5] = "花里实乃理", [6] = "桐谷遥", [7] = "桃井爱莉", [8] = "日野森雫",
        [9] = "小豆泽心羽", [10] = "白石杏", [11] = "东云彰人", [12] = "青柳冬弥",
        [13] = "天马司", [14] = "凤笑梦", [15] = "草薙宁宁", [16] = "神代类",
        [17] = "宵崎奏", [18] = "朝比奈真冬", [19] = "东云绘名", [20] = "晓山瑞希",
        [21] = "初音未来", [22] = "镜音铃", [23] = "镜音连", [24] = "巡音流歌",
        [25] = "MEIKO", [26] = "KAITO",
        [27] = "初音未来", [28] = "初音未来", [29] = "初音未来",
        [30] = "初音未来", [31] = "初音未来"
    };

    private static readonly (string GroupName, int[] CharacterIds)[] CharacterGroups =
    [
        ("全部", [0]),
        ("Leo/need", [1, 2, 3, 4, 27]),
        ("MORE MORE JUMP！", [5, 6, 7, 8, 28]),
        ("Vivid BAD SQUAD", [9, 10, 11, 12, 29]),
        ("Wonderlands×Showtime", [13, 14, 15, 16, 30]),
        ("25时，在Nightcord。", [17, 18, 19, 20, 31]),
        ("Piapro Characters", [21, 22, 23, 24, 25, 26])
    ];

    public static CharacterComboBoxItem[] CreateItems(bool includeUnitMikuVariants = true)
    {
        return CharacterGroups
            .SelectMany(group => group.CharacterIds
                .Where(characterId => includeUnitMikuVariants || characterId < 27)
                .Select(characterId => new CharacterComboBoxItem
                {
                    GroupName = group.GroupName,
                    Name = characterId == 0 ? "全部角色" : CharacterNames[characterId],
                    Value = characterId,
                    GameCharacterId = characterId is >= 27 and <= 31 ? 21 : characterId,
                    Source = characterId == 0
                        ? "pack://application:,,,/Resource/icon.png"
                        : $"pack://application:,,,/Resource/Characters/chr_{characterId}.png"
                }))
            .ToArray();
    }
}
