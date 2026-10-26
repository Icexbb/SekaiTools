using System.Text.Json;
using System.Text.RegularExpressions;

namespace SekaiToolsConfiguration;

public sealed class TranslationRules
{
    public const string FileName = "translation-rules.json";
    private static readonly Lazy<TranslationRules> CurrentValue = new(() => Load());
    public static TranslationRules Current => CurrentValue.Value;
    private readonly Dictionary<string, Regex> _patterns = new(StringComparer.Ordinal);

    public required List<TranslationReplacement> Replacements { get; init; }
    public required List<TranslationCheck> Checks { get; init; }

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
        if (Replacements == null || Checks == null)
            throw new InvalidDataException("翻译配置必须包含 replacements 和 checks 数组");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rule in Replacements)
        {
            if (rule == null || rule.Replacement == null)
                throw new InvalidDataException("替换规则及 replacement 不能为 null");
            AddPattern(rule.Pattern, "替换规则");
        }
        foreach (var rule in Checks)
        {
            if (rule == null || string.IsNullOrWhiteSpace(rule.Id) || !ids.Add(rule.Id))
                throw new InvalidDataException("检查项 id 必须非空且唯一");
            if (rule.Scope is not ("line" or "content"))
                throw new InvalidDataException($"检查项 {rule.Id} 的 scope 必须为 line 或 content");
            if (rule.Conditions == null || rule.Conditions.Count == 0 ||
                rule.Feedback == null || string.IsNullOrWhiteSpace(rule.Feedback.Message) || rule.Feedback.Tags == null)
                throw new InvalidDataException($"检查项 {rule.Id} 必须包含条件和反馈文字");
            foreach (var condition in rule.Conditions)
            {
                if (condition == null || string.IsNullOrWhiteSpace(condition.Target))
                    throw new InvalidDataException($"检查项 {rule.Id} 的条件必须指定 target");
                AddPattern(condition.Pattern, rule.Id);
            }
        }
    }

    private void AddPattern(string pattern, string id)
    {
        if (string.IsNullOrEmpty(pattern)) throw new InvalidDataException($"规则 {id} 的 pattern 不能为空");
        try { _patterns.TryAdd(pattern, new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))); }
        catch (ArgumentException exception) { throw new InvalidDataException($"规则 {id} 的正则表达式无效：{exception.Message}", exception); }
    }

    public string Format(string content)
    {
        foreach (var rule in Replacements.Where(rule => rule.Enabled))
            content = _patterns[rule.Pattern].Replace(content, rule.Replacement);
        return content;
    }

    public IReadOnlyList<TranslationCheckResult> Evaluate(string content, IReadOnlyDictionary<string, string>? context = null)
    {
        var results = new List<TranslationCheckResult>();
        var lines = content.Split('\n').Where(line => line.Length > 0).ToArray();
        foreach (var rule in Checks.Where(rule => rule.Enabled))
        {
            if (rule.Scope == "content") EvaluateText(content, null);
            else for (var i = 0; i < lines.Length; i++) EvaluateText(lines[i].TrimEnd(), i + 1);

            void EvaluateText(string text, int? line)
            {
                foreach (var condition in rule.Conditions)
                {
                    var value = condition.Target == "text" ? text : context?.GetValueOrDefault(condition.Target);
                    // 未提供的指标不参与检查，包括否定条件。
                    if (value == null || _patterns[condition.Pattern].IsMatch(value) != condition.Match) return;
                }
                results.Add(new TranslationCheckResult(rule.Id, line, rule.Feedback));
            }
        }
        return results;
    }

    public string Check(string content) => FormatResults(Evaluate(content));

    public static string FormatResults(IEnumerable<TranslationCheckResult> results) => string.Join("\n",
        results.GroupBy(result => result.Line).OrderBy(group => group.Key ?? int.MaxValue)
            .Select(group => (group.Key is { } line ? $"行{line}:" : "") +
                             string.Concat(group.Select(result => $"【{result.Feedback.Message}】"))));
}

public sealed class TranslationReplacement
{
    public bool Enabled { get; init; } = true;
    public required string Pattern { get; init; }
    public required string Replacement { get; init; }
}

public sealed class TranslationCheck
{
    public required string Id { get; init; }
    public bool Enabled { get; init; } = true;
    public string Scope { get; init; } = "line";
    public required List<TranslationCondition> Conditions { get; init; }
    public required TranslationFeedback Feedback { get; init; }
}

public sealed class TranslationCondition
{
    public string Target { get; init; } = "text";
    public required string Pattern { get; init; }
    public bool Match { get; init; } = true;
}

public sealed class TranslationFeedback
{
    public required string Message { get; init; }
    public List<string> Tags { get; init; } = [];
}

public sealed record TranslationCheckResult(string Id, int? Line, TranslationFeedback Feedback);
