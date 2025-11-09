namespace SekaiToolsCore.Process.FrameSet;

public abstract class BaseFrameSet
{
    public abstract bool IsEmpty();
    public abstract IProcessFrame Start();
    public abstract IProcessFrame End();

    public string StartTime()
    {
        return Start().StartTime();
    }

    public string EndTime()
    {
        return End().EndTime();
    }

    public int StartIndex()
    {
        return Start().Index;
    }

    public int EndIndex()
    {
        return End().Index;
    }
}