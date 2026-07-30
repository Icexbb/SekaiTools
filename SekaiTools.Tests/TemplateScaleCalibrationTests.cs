using SekaiToolsCore.Match.TemplateMatcher;

namespace SekaiTools.Tests;

public class TemplateScaleCalibrationTests
{
    [Fact]
    public void Observe_LocksHighConfidenceScale()
    {
        var calibration = new TemplateScaleCalibration();

        calibration.Observe(1.04, 0.9);

        Assert.Equal([1.04], calibration.CandidateScales);
    }

    [Fact]
    public void Observe_ReopensCalibrationAfterSustainedLowConfidence()
    {
        var calibration = new TemplateScaleCalibration();
        calibration.Observe(0.96, 0.9);

        for (var i = 0; i < 30; i++)
            calibration.Observe(0.96, 0.1);

        Assert.Equal([1.00, 0.96, 1.04], calibration.CandidateScales);
    }
}
