using System.Text.Json;
using SekaiToolsCore.Process.Config;

namespace SekaiTools.Tests;

public class SpeakerExportOptionsTests
{
    [Fact]
    public void DefaultsOnlyExportChangedSpeakers()
    {
        var options = new ExportStyleConfig();
        Assert.True(options.ExportCharacter);
        Assert.False(options.ExportUnchangedCharacter);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OldJsonPreservesExistingSpeakerChoice(bool enabled)
    {
        var options = JsonSerializer.Deserialize<ExportStyleConfig>("{\"ExportCharacter\":" + enabled.ToString().ToLowerInvariant() + "}");
        Assert.Equal(enabled, options.ExportCharacter);
        Assert.False(options.ExportUnchangedCharacter);
        var copy = JsonSerializer.Deserialize<ExportStyleConfig>(JsonSerializer.Serialize(
            new ExportStyleConfig { ExportCharacter = enabled, ExportUnchangedCharacter = true }));
        Assert.Equal(enabled, copy.ExportCharacter);
        Assert.True(copy.ExportUnchangedCharacter);
    }
}
