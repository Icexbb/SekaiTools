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
        Assert.Equal(37, rules.MaxLineLength);
        Assert.Equal(45, rules.ThreeLineMaxLength);
        Assert.Equal("你好？！", rules.Format("你好?!"));
        Assert.Empty(rules.Check("「你好？！」"));
        Assert.Contains("省略号", rules.Check("你好...。"));
        Assert.Contains("破折号", rules.Check("——你好—！"));
    }

    [Fact]
    public void ExternalFileChangesRulesReplacementsAndThresholds()
    {
        var rules = LoadModified(json =>
        {
            json["ValidEndCharacters"] = "♡";
            json["ClosingCharacters"] = "]";
            json["MissingEndMessage"] = "自定义句尾提示";
            json["Replacements"] = new JsonArray(new JsonObject { ["From"] = "!", ["To"] = "♡" });
            json["MaxLineLength"] = 20;
            json["ThreeLineMaxLength"] = 30;
        });
        Assert.Equal("你好♡", rules.Format("你好!"));
        Assert.Empty(rules.Check("[你好♡]"));
        Assert.Contains("自定义句尾提示", rules.Check("你好！"));
        Assert.Equal(20, rules.MaxLineLength);
        Assert.Equal(30, rules.ThreeLineMaxLength);
    }

    [Fact]
    public void ChecksAndFormattingCanBeDisabled()
    {
        var rules = LoadModified(json =>
        {
            json["CheckEndPunctuation"] = false;
            json["CheckEllipsisPunctuation"] = false;
            json["CheckDash"] = false;
            json["CheckLineLength"] = false;
            json["Replacements"] = new JsonArray();
        });
        Assert.Empty(rules.Check("无标点\n省略号...。\n——混合—！"));
        Assert.Equal("你好!", rules.Format("你好!"));
        Assert.False(rules.CheckLineLength);
    }

    [Theory]
    [InlineData("MaxLineLength", 0)]
    [InlineData("ThreeLineMaxLength", -1)]
    [InlineData("DashGroupLength", 0)]
    public void InvalidThresholdIsRejected(string property, int value)
    {
        Assert.Throws<InvalidDataException>(() => LoadModified(json => json[property] = value));
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
