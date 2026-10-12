using SekaiToolsCore.Match.TemplateMatcher;
using SekaiToolsCore.Process;
using System.Text.Json;

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

    [Fact]
    public void SerializedCheckpointPreservesScaleAndPendingUnlock()
    {
        var continuous = new TemplateScaleCalibration();
        continuous.Observe(0.96, 0.9);
        for (var index = 0; index < 29; index++) continuous.Observe(0.96, 0.1);
        var state = JsonSerializer.Deserialize<TemplateScaleStateDto>(JsonSerializer.Serialize(continuous.CaptureState(3)))!;
        var resumed = new TemplateScaleCalibration();
        resumed.RestoreState(state);
        Assert.Equal(continuous.CandidateScales, resumed.CandidateScales);
        continuous.Observe(0.96, 0.1);
        resumed.Observe(0.96, 0.1);
        Assert.Equal(continuous.CandidateScales, resumed.CandidateScales);
        Assert.Equal(3, resumed.CandidateScales.Count);
    }

    [Fact]
    public void InvalidScaleCheckpointDoesNotReplaceExistingCalibration()
    {
        var pool = new TemplateMatchCachePool();
        pool.ObserveScale(TemplateMatchCachePool.MatchUsage.DialogNameTag, 1.04, 0.9);
        var states = pool.CaptureScaleState();
        states[2] = new TemplateScaleStateDto(2, 1.5, 0);
        Assert.Throws<InvalidDataException>(() => pool.RestoreScaleState(states));
        Assert.Equal([1.04], pool.GetCandidateScales(TemplateMatchCachePool.MatchUsage.DialogNameTag));
        pool.RestoreScaleState(null);
        Assert.Equal(3, pool.GetCandidateScales(TemplateMatchCachePool.MatchUsage.DialogNameTag).Count);
    }
}
