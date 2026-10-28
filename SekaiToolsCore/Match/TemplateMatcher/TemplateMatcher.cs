using System.Drawing;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using SekaiToolsCore.Process.Model;
using SekaiToolsCore.Utils;
using SekaiToolsCore.Process.Performance;

namespace SekaiToolsCore.Match.TemplateMatcher;

public static class TemplateMatcher
{
    private const int SearchDownscaleDivisor = 2;
    private const int MinTemplateDimAfterScale = 8;
    private const int RefinementMargin = 4;
    private static readonly Inter SearchInterpolation = Inter.Area;

    public static TemplateMatchResult Match(FrameMatchContext frame, Rectangle region, GaMat tmp,
        TemplateMatchCachePool cachePool,
        TemplateMatchCachePool.MatchUsage usage = TemplateMatchCachePool.MatchUsage.Misc,
        TemplateMatchingType matchingType = TemplateMatchingType.CcoeffNormed,
        [CallerMemberName] string memberName = "")
        => MatchCore(frame, region, tmp, cachePool, usage, matchingType, memberName, null);

    internal static TemplateMatchResult MatchContent(FrameMatchContext frame, Rectangle region, GaMat template,
        TemplateMatchCachePool pool, TemplateMatchCachePool.MatchUsage usage, double threshold,
        [CallerMemberName] string memberName = "", bool allowScaleRecovery = false) =>
        MatchCore(frame, region, template, pool, usage, TemplateMatchingType.CcoeffNormed, memberName, threshold, allowScaleRecovery);

    private static TemplateMatchResult MatchCore(FrameMatchContext frame, Rectangle region, GaMat tmp,
        TemplateMatchCachePool cachePool, TemplateMatchCachePool.MatchUsage usage,
        TemplateMatchingType matchingType, string memberName, double? acceptanceThreshold, bool allowScaleRecovery = false)
    {
        var metrics = cachePool.Metrics.Enabled ? cachePool.Metrics : null;
        using var measurement = metrics?.Enter(metrics.CurrentState, usage) ?? default;
        var started = metrics != null ? Stopwatch.GetTimestamp() : 0;
        frame.Metrics = metrics;
        if (cachePool.TryGet(usage, frame.Gray, region, tmp, matchingType, out var cachedResult, acceptanceThreshold) &&
            !(allowScaleRecovery && acceptanceThreshold is { } cachedThreshold &&
              !cachedResult.IsMatch(cachedThreshold) && cachePool.GetCandidateScales(usage).Count == 1))
        {
            metrics?.RecordRequest(usage, true, started);
            return cachedResult;
        }

        var res = new TemplateMatchResult(double.NegativeInfinity, double.PositiveInfinity,
            Point.Empty, Point.Empty);
        var scales = cachePool.GetCandidateScales(usage);
        if (acceptanceThreshold is { } threshold && double.IsFinite(threshold) && scales.Count == 1
            && cachePool.TryGetContentPosition(usage, region, tmp, scales[0], out var previousPosition))
        {
            var size = tmp.GetScaledLayer(scales[0], 1).Size;
            var probe = Rectangle.Intersect(region, new Rectangle(
                region.X + previousPosition.X - 8, region.Y + previousPosition.Y - 8,
                size.Width + 16, size.Height + 16));
            if (probe != region && probe.Width >= size.Width && probe.Height >= size.Height)
            {
                var candidate = MatchNoCache(frame, probe, tmp, scales[0], matchingType, memberName, acceptanceThreshold);
                // This path is used only for a presence decision, never for name-tag position tracking.
                // Weak/absent evidence falls through to the unchanged complete search.
                if (candidate.IsMatch(Math.Max(0.85, threshold + 0.02)))
                {
                    var offset = new Size(probe.X - region.X, probe.Y - region.Y);
                    candidate.MaxLoc += offset;
                    candidate.MinLoc += offset;
                    cachePool.ObserveScale(usage, candidate.Scale, candidate.MaxVal);
                    cachePool.RegisterResult(usage, frame.Gray, region, tmp, matchingType, candidate, acceptanceThreshold);
                    metrics?.RecordRequest(usage, false, started);
                    return candidate;
                }
            }
        }
        for (var index = 0; index < scales.Count; index++)
        {
            var scale = scales[index];
            var candidate = MatchNoCache(frame, region, tmp, scale, matchingType, memberName, acceptanceThreshold);
            if (candidate.MaxVal > res.MaxVal)
                res = candidate;
        }

        // A locked scale can fail tracking without reaching the much lower global
        // unlock threshold. Recheck only the other two supported scales, and move
        // the lock only on strong evidence; never relax the acceptance threshold.
        if (allowScaleRecovery && acceptanceThreshold is { } recoveryThreshold &&
            scales.Count == 1 && !res.IsMatch(recoveryThreshold))
        {
            var lockedScale = scales[0];
            var recovered = res;
            foreach (var scale in TemplateScaleCalibration.RecoveryScales)
            {
                if (scale == lockedScale) continue;
                var candidate = MatchNoCache(frame, region, tmp, scale, matchingType, memberName, acceptanceThreshold);
                if (candidate.MaxVal > recovered.MaxVal) recovered = candidate;
            }
            if (cachePool.TryRecoverScale(usage, recovered.Scale, recovered.MaxVal, recoveryThreshold))
                res = recovered;
        }

        cachePool.ObserveScale(usage, res.Scale, res.MaxVal);
        cachePool.RegisterResult(usage, frame.Gray, region, tmp, matchingType, res, acceptanceThreshold);
        metrics?.RecordRequest(usage, false, started);

        return res;
    }

