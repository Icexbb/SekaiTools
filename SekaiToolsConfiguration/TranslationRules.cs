using System.Text.Json;

namespace SekaiToolsConfiguration;

public sealed class TranslationRules
{
    public const string FileName = "translation-rules.json";
    private static readonly Lazy<TranslationRules> CurrentValue = new(() => Load());
    public static TranslationRules Current => CurrentValue.Value;

    public required List<TranslationReplacement> Replacements { get; init; }
    public required bool CheckEndPunctuation { get; init; }
    public required string ValidEndCharacters { get; init; }
    public required string ClosingCharacters { get; init; }
    public required string MissingEndMessage { get; init; }
    public required bool CheckEllipsisPunctuation { get; init; }
    public required List<string> InvalidEndings { get; init; }
    public required string InvalidEndingMessage { get; init; }
    public required bool CheckDash { get; init; }
    public required string DashCharacter { get; init; }
    public required int DashGroupLength { get; init; }
    public required string DashMessage { get; init; }
    public required bool CheckLineLength { get; init; }
    public required int MaxLineLength { get; init; }
    public required int ThreeLineMaxLength { get; init; }

    public static TranslationRules Load(string? filePath = null)
    {
        var path = filePath ?? Path.Combine(AppContext.BaseDirectory, FileName);
        string json;
        if (File.Exists(path)) json = File.ReadAllText(path);
        else
        {
            using var stream = typeof(TranslationRules).Assembly.GetManifestResourceStream(
                "SekaiToolsConfiguration.translation-rules.json")
                ?? throw new FileNotFoundException("找不到内置翻译检查配置");
            using var reader = new StreamReader(stream);
            json = reader.ReadToEnd();
        }
        var rules = JsonSerializer.Deserialize<TranslationRules>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("翻译检查配置为空");
        rules.Validate();
        return rules;
    }

    private void Validate()
    {
        if (Replacements == null || Replacements.Any(r => r == null || string.IsNullOrEmpty(r.From) || r.To == null))
            throw new InvalidDataException("翻译替换规则的 from 不能为空，to 不能为 null");
        if (ValidEndCharacters == null || ClosingCharacters == null ||
            MissingEndMessage == null || InvalidEndingMessage == null || DashMessage == null ||
            InvalidEndings == null || InvalidEndings.Any(string.IsNullOrEmpty))
            throw new InvalidDataException("翻译检查符号、提示及非法结尾配置不能为 null，非法结尾不能为空");
        if (CheckEndPunctuation && ValidEndCharacters.Length == 0)
            throw new InvalidDataException("启用结束标点检查时，合法结束符号不能为空");
        if (DashCharacter == null || DashCharacter.Length != 1 || DashGroupLength < 1)
            throw new InvalidDataException("破折号字符必须为单个字符，分组长度必须大于零");
        if (MaxLineLength < 1 || ThreeLineMaxLength < 1)
            throw new InvalidDataException("翻译行长阈值必须大于零");
    }

    public string Format(string content)
    {
        foreach (var rule in Replacements)
            content = content.Replace(rule.From, rule.To, StringComparison.Ordinal);
        return content;
    }

    public string Check(string content)
    {
        var result = new List<string>();
        var lines = content.Split('\n').Where(line => line.Length > 0).ToArray();
        var closings = ClosingCharacters.ToCharArray();
        var dashGroup = new string(DashCharacter[0], DashGroupLength);
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd();
            var ending = line.TrimEnd(closings);
            var messages = "";
            if (CheckEndPunctuation && (ending.Length == 0 || !ValidEndCharacters.Contains(ending[^1])))
                messages += $"【{MissingEndMessage}】";
            if (CheckEllipsisPunctuation && InvalidEndings.Any(suffix => ending.EndsWith(suffix, StringComparison.Ordinal)))
                messages += $"【{InvalidEndingMessage}】";
            if (CheckDash && line.Contains(dashGroup, StringComparison.Ordinal) &&
                line.Split(DashCharacter).Length != (line.Split(dashGroup).Length - 1) * DashGroupLength + 1)
                messages += $"【{DashMessage}】";
            if (messages.Length > 0) result.Add($"行{i + 1}:{messages}");
        }
        return string.Join("\n", result);
    }
}

public sealed class TranslationReplacement
{
    public required string From { get; init; }
    public required string To { get; init; }
}
