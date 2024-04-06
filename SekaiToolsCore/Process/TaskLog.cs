namespace SekaiToolsCore.Process;

public enum TaskLogType
{
    Content,
    Progress,
    Request,
    Exception
}

public abstract class TaskLog
{
    public abstract TaskLogType Type { get; }
}

public class TaskLogProgress(double progressedFrameCount, int totalFrameCount, long startProcessTime) : TaskLog
{
    public override TaskLogType Type { get; } = TaskLogType.Progress;

    private double ProgressedFrameCount { get; } = progressedFrameCount;
    private int TotalFrameCount { get; } = totalFrameCount;
    private long InfoTimeMilliseconds { get; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startProcessTime;

    public double Progress => ProgressedFrameCount / TotalFrameCount * 100;
    public double Fps => ProgressedFrameCount / (InfoTimeMilliseconds / 1000.0);

    public bool Finished => Math.Abs(ProgressedFrameCount - TotalFrameCount) < 1;
}

public class TaskLogContext(string content = "") : TaskLog
{
    public override TaskLogType Type { get; } = TaskLogType.Content;
    public string Content { get; } = content;
}

public class RequestItem(string content, int startFrame, int endFrame, double fps, bool isDialogJitter = false)
{
    public string Content { get; } = content;
    public int StartFrame { get; } = startFrame;
    public int EndFrame { get; } = endFrame;
    public double Fps { get; } = fps;

    public bool IsDialogJitter { get; } = isDialogJitter;
}

public class TaskLogRequest(List<RequestItem> collection) : TaskLog
{
    public override TaskLogType Type { get; } = TaskLogType.Request;

    public List<RequestItem> Collection { get; } = collection;
}

public class TaskLogException(Exception exception) : TaskLog
{
    public override TaskLogType Type { get; } = TaskLogType.Exception;
    public Exception Exception { get; } = exception;
}