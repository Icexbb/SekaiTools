using System.Text.RegularExpressions;
using SekaiToolsBase.GameScript;

namespace SekaiToolsBase.Story;

public sealed record FlashbackInfo(string MainSource, string[] Sources, string[] VoiceIds)
{
    public bool IsFlashback => Sources.Any(source => source != MainSource);
    public string Hint => $"根据语音来源推测，可能为闪回。\n当前主要来源：{MainSource}\n来源：" +
        string.Join("、", Sources.Where(source => source != MainSource).Select(FlashbackAnalyzer.DescribeSource)) +
        "\n语音：" + string.Join("、", VoiceIds);
}

/// <summary>从语音命名推测来源；不将推测写入翻译正文。</summary>
public static partial class FlashbackAnalyzer
{
    [GeneratedRegex(@"voice_(.+)_\d+[a-z]?_\d+(?:_?.*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex VoicePattern();

    public static string? GetSource(string? voiceId)
    {
        if (string.IsNullOrEmpty(voiceId) || voiceId.Contains("partvoice", StringComparison.Ordinal)) return null;
        var match = VoicePattern().Match(voiceId);
        return match.Success ? match.Groups[1].Value : null;
    }

    public static FlashbackInfo?[] Analyze(Talk[] talks)
    {
        var sources = talks.Select(talk => (talk.Voices ?? []).Select(voice => GetSource(voice.VoiceId))
            .OfType<string>().Distinct(StringComparer.Ordinal).ToArray()).ToArray();
        // 每行每个来源只计一次；并列时采用最先出现的来源，与参考实现一致。
        var main = sources.SelectMany(source => source).GroupBy(source => source, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count()).FirstOrDefault()?.Key;
        return sources.Select((source, index) => main == null ? null : new FlashbackInfo(main, source,
            (talks[index].Voices ?? []).Select(voice => voice.VoiceId).ToArray())).ToArray();
    }

    public static string DescribeSource(string source)
    {
        var value = source.StartsWith("sc_", StringComparison.Ordinal) ? source[3..] : source;
        var kind = value.StartsWith("card_", StringComparison.Ordinal) ||
                   value.StartsWith("ev_", StringComparison.Ordinal) && Regex.IsMatch(value, @"_\d+[ab]$")
            ? "卡面剧情"
            : value.StartsWith("ev_", StringComparison.Ordinal) ? "活动剧情"
            : value.StartsWith("op_", StringComparison.Ordinal) ? "主线序章"
            : value.StartsWith("ms_", StringComparison.Ordinal) || value.StartsWith("unit_", StringComparison.Ordinal)
                ? "主线剧情" : "未知剧情";
        return $"{kind}（{source}）";
    }
}
