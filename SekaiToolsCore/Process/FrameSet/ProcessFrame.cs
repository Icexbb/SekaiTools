using SekaiToolsCore.Process.Model;

namespace SekaiToolsCore.Process.FrameSet;

public interface IProcessFrame
{
    public int Index { get; }

    public FrameRate Fps { get; }

    public string ExactTime();
    public string StartTime();
    public string EndTime();
}

public class ProcessFrame(int index, FrameRate fps) : IProcessFrame
{
    public static string Zero => "00:00:00.00";
    public int Index { get; } = index;

    public FrameRate Fps { get; } = fps;

    public string ExactTime()
    {
        return Fps.TimeAtFrame(Index).GetAssFormatted();
    }

    public string StartTime()
    {
        return Fps.TimeAtFrame(Index, FrameType.Start).GetAssFormatted();
    }

    public string EndTime()
    {
        return Fps.TimeAtFrame(Index, FrameType.End).GetAssFormatted();
    }
}