    private static TemplateMatchResult MatchNoCache(FrameMatchContext frame, Rectangle region, GaMat tmp,
        double scale,
        TemplateMatchingType matchingType = TemplateMatchingType.CcoeffNormed,
        string memberName = "", double? acceptanceThreshold = null)
    {
        var templateLayer = tmp.GetScaledLayer(scale, 1);
        if (templateLayer.Size.Width > region.Width || templateLayer.Size.Height > region.Height)
            return new TemplateMatchResult(double.NegativeInfinity, double.PositiveInfinity,
                Point.Empty, Point.Empty)
            { Scale = scale };

        if (templateLayer.Size.Width / SearchDownscaleDivisor >= MinTemplateDimAfterScale
            && templateLayer.Size.Height / SearchDownscaleDivisor >= MinTemplateDimAfterScale)
            return MatchNoCacheScaled(frame, region, tmp, templateLayer, scale, matchingType, memberName, acceptanceThreshold);

        using var image = frame.CreateGrayRoi(region);
        return MatchNoCacheFull(frame, image, templateLayer, scale, matchingType, memberName);
    }

    private static TemplateMatchResult MatchNoCacheScaled(FrameMatchContext frame, Rectangle region, GaMat tmp,
        GaMatLayer fullTemplateLayer, double scale, TemplateMatchingType matchingType, string memberName,
        double? acceptanceThreshold)
    {
        var imgSmall = frame.GetScaledGrayRoi(region, SearchDownscaleDivisor, SearchInterpolation);
        var templateLayer = tmp.GetScaledLayer(scale, SearchDownscaleDivisor);

        using var resultLease = frame.RentMatchResult(imgSmall.Size, templateLayer.Size);
        var matchResult = resultLease.Image;
        double maxVal = 0, minVal = 0;
        Point minLoc = new(), maxLoc = new();
        MatchAndLocate(frame, imgSmall, templateLayer, matchResult, matchingType,
            ref minVal, ref maxVal, ref minLoc, ref maxLoc);

        var coarsePeak = maxLoc;
        var coarseMaxLoc = new Point(
            maxLoc.X * SearchDownscaleDivisor + SearchDownscaleDivisor / 2,
            maxLoc.Y * SearchDownscaleDivisor + SearchDownscaleDivisor / 2);
        var coarseMinLoc = new Point(
            minLoc.X * SearchDownscaleDivisor + SearchDownscaleDivisor / 2,
            minLoc.Y * SearchDownscaleDivisor + SearchDownscaleDivisor / 2);

        var refinementRegion = new Rectangle(
            coarseMaxLoc.X - RefinementMargin,
            coarseMaxLoc.Y - RefinementMargin,
            fullTemplateLayer.Size.Width + RefinementMargin * 2,
            fullTemplateLayer.Size.Height + RefinementMargin * 2);
        refinementRegion = Rectangle.Intersect(refinementRegion, new Rectangle(Point.Empty, region.Size));

        if (refinementRegion.Width >= fullTemplateLayer.Size.Width &&
            refinementRegion.Height >= fullTemplateLayer.Size.Height)
        {
            var frameRefinementRegion = new Rectangle(
                region.X + refinementRegion.X,
                region.Y + refinementRegion.Y,
                refinementRegion.Width,
                refinementRegion.Height);
            using var refinementImage = frame.CreateGrayRoi(frameRefinementRegion);
            using var refinementLease = frame.RentMatchResult(refinementImage.Size, fullTemplateLayer.Size, true);
            var refinementResult = refinementLease.Image;
            MatchAndLocate(frame, refinementImage, fullTemplateLayer, refinementResult, matchingType,
                ref minVal, ref maxVal, ref minLoc, ref maxLoc);
            maxLoc += new Size(refinementRegion.Location);
            minLoc += new Size(refinementRegion.Location);
        }
        else
        {
            maxLoc = coarseMaxLoc;
            minLoc = coarseMinLoc;
        }

        var best = new TemplateMatchResult(maxVal, minVal, maxLoc, minLoc) { Scale = scale };
        // Only presence requests have an acceptance threshold. Keep ordinary position
        // tracking unchanged, and pay for alternate peaks only after refinement fails.
        if (acceptanceThreshold is { } threshold && double.IsFinite(threshold) && !best.IsMatch(threshold))
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var suppression = Rectangle.Intersect(new Rectangle(coarsePeak.X - 2, coarsePeak.Y - 2, 5, 5),
                    new Rectangle(Point.Empty, matchResult.Size));
                using (var suppressed = new Mat(matchResult, suppression))
                    suppressed.SetTo(new MCvScalar(-float.MaxValue));
                double nextMax = 0, nextMin = 0;
                Point nextMinLoc = new();
                var started = frame.Metrics != null ? Stopwatch.GetTimestamp() : 0;
                CvInvoke.MinMaxLoc(matchResult, ref nextMin, ref nextMax, ref nextMinLoc, ref coarsePeak);
                frame.Metrics?.Record(TemplateMatchingStage.Extremum, started);
                if (!double.IsFinite(nextMax) || nextMax <= -float.MaxValue) break;
                var candidateRegion = Rectangle.Intersect(new Rectangle(
                    coarsePeak.X * SearchDownscaleDivisor + SearchDownscaleDivisor / 2 - RefinementMargin,
                    coarsePeak.Y * SearchDownscaleDivisor + SearchDownscaleDivisor / 2 - RefinementMargin,
                    fullTemplateLayer.Size.Width + RefinementMargin * 2,
                    fullTemplateLayer.Size.Height + RefinementMargin * 2), new Rectangle(Point.Empty, region.Size));
                if (candidateRegion.Width < fullTemplateLayer.Size.Width || candidateRegion.Height < fullTemplateLayer.Size.Height)
                    continue;
                using var candidateImage = frame.CreateGrayRoi(new Rectangle(
                    region.X + candidateRegion.X, region.Y + candidateRegion.Y, candidateRegion.Width, candidateRegion.Height));
                using var candidateLease = frame.RentMatchResult(candidateImage.Size, fullTemplateLayer.Size, true);
                double candidateMax = 0, candidateMin = 0;
                Point candidateMaxLoc = new(), candidateMinLoc = new();
                MatchAndLocate(frame, candidateImage, fullTemplateLayer, candidateLease.Image, matchingType,
                    ref candidateMin, ref candidateMax, ref candidateMinLoc, ref candidateMaxLoc);
                var candidate = new TemplateMatchResult(candidateMax, candidateMin, candidateMaxLoc, candidateMinLoc) { Scale = scale };
                candidate.MaxLoc += new Size(candidateRegion.Location);
                candidate.MinLoc += new Size(candidateRegion.Location);
                // Alternate peaks need strong evidence even when the caller is
                // using a relaxed tracking/fallback threshold.
                if (candidate.IsMatch(Math.Max(0.85, threshold)))
                {
                    best = candidate;
                    break;
                }
            }
        }

        if (ShouldShowImg(memberName))
        {
            using var image = frame.CreateGrayRoi(region);
            ShowImg(image, fullTemplateLayer, best.MaxVal, best.MaxLoc, memberName);
        }
        return best;
    }

    private static TemplateMatchResult MatchNoCacheFull(FrameMatchContext frame, Mat img, GaMatLayer templateLayer, double scale,
        TemplateMatchingType matchingType = TemplateMatchingType.CcoeffNormed,
        string memberName = "")
    {
        using var resultLease = frame.RentMatchResult(img.Size, templateLayer.Size);
        var matchResult = resultLease.Image;
        double maxVal = 0, minVal = 0;
        Point minLoc = new(), maxLoc = new();
        MatchAndLocate(frame, img, templateLayer, matchResult, matchingType,
            ref minVal, ref maxVal, ref minLoc, ref maxLoc);
        if (ShouldShowImg(memberName)) ShowImg(img, templateLayer, maxVal, maxLoc, memberName);
        return new TemplateMatchResult(maxVal, minVal, maxLoc, minLoc) { Scale = scale };
    }

    private static void MatchAndLocate(FrameMatchContext frame, Mat image, GaMatLayer template,
        Mat result, TemplateMatchingType type, ref double min, ref double max, ref Point minLoc, ref Point maxLoc)
    {
        var metrics = frame.Metrics;
        var started = metrics != null ? Stopwatch.GetTimestamp() : 0;
        CvInvoke.MatchTemplate(image, template.Gray, result, type, template.Alpha);
        metrics?.Record(TemplateMatchingStage.Correlation, started);
        started = metrics != null ? Stopwatch.GetTimestamp() : 0;
        result.MatRemoveErrorInf();
        metrics?.Record(TemplateMatchingStage.ScoreCleanup, started);
        started = metrics != null ? Stopwatch.GetTimestamp() : 0;
        CvInvoke.MinMaxLoc(result, ref min, ref max, ref minLoc, ref maxLoc);
        metrics?.Record(TemplateMatchingStage.Extremum, started);
    }

    private static bool ShouldShowImg(string memberName) => Debugger.IsAttached &&
        (Environment.GetEnvironmentVariable("DebugShowImg") ?? "").Contains(memberName);

    private static void ShowImg(Mat img, GaMatLayer tmp, double maxVal, Point maxLoc, string memberName)
    {

        using var show = img.Clone()!;
        CvInvoke.PutText(show, $"MaxVal: {maxVal:0.00}", maxLoc with { Y = maxLoc.Y - 5 },
            HersheyFonts.Simplex, 0.4, new MCvScalar(255));
        CvInvoke.Rectangle(show, new Rectangle(maxLoc, tmp.Size), new MCvScalar(255), 2);


        using var tempGray = tmp.Gray.Clone();
        using var tempAlpha = tmp.Alpha.Clone();
        using var temp = new Mat(tempAlpha.Rows + tempGray.Rows, tempGray.Cols, tempGray.Depth,
            tempGray.NumberOfChannels);
        CvInvoke.VConcat(new VectorOfMat(tempGray, tempAlpha), temp);
        if (temp.Height > show.Height)
        {
            using var emptyMat = new Mat(temp.Rows - show.Rows, show.Cols, show.Depth, show.NumberOfChannels);
            emptyMat.SetTo(new MCvScalar(0));
            CvInvoke.VConcat(new VectorOfMat(emptyMat, show), show);
        }
        else if (temp.Height < show.Height)
        {
            using var emptyMat = new Mat(show.Rows - temp.Rows, temp.Cols, temp.Depth, temp.NumberOfChannels);
            emptyMat.SetTo(new MCvScalar(0));
            CvInvoke.VConcat(new VectorOfMat(emptyMat, temp), temp);
        }

        CvInvoke.HConcat(new VectorOfMat(show, temp), show);


        // var emptyMat = new Mat(show.Rows - tempGray.Rows, tempGray.Cols, tempGray.Depth, tempGray.NumberOfChannels);
        // emptyMat.SetTo(new MCvScalar(0));
        // CvInvoke.VConcat(new VectorOfMat(emptyMat, tempGray), tempGray);
        // CvInvoke.HConcat(new VectorOfMat(show, tempGray), show);
        // tempGray.Dispose();


        CvInvoke.Imshow(memberName, show);
        CvInvoke.WaitKey(Environment.GetEnvironmentVariable("DebugImgWait") == "true" ? 0 : 1);
    }
}
