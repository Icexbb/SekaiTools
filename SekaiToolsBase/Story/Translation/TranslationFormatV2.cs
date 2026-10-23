using System.Text;
using System.Text.RegularExpressions;

namespace SekaiToolsBase.Story.Translation;

public static class TranslationFormatV2
{
    public record Entry(string Type, int Number, Translation Value, int Line = 0);

    public static List<(string Type, int Number)> Keys(GameScript.GameScript script)
    {
        var result = new List<(string, int)>();
        int talk = 0, effect = 0;
        foreach (var snippet in script.Snippets)
        {
            if (snippet.Action == 1) result.Add(("dialog", ++talk));
            else if (snippet.Action == 6)
            {
                var index = effect++;
                var number = script.OriginalEffectIndices.Length == script.SpecialEffectData.Length
                    ? script.OriginalEffectIndices[index] + 1 : index + 1;
                result.Add((script.SpecialEffectData[index].EffectType == 8 ? "banner" : "marker", number));
            }
        }
        return result;
    }

    public static (string Scenario, List<Entry> Entries) Parse(string[] lines)
    {
        string? scenario = null;
        var version = false;
        var entries = new List<Entry>();
        var body = new List<string>();
        Entry? current = null;
        void Finish()
        {
            if (current == null) return;
            while (body.Count > 0 && string.IsNullOrWhiteSpace(body[0])) body.RemoveAt(0);
            while (body.Count > 0 && string.IsNullOrWhiteSpace(body[^1])) body.RemoveAt(body.Count - 1);
            current.Value.Body = string.Join("\n", body);
            entries.Add(current);
            body.Clear();
        }
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            FormatException Error(string reason) => new($"翻译文件第 {i + 1} 行：{reason}");
            if (line.StartsWith('#')) continue;
            if (!line.StartsWith('@'))
            {
                if (current == null)
                {
                    if (!string.IsNullOrWhiteSpace(line)) throw Error("正文前缺少条目标记");
                    continue;
                }
                if (line.Length >= 2 && line[0] == '\\' && line[1] is '@' or '#' or '\\') line = line[1..];
                body.Add(line);
                continue;
            }
            var tag = line.TrimEnd(' ', '\t');
            if (!version)
            {
                if (tag != "@version:2") throw Error("第一条有效行必须是 @version:2");
                version = true;
                continue;
            }
            if (tag.StartsWith("@scenario:"))
            {
                if (scenario != null || current != null) throw Error("剧本标识重复或位置不合法");
                scenario = tag[10..];
                if (string.IsNullOrWhiteSpace(scenario)) throw Error("剧本标识为空");
                continue;
            }
            if (scenario == null) throw Error("缺少 @scenario 标记");
            var match = Regex.Match(tag, @"^@(dialog|banner|marker):([0-9]+)(?:\[(.*)\])?$");
            if (!match.Success || !int.TryParse(match.Groups[2].Value, out var number) || number <= 0)
                throw Error("未知标签或条目标记格式错误");
            var type = match.Groups[1].Value;
            if ((type == "dialog") != match.Groups[3].Success) throw Error("对话必须包含说话人方括号，效果不得包含说话人");
            Finish();
            if (entries.Any(e => e.Type == type && e.Number == number)) throw Error($"重复条目 {type}:{number}");
            current = new Entry(type, number, type == "dialog"
                ? new DialogTranslate(match.Groups[3].Value, "") : new EffectTranslate(""), i + 1);
        }
        Finish();
        if (!version || scenario == null) throw new FormatException("翻译文件缺少版本或剧本标识");
        return (scenario, entries);
    }

    public static string Export(GameScript.GameScript script, IReadOnlyList<Translation> values)
    {
        if (string.IsNullOrWhiteSpace(script.ScenarioId)) throw new InvalidOperationException("剧本缺少 ScenarioId，无法导出 v2；可选择旧格式导出");
        var keys = Keys(script);
        if (keys.Count != values.Count) throw new InvalidOperationException("翻译条目数量与剧本不一致");
        var output = new StringBuilder($"@version:2\n@scenario:{script.ScenarioId}\n");
        for (var i = 0; i < keys.Count; i++)
        {
            var value = values[i];
            var speaker = value is DialogTranslate dialog ? $"[{dialog.Chara}]" : "";
            if (speaker.Contains('\n') || speaker.Contains('\r')) throw new InvalidOperationException("说话人不能包含换行");
            output.Append($"\n@{keys[i].Type}:{keys[i].Number:D3}{speaker}\n");
            var content = value is DialogTranslate ? value.Body.Trim() : value.Body;
            foreach (var line in content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                if (line.Length > 0 && line[0] is '@' or '#' or '\\') output.Append('\\');
                output.Append(line).Append('\n');
            }
        }
        return output.ToString();
    }
}
