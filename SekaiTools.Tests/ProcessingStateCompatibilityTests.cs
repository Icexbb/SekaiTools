using SekaiToolsCore.Process;
using SekaiToolsCore.Process.Config;

namespace SekaiTools.Tests;

public class ProcessingStateCompatibilityTests
{
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
