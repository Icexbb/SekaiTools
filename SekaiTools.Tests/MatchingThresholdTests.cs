using SekaiToolsCore.Process.Config;

namespace SekaiTools.Tests;

public class MatchingThresholdTests
{
    [Fact]
    public void Config_UsesStandardThresholdsWhenNotSpecified()
    {
        WithTemporaryInputs((videoPath, scriptPath) =>
        {
            var config = new Config(videoPath, scriptPath, "");

            Assert.Equal(0.80, config.MatchingThreshold.DialogNametagNormal);
            Assert.Equal(0.80, config.MatchingThreshold.DialogNametagSpecial);
            Assert.Equal(0.80, config.MatchingThreshold.DialogContentNormal);
            Assert.Equal(0.80, config.MatchingThreshold.DialogContentSpecial);
            Assert.Equal(0.75, config.MatchingThreshold.BannerNormal);
            Assert.Equal(0.75, config.MatchingThreshold.MarkerNormal);
        });
    }

    [Fact]
    public void Config_PreservesExplicitThresholds()
    {
        WithTemporaryInputs((videoPath, scriptPath) =>
        {
            var thresholds = new MatchingThreshold
            {
                DialogNametagNormal = 0.91,
                DialogNametagSpecial = 0.82,
                DialogContentNormal = 0.73,
                DialogContentSpecial = 0.64,
                BannerNormal = 0.55,
                MarkerNormal = 0.46
            };

            var config = new Config(videoPath, scriptPath, "", matchingThreshold: thresholds);

            Assert.Equal(thresholds, config.MatchingThreshold);
        });
    }

    private static void WithTemporaryInputs(Action<string, string> test)
    {
        var videoPath = Path.GetTempFileName();
        var scriptPath = Path.GetTempFileName();
        try
        {
            test(videoPath, scriptPath);
        }
        finally
        {
            File.Delete(videoPath);
            File.Delete(scriptPath);
        }
    }
}
