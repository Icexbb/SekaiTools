using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using SekaiToolsCore.Process.Model;

namespace SekaiToolsCore.Match.TemplateMatcher;

/// <summary>
///     单个视频处理任务内的模板匹配结果缓存。
///     只复用同一帧、同一图像对象、同一模板对象和同一匹配算法的结果。
/// </summary>
public sealed class TemplateMatchCachePool
{
    public enum MatchUsage
    {
        ContentStartSign = 0,
        Banner = 1,
        DialogNameTag = 2,
        DialogContent1 = 3,
        DialogContent2 = 4,
        DialogContent3 = 5,
        Marker = 6,
        Misc = 7
    }

    private readonly MatchCacheEntry[] _entries =
        new MatchCacheEntry[(int)MatchUsage.Misc + 1];
    private readonly TemplateScaleCalibration[] _scaleCalibrations =
        Enumerable.Range(0, (int)MatchUsage.Misc + 1)
            .Select(_ => new TemplateScaleCalibration())
            .ToArray();

    private int _currentFrameIndex = -1;

    public void SetFrameIndex(int frameIndex)
    {
        _currentFrameIndex = frameIndex;
    }

    public bool TryGet(
        MatchUsage usage,
        Mat image,
        Rectangle region,
        GaMat template,
        TemplateMatchingType matchingType,
        out TemplateMatchResult result)
    {
        var entry = _entries[(int)usage];
        if (entry.FrameIndex == _currentFrameIndex &&
            ReferenceEquals(entry.Image, image) &&
            entry.Region == region &&
            ReferenceEquals(entry.Template, template) &&
            entry.MatchingType == matchingType)
        {
            result = entry.Result;
            return true;
        }

        result = default;
        return false;
    }

    public void RegisterResult(
        MatchUsage usage,
        Mat image,
        Rectangle region,
        GaMat template,
        TemplateMatchingType matchingType,
        TemplateMatchResult result)
    {
        _entries[(int)usage] = new MatchCacheEntry(
            _currentFrameIndex,
            image,
            region,
            template,
            matchingType,
            result);
    }

    internal IReadOnlyList<double> GetCandidateScales(MatchUsage usage)
    {
        return _scaleCalibrations[(int)usage].CandidateScales;
    }

    internal void ObserveScale(MatchUsage usage, double scale, double score)
    {
        _scaleCalibrations[(int)usage].Observe(scale, score);
    }

    public void NextDialog()
    {
        Reset(MatchUsage.DialogNameTag);
        Reset(MatchUsage.DialogContent1);
        Reset(MatchUsage.DialogContent2);
        Reset(MatchUsage.DialogContent3);
    }

    public void ResetAll()
    {
        Array.Clear(_entries);
        foreach (var calibration in _scaleCalibrations)
            calibration.Reset();
        _currentFrameIndex = -1;
    }

    private void Reset(MatchUsage usage)
    {
        _entries[(int)usage] = default;
    }

    private readonly record struct MatchCacheEntry(
        int FrameIndex,
        Mat? Image,
        Rectangle Region,
        GaMat? Template,
        TemplateMatchingType MatchingType,
        TemplateMatchResult Result);
}
