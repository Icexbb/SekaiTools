namespace SekaiToolsCore.Process;

public class Frame(int index, FrameRate fps)
{
    public int Index => index;

    public static string Zero => "00:00:00.00";

    public string ExactTime()
    {
        return fps.TimeAtFrame(index).GetAssFormatted();
    }

    public string StartTime()
    {
        return fps.TimeAtFrame(index, FrameType.Start).GetAssFormatted();
    }

    public string EndTime()
    {
        return fps.TimeAtFrame(index, FrameType.End).GetAssFormatted();
    }
}