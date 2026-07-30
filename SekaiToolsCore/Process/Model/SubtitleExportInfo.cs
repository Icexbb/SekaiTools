using SekaiToolsBase.SubStationAlpha;

namespace SekaiToolsCore.Process.Model;

public record SubtitleExportInfo(
    string ProgramName,
    string ProgramVersion,
    string TaskStatus,
    string VideoFileName,
    string ScriptFileName,
    string TranslationFileName,
    ProcessingResultReport? ResultReport = null)
{
    public IReadOnlyList<Event> MakeComments()
    {
        const string start = "0:00:00.00";
        const string end = "0:00:00.00";
        List<Event> comments =
        [
            Event.Comment($"程序：{ProgramName}；版本：{ProgramVersion}", start, end, "Screen"),
            Event.Comment($"任务运行状态：{TaskStatus}", start, end, "Screen"),
            Event.Comment($"使用素材：视频={VideoFileName}；剧本={ScriptFileName}；翻译={TranslationFileName}",
                start, end, "Screen")
        ];

        if (ResultReport == null) return comments;

        comments.Add(Event.Comment($"识别结果：{ResultReport.Summary}", start, end, "Screen"));
        comments.AddRange(ResultReport.UnmatchedEvents.Select(item =>
            Event.Comment($"未识别：{item.Type}[{item.Index}] {item.Content}；原因={item.Reason}",
                start, end, "Screen")));
        return comments;
    }
}
