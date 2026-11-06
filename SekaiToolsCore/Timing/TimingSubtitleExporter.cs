using System.Drawing;
using SekaiToolsBase.SubStationAlpha;
using SekaiToolsCore.Process.Config;
using SekaiToolsCore.Process.FrameSet;

namespace SekaiToolsCore.Timing;

public static class TimingSubtitleExporter
{
    public static Subtitle Make(Size resolution, string videoPath, IEnumerable<DialogBaseFrameSet> dialogs,
        StyleFontConfig fonts, ExportStyleConfig options)
    {
        var style = new Style("Line1", fonts.DialogFontFamily,
            Math.Max(12, (int)(resolution.Height * .043)), shadow: 0,
            marginL: resolution.Width / 10, marginR: resolution.Width / 10, marginV: resolution.Height / 12);
        var events = dialogs.Where(set => !set.IsEmpty() && !string.IsNullOrWhiteSpace(set.Data.BodyTranslated))
            .Select(set => Event.Dialog(set.Data.BodyTranslated.Replace("\r\n", "\n").Replace("\n", "\\N"),
                set.StartTime(), set.EndTime(), "Line1")).ToArray();
        if (!options.ExportLine1) events = [];
        return new Subtitle(new ScriptInfo(resolution.Width, resolution.Height),
            new Garbage(Path.GetFileName(videoPath), Path.GetFileName(videoPath)),
            new Styles([style]), new Events(events));
    }
}
