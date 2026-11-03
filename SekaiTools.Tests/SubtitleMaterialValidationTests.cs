using System.Text.Json;
using SekaiToolsBase.GameScript;
using SekaiToolsBase.Story;

namespace SekaiTools.Tests;

public sealed class SubtitleMaterialValidationTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "SekaiTools-MaterialValidation-" + Guid.NewGuid());
    private readonly string _script;
    private readonly string _translation;

    public SubtitleMaterialValidationTests()
    {
        Directory.CreateDirectory(_directory);
        _script = Path.Combine(_directory, "script.json");
        _translation = Path.Combine(_directory, "translation.txt");
        File.WriteAllText(_script, JsonSerializer.Serialize(new GameScript
        {
            ScenarioId = "test",
            TalkData = [new("角色", "原文", 0)],
            SpecialEffectData = [new(8, "横幅", "", 0, 0)],
            Snippets = [new() { Action = 1 }, new() { Action = 6 }]
        }));
    }

    [Theory]
    [InlineData("角色：译文\n横幅", true, "")]
    [InlineData("角色：译文", false, "特效数量不匹配")]
    [InlineData("横幅\n角色：译文", false, "第 1 个条目类型不匹配")]
    [InlineData("# 只有注释", false, "没有任何翻译条目")]
    [InlineData("@version:2\n@scenario:test\n@dialog:001[]\n@banner:001", true, "")]
    [InlineData("@version:2\n@scenario:other\n@dialog:001[角色]\n译文", false, "剧本标识")]
    [InlineData("@version:2\n@scenario:test\n@dialog:001[角色]\n译文", false, "缺少条目")]
    [InlineData("@version:2\n@scenario:test\n@dialog:002[角色]\n译文", false, "未知编号")]
    [InlineData("@version:2\n@scenario:test\n@dialog:001", false, "第 3 行")]
    public void ChecksLegacyAndV2UsingRecognitionRules(string content, bool valid, string problem)
    {
        File.WriteAllText(_translation, content);
        var result = SubtitleMaterialValidation.Check(_script, _translation);
        Assert.Equal(valid, result.CanUse);
        Assert.False(result.IsPending);
        Assert.Contains(problem, result.Details);
    }

    [Theory]
    [InlineData("not json", "剧本文件无法读取或解析")]
    [InlineData("{}", "不包含可识别")]
    [InlineData("{\"TalkData\":[{\"Body\":\"text\"}]}", "不包含可识别")]
    [InlineData("{\"TalkData\":[{},{}],\"Snippets\":[{\"Action\":1}]}", "数量不一致")]
    public void ExplainsUnusableScripts(string json, string problem)
    {
        File.WriteAllText(_script, json);
        File.WriteAllText(_translation, "角色：译文");
        var result = SubtitleMaterialValidation.Check(_script, _translation);
        Assert.False(result.CanUse);
        Assert.Contains(problem, result.Details);
    }

    [Fact]
    public void PendingAndMissingFilesAreDistinctAndRechecksDiskChanges()
    {
        Assert.True(SubtitleMaterialValidation.Check(_script, "").IsPending);
        File.WriteAllText(_translation, "角色：译文\n横幅");
        Assert.True(SubtitleMaterialValidation.Check(_script, _translation).CanUse);
        File.WriteAllText(_translation, "角色：译文");
        Assert.False(SubtitleMaterialValidation.Check(_script, _translation).CanUse);
        File.Delete(_translation);
        Assert.Contains("翻译文件无法读取", SubtitleMaterialValidation.Check(_script, _translation).Details);
    }

    public void Dispose() => Directory.Delete(_directory, true);
}
