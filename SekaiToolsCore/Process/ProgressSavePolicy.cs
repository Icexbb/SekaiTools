namespace SekaiToolsCore.Process;

/// <summary>
/// 控制进度检查点频率，避免随着视频帧数增长重复写入完整状态。
/// </summary>
internal static class ProgressSavePolicy
{
    public const int InitialInterval = 300;
    public const int MinimumCompletionInterval = 30;
    public static int GetNextFrame(int currentFrame)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(currentFrame);
        var interval = Math.Max(currentFrame, InitialInterval);
        return currentFrame > int.MaxValue - interval
            ? int.MaxValue
            : currentFrame + interval;
    }

    public static bool ShouldSave(int frameIndex, int nextFrame, int lastSavedFrame, bool frameSetJustCompleted)
    {
        if (frameIndex >= nextFrame) return true;
        // 仅允许第一次完成事件提前落一个检查点；之后统一遵循增长中的计划，
        // 避免大量短事件把完整状态保存重新放大为 O(N²)。
        return frameSetJustCompleted && lastSavedFrame == 0 && frameIndex >= MinimumCompletionInterval;
    }
}
