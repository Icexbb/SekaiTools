using SekaiToolsBase.Story.Translation;

namespace SekaiToolsBase.Story;

public sealed record SubtitleMaterialValidationResult(bool CanUse, bool IsPending, string Title, string Details);

/// <summary>Preflight the same parsed script and translation used by subtitle recognition.</summary>
public static class SubtitleMaterialValidation
{
    public static SubtitleMaterialValidationResult Check(string scriptPath, string translationPath)
    {
        var errors = new List<string>();
        var summary = new List<string>();
        GameScript.GameScript? script = null;
        TranslationData? translation = null;
        if (!string.IsNullOrWhiteSpace(scriptPath))
        {
            try
            {
                script = new GameScript.GameScript(scriptPath);
                if (script.Empty() || script.Snippets.Length == 0)
                    errors.Add("剧本不包含可识别的对话、横幅或标记，请选择游戏剧情剧本文件。");
                else if (script.Snippets.Count(item => item.Action == 1) != script.TalkData.Length ||
                         script.Snippets.Count(item => item.Action == 6) != script.SpecialEffectData.Length)
                    errors.Add("剧本的事件顺序与对话/特效数据数量不一致，无法用于轴机识别。");
                else
                    summary.Add($"剧本：{script.TalkData.Length} 条对话，{script.SpecialEffectData.Length} 条横幅/标记。");
            }
            catch (Exception exception)
            {
                errors.Add($"剧本文件无法读取或解析：{exception.Message}");
            }
        }

        if (!string.IsNullOrWhiteSpace(translationPath))
        {
            try
            {
                translation = new TranslationData(translationPath);
                if (translation.IsEmpty())
                    errors.Add("翻译文件没有任何翻译条目，请选择包含翻译内容的 TXT 文件。");
                else
                    summary.Add($"翻译：{(translation.IsV2 ? "TXT v2" : "旧 TXT")}，{translation.Translations.Count} 个条目。");
            }
            catch (Exception exception)
            {
                errors.Add($"翻译文件无法读取或解析：{exception.Message}");
            }
        }

        if (errors.Count == 0 && script != null && translation != null)
        {
            try
            {
                var reason = translation.GetMismatchReason(script);
                if (reason != null) errors.Add("翻译不适用于当前剧本：\n" + reason);
                else _ = new Story(script, translation);
            }
            catch (Exception exception)
            {
                errors.Add($"剧本与翻译无法用于字幕处理：{exception.Message}");
            }
        }

        var pending = string.IsNullOrWhiteSpace(scriptPath) || string.IsNullOrWhiteSpace(translationPath);
        if (errors.Count > 0)
            return new(false, false, "文件不适用", string.Join("\n\n", errors));
        if (pending)
            return new(false, true, "等待选择文件", string.Join("\n", summary.Append("请选择剧本和翻译文件，选择后自动检查适用性。")));
        return new(true, false, "剧本与翻译检查通过", string.Join("\n", summary));
    }
}
