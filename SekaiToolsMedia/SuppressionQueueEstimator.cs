namespace SekaiToolsMedia;

public sealed record SuppressionTimingSample(VideoSuppressionOptions Options, VideoSuppressionProgress Progress);

public static class SuppressionQueueEstimator
{
    public static TimeSpan? EstimateRemaining(SuppressionTimingSample? running,
        IReadOnlyList<VideoSuppressionOptions> pending, IReadOnlyList<SuppressionTimingSample> completed)
    {
        if (running == null && pending.Count == 0) return TimeSpan.Zero;
        double seconds = 0;
        double? CurrentRate(VideoSuppressionOptions target)
        {
            if (running is { Progress.State: VideoSuppressionState.Running } && Compatible(running.Options, target) &&
                running.Progress.ElapsedSeconds >= 5 && running.Progress.ProcessedFrames >= 30 &&
                double.IsFinite(running.Progress.FramesPerSecond) && running.Progress.FramesPerSecond > 0)
                return running.Progress.FramesPerSecond;
            foreach (var sample in completed.Reverse())
            {
                if (sample.Progress.State != VideoSuppressionState.Completed || !Compatible(sample.Options, target)) continue;
                var frames = sample.Progress.TotalFrames;
                var elapsed = sample.Progress.ElapsedSeconds;
                if (frames > 0 && double.IsFinite(elapsed) && elapsed >= 5) return frames / elapsed;
            }
            return null;
        }
        if (running != null)
        {
            var progress = running.Progress;
            if (progress.State != VideoSuppressionState.Running || progress.IsFinalizing) return null;
            var rate = CurrentRate(running.Options);
            if (rate == null || progress.TotalFrames <= 0) return null;
            seconds += Math.Max(0, (double)progress.TotalFrames - progress.ProcessedFrames) / rate.Value;
        }
        foreach (var options in pending)
        {
            var rate = CurrentRate(options);
            if (rate == null || options.SourceFrameCount <= 0) return null;
            seconds += options.SourceFrameCount / rate.Value;
        }
        return double.IsFinite(seconds) && seconds <= TimeSpan.MaxValue.TotalSeconds - 1
            ? TimeSpan.FromSeconds(Math.Ceiling(seconds)) : null;
    }

    private static bool Compatible(VideoSuppressionOptions first, VideoSuppressionOptions second) =>
        first.SourceHeight > 0 && first.SourceHeight == second.SourceHeight &&
        first.EncodingSettings.Crf == second.EncodingSettings.Crf &&
        first.EncodingSettings.FfmpegPreset == second.EncodingSettings.FfmpegPreset &&
        first.EncodingSettings.Threads == second.EncodingSettings.Threads &&
        string.IsNullOrWhiteSpace(first.SourceSubtitle) == string.IsNullOrWhiteSpace(second.SourceSubtitle);
}
