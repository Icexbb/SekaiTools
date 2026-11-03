using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using SekaiDataFetch;
using SekaiToolsBase;
using SekaiToolsGUI.ViewModel.Download;
using SekaiToolsGUI.ViewModel.Setting;
using SekaiToolsGUI.ViewModel.Translate;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.View.Translate;

public partial class TranslatePage
{
    private static readonly ILogger VoiceLogger = Logger.CreateLogger("TranslationVoicePreview");

    private readonly DispatcherTimer _voiceProgressTimer = new()
        { Interval = TimeSpan.FromMilliseconds(50) };

    private IDisposable? _voiceCacheProtection;
    private LineDialogModel? _voiceLine;
    private string? _voicePath;
    private bool _voicePlaybackActive;

    private MediaPlayer? _voicePlayer;
    private CancellationTokenSource? _voiceRequest;

    private void SetVoicePlaybackActive(bool active)
    {
        _voicePlaybackActive = active;
        foreach (var row in ViewModel.Events) row.VoicePlaybackAvailable = !active;
    }

    private void UpdateVoiceProgress(object? sender, EventArgs e)
    {
        if (_voicePlayer == null || _voiceLine == null || !_voicePlayer.NaturalDuration.HasTimeSpan) return;
        var duration = _voicePlayer.NaturalDuration.TimeSpan.TotalSeconds;
        _voiceLine.VoiceProgress = duration > 0 ? Math.Clamp(_voicePlayer.Position.TotalSeconds / duration, 0, 1) : 0;
    }

    private void StopVoice()
    {
        StopVoiceWithReason("停止或取消");
    }

    private void StopVoiceWithReason(string reason)
    {
        if (_voiceLine is { } current)
            VoiceLogger.LogInformation(
                "语音预览{Reason}：第 {Sequence} 条，剧本 {ScenarioId}，语音 {VoiceId}，位置 {PositionSeconds:F2} 秒",
                reason, current.SequenceNumber, current.VoiceScenarioId, current.OriginalVoice?.VoiceId,
                _voicePlayer?.Position.TotalSeconds ?? 0);

        _voiceProgressTimer.Stop();
        _voiceProgressTimer.Tick -= UpdateVoiceProgress;
        SetVoicePlaybackActive(false);
        _voiceRequest?.Cancel();
        _voiceRequest?.Dispose();
        _voiceRequest = null;
        _voicePlayer?.Close();
        _voicePlayer = null;
        _voiceCacheProtection?.Dispose();
        _voiceCacheProtection = null;
        if (_voicePath is { } stoppedPath)
        {
            var cacheDirectory = Directory.GetParent(Path.GetDirectoryName(stoppedPath)!)!.FullName;
            VoiceCache.Trim(cacheDirectory, VoiceCache.AutomaticLimit(cacheDirectory));
        }

        _voicePath = null;
        if (_voiceLine != null)
        {
            _voiceLine.VoiceProgress = 0;
            _voiceLine.VoiceLoading = _voiceLine.VoicePlaying = false;
            _voiceLine.VoiceState = "播放原始语音";
        }

        _voiceLine = null;
    }

    private async void OnVoiceRequested(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not LineDialogModel { HasVoice: true } line) return;
        e.Handled = true;
        if (_voicePlaybackActive) return;
        if (ReferenceEquals(line, _voiceLine) && line.VoiceLoading)
        {
            StopVoice();
            return;
        }

