using System.Text.Json;
using System.Text.Json.Nodes;
using SekaiToolsConfiguration;

namespace SekaiTools.Tests;

public class TranslationRulesTests
{
    [Fact]
    public void MissingFileUsesEmbeddedDefaults()
    {
        var rules = TranslationRules.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"));
        Assert.Equal("你好？！", rules.Format("你好?!"));
        Assert.Empty(rules.Check("「你好？！」"));
        Assert.Contains("省略号", rules.Check("你好...。"));
        Assert.Contains("破折号", rules.Check("——你好—！"));
    }

    [Fact]
    public void NewRegexCheckIncludesItsOwnConditionsAndFeedback()
    {
        var rules = LoadModified(json =>
        {
            json["Checks"] = new JsonArray(new JsonObject
            {
                ["Id"] = "custom", ["Scope"] = "line",
                ["Conditions"] = new JsonArray(new JsonObject { ["Pattern"] = "重复(.+)重复" }),
                ["Feedback"] = new JsonObject { ["Message"] = "自定义提示" }
            });
            json["Replacements"] = new JsonArray(new JsonObject { ["Pattern"] = "([A-Z]+)!", ["Replacement"] = "$1♡" });
        });
        Assert.Equal("ABC♡", rules.Format("ABC!"));
        Assert.Equal("行2:【自定义提示】", rules.Check("普通文本\n重复内容重复"));
        Assert.Empty(rules.Check("普通文本"));
    }

    [Fact]
    public void DisabledRulesDoNotCheckOrReplace()
    {
        var rules = LoadModified(json =>
        {
            foreach (var check in json["Checks"]!.AsArray()) check!["Enabled"] = false;
            foreach (var replacement in json["Replacements"]!.AsArray()) replacement!["Enabled"] = false;
        });
        Assert.Empty(rules.Check("无标点\n省略号...。\n——混合—！"));
        Assert.Equal("你好!", rules.Format("你好!"));
    }

    [Theory]
    [InlineData(2, 37, false)]
    [InlineData(2, 38, true)]
    [InlineData(3, 45, false)]
    [InlineData(3, 46, true)]
    [InlineData(2, 100, true)]
    public void LengthRulesUseGenericContextConditions(int originalLines, int length, bool expected)
    {
        var results = TranslationRules.Current.Evaluate("你好！", new Dictionary<string, string>
        {
            ["originalLineCount"] = originalLines.ToString(), ["maxLineLength"] = length.ToString()
        });
        Assert.Equal(expected, results.Any(result => result.Feedback.Tags.Contains("length")));
    }

    [Theory]
    [InlineData("——你好——！", false)]
    [InlineData("——你好—！", true)]
    [InlineData("———你好！", true)]
    [InlineData("—你好！", false)]
    public void DashRegexPreservesExistingChecks(string text, bool expected)
    {
        Assert.Equal(expected, TranslationRules.Current.Check(text).Contains("破折号"));
    }

    [Fact]
    public void InvalidRegexReportsRuleId()
    {
        var exception = Assert.Throws<InvalidDataException>(() => LoadModified(json =>
            json["Checks"]![0]!["Conditions"]![0]!["Pattern"] = "["));
        Assert.Contains("missing-end-punctuation", exception.Message);
    }

    [Fact]
    public void DuplicateCheckIdIsRejected()
    {
        Assert.Throws<InvalidDataException>(() => LoadModified(json =>
            json["Checks"]![1]!["Id"] = json["Checks"]![0]!["Id"]!.GetValue<string>()));
    }

    [Fact]
    public void AbsentContextDoesNotTriggerNegativeConditions()
    {
        Assert.DoesNotContain(TranslationRules.Current.Evaluate("你好！"), result => result.Feedback.Tags.Contains("length"));
    }

    private static TranslationRules LoadModified(Action<JsonObject> modify)
    {
        var json = JsonSerializer.SerializeToNode(TranslationRules.Current)!.AsObject();
        modify(json);
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, json.ToJsonString());
            return TranslationRules.Load(path);
        }
        finally { File.Delete(path); }
    }
}
