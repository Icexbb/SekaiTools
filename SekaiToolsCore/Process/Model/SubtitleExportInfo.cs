using SekaiToolsBase.SubStationAlpha;

namespace SekaiToolsCore.Process.Model;

public record SubtitleExportInfo(
    string ProgramName,
    string ProgramVersion,
    string TaskStatus,
    string VideoFileName,
    string ScriptFileName,
    string TranslationFileName)
{
    public IReadOnlyList<Event> MakeComments()
    {
        const string start = "0:00:00.00";
        const string end = "0:00:00.00";
        return
        [
            Event.Comment($"程序：{ProgramName}；版本：{ProgramVersion}", start, end, "Screen"),
            Event.Comment($"任务运行状态：{TaskStatus}", start, end, "Screen"),
            Event.Comment($"使用素材：视频={VideoFileName}；剧本={ScriptFileName}；翻译={TranslationFileName}",
                start, end, "Screen")
        ];
    }
}