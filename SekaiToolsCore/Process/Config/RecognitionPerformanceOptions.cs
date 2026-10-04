namespace SekaiToolsCore.Process.Config;

public enum RecognitionPerformanceMode { MemorySaving, Automatic, HighPerformance }

public sealed record RecognitionPerformanceOptions
{
    public RecognitionPerformanceMode Mode { get; init; } = RecognitionPerformanceMode.Automatic;
    public int MemoryBudgetMiB { get; init; } = 2048;
    public bool EnableFramePrefetch { get; init; } = true;

    public int GetFrameCapacity(int width, int height, long availableBytes)
    {
        if (!EnableFramePrefetch || width <= 0 || height <= 0 || availableBytes <= 0) return 0;
        var cap = Mode switch
        {
            RecognitionPerformanceMode.MemorySaving => 128,
            RecognitionPerformanceMode.HighPerformance => Math.Clamp(MemoryBudgetMiB, 128, 4096),
            _ => 512
        };
        // Leave headroom for the existing decoder, matchers, UI and other applications.
        var budget = Math.Min((long)cap * 1024 * 1024, availableBytes / 4);
        var frameBytes = checked((long)width * height * 3);
        var count = Math.Max(0, budget - 64L * 1024 * 1024) / frameBytes;
        var maxFrames = Mode switch
        {
            RecognitionPerformanceMode.MemorySaving => 4,
            RecognitionPerformanceMode.HighPerformance => 32,
            _ => 16
        };
        return count < 2 ? 0 : (int)Math.Min(count, maxFrames);
    }
}