        StopVoice();
        _voiceLine = line;
        var request = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        _voiceRequest = request;
        line.VoiceLoading = true;
        line.VoiceState = "正在加载语音；再次点击取消";
        VoiceLogger.LogInformation("加载语音预览：第 {Sequence} 条，剧本 {ScenarioId}，语音 {VoiceId}，数据源 {Source}",
            line.SequenceNumber, line.VoiceScenarioId, line.OriginalVoice?.VoiceId,
            DownloadPageModel.Instance.CurrentSource.SourceName);
        try
        {
            Fetcher.Instance.SetProxy(SettingPageModel.Instance.GetProxy());
            var uri = ScenarioVoice.Locate(DownloadPageModel.Instance.CurrentSource.StorageBaseUrl,
                line.VoiceScenarioId, line.OriginalVoice!.Value.VoiceId);
            VoiceLogger.LogInformation("语音资源地址：第 {Sequence} 条，剧本 {ScenarioId}，语音 {VoiceId}，URL {AudioUrl}",
                line.SequenceNumber, line.VoiceScenarioId, line.OriginalVoice.Value.VoiceId, uri.AbsoluteUri);
            var path = await ScenarioVoice.LoadAsync(Fetcher.Instance, uri,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SekaiTools",
                    "VoiceCache"), request.Token);
            if (request.IsCancellationRequested || !ReferenceEquals(_voiceRequest, request)) return;
            VoiceLogger.LogInformation("语音资源已就绪：第 {Sequence} 条，语音 {VoiceId}，本地文件 {Path}",
                line.SequenceNumber, line.OriginalVoice?.VoiceId, path);
            _voiceCacheProtection = VoiceCache.Protect(path);
            _voicePath = path;
            var player = new MediaPlayer();
            _voicePlayer = player;
            player.Volume = double.IsFinite(line.OriginalVoice.Value.Volume)
                ? Math.Clamp(line.OriginalVoice.Value.Volume, 0, 1)
                : 1;
            player.MediaOpened += (_, _) =>
            {
                if (!ReferenceEquals(player, _voicePlayer)) return;
                line.VoiceLoading = false;
                line.VoicePlaying = true;
                line.VoiceState = "正在播放原始语音";
                SetVoicePlaybackActive(true);
                _voiceProgressTimer.Tick += UpdateVoiceProgress;
                _voiceProgressTimer.Start();
                player.Play();
                VoiceLogger.LogInformation(
                    "开始播放语音预览：第 {Sequence} 条，剧本 {ScenarioId}，语音 {VoiceId}，时长 {DurationSeconds:F2} 秒，音量 {Volume}",
                    line.SequenceNumber, line.VoiceScenarioId, line.OriginalVoice?.VoiceId,
                    player.NaturalDuration.HasTimeSpan ? player.NaturalDuration.TimeSpan.TotalSeconds : 0,
                    player.Volume);
            };
            player.MediaEnded += (_, _) =>
            {
                if (ReferenceEquals(player, _voicePlayer)) StopVoiceWithReason("播放完成");
            };
            player.MediaFailed += (_, args) =>
            {
                if (!ReferenceEquals(player, _voicePlayer)) return;
                VoiceLogger.LogError(args.ErrorException, "语音预览播放失败：第 {Sequence} 条，剧本 {ScenarioId}，语音 {VoiceId}",
                    line.SequenceNumber, line.VoiceScenarioId, line.OriginalVoice?.VoiceId);
                var failedPath = _voicePath;
                StopVoiceWithReason("播放失败");
                try
                {
                    if (failedPath != null) File.Delete(failedPath);
                }
                catch (IOException ex)
                {
                    VoiceLogger.LogWarning(ex, "清理无法播放的语音缓存失败：{Path}", failedPath);
                }

                line.VoiceState = "语音加载失败，点击重试：" + args.ErrorException.Message;
            };
            player.Open(new Uri(path));
        }
        catch (Exception ex)
        {
            if (!ReferenceEquals(_voiceRequest, request)) return;
            if (ex is OperationCanceledException)
                VoiceLogger.LogWarning(ex, "语音预览加载超时：第 {Sequence} 条，剧本 {ScenarioId}，语音 {VoiceId}",
                    line.SequenceNumber, line.VoiceScenarioId, line.OriginalVoice?.VoiceId);
            else
                VoiceLogger.LogError(ex, "语音预览加载失败：第 {Sequence} 条，剧本 {ScenarioId}，语音 {VoiceId}",
                    line.SequenceNumber, line.VoiceScenarioId, line.OriginalVoice?.VoiceId);
            StopVoiceWithReason("加载失败");
            line.VoiceState = "语音加载失败，点击重试：" + ex.Message;
            if (ex is HttpRequestException { StatusCode: HttpStatusCode.NotFound })
            {
                line.VoiceState = "语音文件不存在（404），可点击重试";
                SnackbarService.Show("语音播放失败", $"第 {line.SequenceNumber} 条的语音文件不存在（404）：{line.OriginalVoice?.VoiceId}。",
                    ControlAppearance.Danger, new SymbolIcon(SymbolRegular.CloudDismiss24), TimeSpan.FromSeconds(6));
            }
        }
    }
}