using SekaiToolsCore.Process;
using SekaiToolsCore.Process.Config;

namespace SekaiTools.Tests;

public class ProcessingStateCompatibilityTests
{
    [Theory]
    [InlineData(null, "periodic:runtime-a:plugin-a:16:12", false)]
    [InlineData("periodic:runtime-a:plugin-a:16:12", null, false)]
    [InlineData("periodic:runtime-a:plugin-a:16:12", "periodic:runtime-b:plugin-a:16:12", false)]
    [InlineData("periodic:runtime-a:plugin-a:16:12", "periodic:runtime-a:plugin-a:16:10", false)]
    [InlineData("periodic:runtime-a:plugin-a:16:12", "periodic:runtime-a:plugin-a:16:12", true)]
    [InlineData(null, null, true)]
    public void ValidateAndMigrate_RequiresMatchingCaptureIdentity(string? saved, string? current, bool compatible)
    {
        WithInputs((config, metadata) =>
        {
            var state = CreateState(config, metadata with { CaptureIdentity = saved });
            state = System.Text.Json.JsonSerializer.Deserialize<ProcessingState>(
                System.Text.Json.JsonSerializer.Serialize(state))!;
            Assert.Equal(saved, state.Metadata!.CaptureIdentity);
            var result = ProcessingStateCompatibility.ValidateAndMigrate(state, config,
                metadata with { CaptureIdentity = current });
            Assert.Equal(compatible, result.CanRestore);
        });
    }

    [Fact]
    public void ValidateAndMigrate_RejectsLegacyAlternateCaptureWithoutMutatingState()
    {
        WithInputs((config, metadata) =>
        {
            var state = CreateState(config, metadata);
            state.Version = "1.0";
            state.Metadata = null;
            var result = ProcessingStateCompatibility.ValidateAndMigrate(state, config,
                metadata with { CaptureIdentity = "periodic:runtime-a:plugin-a:16:12" });
            Assert.False(result.CanRestore);
            Assert.Equal("1.0", state.Version);
            Assert.Null(state.Metadata);
        });
    }

    [Fact]
    public void ValidateAndMigrate_RejectsChangedInputFingerprint()
    {
        WithInputs((config, metadata) =>
        {
            var state = CreateState(config, metadata);
            File.AppendAllText(config.ScriptFilePath, "changed");
            var changedMetadata = ProcessingStateMetadata.Create(config, metadata.VideoInfo);

            var result = ProcessingStateCompatibility.ValidateAndMigrate(state, config, changedMetadata);

            Assert.Equal(ProcessingStateCompatibilityStatus.Incompatible, result.Status);
        });
    }

    [Fact]
    public void ValidateAndMigrate_RejectsChangedConfig()
    {
        WithInputs((config, metadata) =>
        {
            var state = CreateState(config, metadata);
            var changedConfig = new Config(config.VideoFilePath, config.ScriptFilePath, "",
                matchingThreshold: new MatchingThreshold { BannerNormal = 0.6 });
            var changedMetadata = ProcessingStateMetadata.Create(changedConfig, metadata.VideoInfo);

            var result = ProcessingStateCompatibility.ValidateAndMigrate(state, changedConfig, changedMetadata);

            Assert.Equal(ProcessingStateCompatibilityStatus.Incompatible, result.Status);
        });
    }

    [Fact]
    public void ValidateAndMigrate_MigratesLegacyStateWithMatchingPaths()
    {
        WithInputs((config, metadata) =>
        {
            var state = new ProcessingState
            {
                Version = "1.0",
                VideoFilePath = config.VideoFilePath,
                ScriptFilePath = config.ScriptFilePath,
                TranslateFilePath = ""
            };

            var result = ProcessingStateCompatibility.ValidateAndMigrate(state, config, metadata);

            Assert.Equal(ProcessingStateCompatibilityStatus.MigratedLegacy, result.Status);
            Assert.Equal(ProcessingStateCompatibility.CurrentVersion, state.Version);
            Assert.Equal(metadata, state.Metadata);
        });
    }

    private static ProcessingState CreateState(Config config, ProcessingStateMetadata metadata)
    {
        return new ProcessingState
        {
            VideoFilePath = config.VideoFilePath,
            ScriptFilePath = config.ScriptFilePath,
            TranslateFilePath = "",
            Metadata = metadata
        };
    }

    private static void WithInputs(Action<Config, ProcessingStateMetadata> test)
    {
        var videoPath = Path.GetTempFileName();
        var scriptPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(videoPath, "video");
            File.WriteAllText(scriptPath, "script");
            var config = new Config(videoPath, scriptPath, "");
            var metadata = ProcessingStateMetadata.Create(config,
                new VideoStateMetadata(1920, 1080, 1000, 30));
            test(config, metadata);
        }
        finally
        {
            File.Delete(videoPath);
            File.Delete(scriptPath);
        }
    }
}
