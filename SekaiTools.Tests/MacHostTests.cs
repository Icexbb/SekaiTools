using System.Text.Json;
using SekaiToolsBase.GameScript;
using SekaiToolsHost;

namespace SekaiTools.Tests;

public sealed class MacHostTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "SekaiToolsHostTests", Guid.NewGuid().ToString("N"));
    private string ScriptPath => Path.Combine(_directory, "script.json");

    public MacHostTests()
    {
        Directory.CreateDirectory(_directory);
        var script = new GameScript
        {
            ScenarioId = "host-test",
            TalkData = [new Talk("原名", "原文", 0)],
            SpecialEffectData = [new SpecialEffect(8, "横幅", "", 0, 0)],
            Snippets = [new Snippet { Action = 6 }, new Snippet { Action = 1 }]
        };
        File.WriteAllText(ScriptPath, JsonSerializer.Serialize(script));
    }

    [Fact]
    public void EditorRoundtripPreservesExplicitEmptyAndMultilineV2Content()
    {
        var rows = TranslationService.Load(new TranslationLoad(ScriptPath));
        rows[0] = rows[0] with { Content = "@横幅\n#第二行" };
        rows[1] = rows[1] with { Speaker = "", Content = "" };
        var output = Path.Combine(_directory, "translation.txt");
        Protocol.AtomicWrite(output, TranslationService.Export(new TranslationSave(ScriptPath, output, rows)));
        var loaded = TranslationService.Load(new TranslationLoad(ScriptPath, output));
        Assert.Equal(rows, loaded);
        Assert.StartsWith("@version:2\n@scenario:host-test", File.ReadAllText(output));
    }

    [Fact]
    public void ExportRejectsRowsReorderedOrFromAnotherScript()
    {
        var rows = TranslationService.Load(new TranslationLoad(ScriptPath));
        var output = Path.Combine(_directory, "translation.txt");
        File.WriteAllText(output, "original");
        Assert.Throws<InvalidDataException>(() => TranslationService.Export(new TranslationSave(ScriptPath, output, rows.Reverse().ToArray())));
        rows[0] = rows[0] with { Original = "其他剧本" };
        Assert.Throws<InvalidDataException>(() => TranslationService.Export(new TranslationSave(ScriptPath, output, rows)));
        Assert.Equal("original", File.ReadAllText(output));
    }

    [Theory]
    [InlineData("{\"version\":2,\"command\":\"capabilities\",\"args\":{}}")]
    [InlineData("{\"version\":1,\"command\":\"\",\"args\":{}}")]
    public void ProtocolRejectsUnsupportedRequests(string json) => Assert.Throws<InvalidDataException>(() => Protocol.Parse(json));

    [Fact]
    public void AtomicWriteFailureLeavesNoTemporaryFiles()
    {
        var destination = Path.Combine(_directory, "existing-directory");
        Directory.CreateDirectory(destination);
        var error = Record.Exception(() => Protocol.AtomicWrite(destination, "new content"));
        Assert.True(error is IOException or UnauthorizedAccessException, error?.ToString());
        Assert.Empty(Directory.EnumerateFiles(_directory, "*.tmp"));
    }

    [Fact]
    public void OutputCannotReplaceInputMaterial()
    {
        Assert.Throws<InvalidDataException>(() => Protocol.RejectInputOverwrite(ScriptPath, ScriptPath));
        Protocol.RejectInputOverwrite(Path.Combine(_directory, "output.ass"), ScriptPath, "");
    }

    public void Dispose() => Directory.Delete(_directory, true);
}
