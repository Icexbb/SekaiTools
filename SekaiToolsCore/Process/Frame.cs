namespace SekaiToolsCore.Process;

public class Frame(int index, FrameRate fps)
{
    public int Index => index;
    public string ExactTime() => fps.TimeAtFrame(index).GetAssFormatted();
    public string StartTime() => fps.TimeAtFrame(index, FrameType.Start).GetAssFormatted();
    public string EndTime() => fps.TimeAtFrame(index, FrameType.End).GetAssFormatted();
}