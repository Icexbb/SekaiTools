namespace SekaiToolsCore.Process.FrameSet;

public abstract class FrameSet
{
    public abstract bool IsEmpty();
    public abstract IProcessFrame Start();
    public abstract IProcessFrame End();
    public string StartTime() => Start().StartTime();
    public string EndTime() => End().EndTime();

    public int StartIndex() => Start().Index;

    public int EndIndex() => End().Index;
}