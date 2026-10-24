using SekaiToolsCore;
using SekaiToolsCore.Process;
using SekaiToolsCore.Process.Config;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsInfrastructure.Persistence;
using SekaiToolsInfrastructure.Resources;

namespace SekaiToolsHost;

public sealed record SubtitleArgs(string Video, string Script, string Translation, string Output, bool Resume = false);
public sealed record TimingRow(string Kind, int Index, string Content, int StartFrame, int EndFrame, string Start, string End);

public static class SubtitleService
{
    public static async Task<object> Run(SubtitleArgs args, Action<object> progress, CancellationToken token)
    {
        Protocol.RejectInputOverwrite(args.Output, args.Video, args.Script, args.Translation);
        NativeRuntime.Configure();
        progress(new { stage = "正在准备识别模板", value = 0.0 });
        await ResourceManager.Instance.EnsureResource(ResourceType.VideoProcess, token);
        token.ThrowIfCancellationRequested();
        var config = new Config(args.Video, args.Script, args.Translation,
            new StyleFontConfig(), new ExportStyleConfig(), new TypewriterSetting());
        var callbacks = new VideoProcessCallbacks
        {
            IsPreviewEnabled = () => false,
            OnProgress = value => progress(new { stage = "正在识别", value }),
            OnFps = (fps, eta) => progress(new { fps, etaSeconds = eta.TotalSeconds }),
            OnException = error => progress(new { warning = error.Message })
        };
        using var processor = new VideoProcessor(config, callbacks, ResourceManager.Instance,
            SubtitleQueueProcessingStatePersistence.Instance);
        var key = ProgressStore.GetSaveKey(args.Video, args.Script, args.Translation);
        if (args.Resume)
        {
            var saved = SubtitleQueueProgressStore.EnumerateProgressFiles().FirstOrDefault(entry => entry.SaveKey == key);
            if (saved.State == null) throw new InvalidDataException("未找到匹配的未完成进度");
            processor.ApplyState(saved.State);
        }
        processor.EnableProgressSaving(key);
        using var cancellation = token.Register(processor.StopProcess);
        token.ThrowIfCancellationRequested();
        processor.StartProcess();
        // Close the small race between the pre-start check and StartProcess resetting its token.
        if (token.IsCancellationRequested) processor.StopProcess();
        await processor.WaitForCompletionAsync();
        if (token.IsCancellationRequested)
            return new { canceled = true, stopReason = processor.StopReason.ToString(), saveKey = key };
        List<DialogBaseFrameSet> dialogs = [];
        List<BannerBaseFrameSet> banners = [];
        List<MarkerBaseFrameSet> markers = [];
        processor.ReplayExportableCallbacks(dialogs.Add, banners.Add, markers.Add);
        var rows = new List<TimingRow>();
        void Add(BaseFrameSet set, string kind, int index, string content) => rows.Add(new TimingRow(kind, index,
            content, set.StartIndex(), set.EndIndex(), set.StartTime(), set.EndTime()));
        foreach (var set in dialogs) Add(set, "Dialog", set.Data.Index, set.Data.FinalContent);
        foreach (var set in banners) Add(set, "Banner", set.Data.Index, set.Data.FinalContent);
        foreach (var set in markers) Add(set, "Marker", set.Data.Index, set.Data.FinalContent);
        if (rows.Count == 0) throw new InvalidDataException($"没有可导出的结果：{processor.StopReason}");
        Protocol.AtomicWrite(args.Output, processor.GenerateSubtitle(banners, dialogs, markers).ToString());
        return new { canceled = false, stopReason = processor.StopReason.ToString(),
            summary = processor.ResultReport.Summary, output = args.Output, rows };
    }
}
