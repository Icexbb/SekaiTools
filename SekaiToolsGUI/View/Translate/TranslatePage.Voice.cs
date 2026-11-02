using System.IO;
using System.Windows;
using System.Windows.Media;
using SekaiDataFetch;
using SekaiToolsGUI.ViewModel.Translate;
using SekaiToolsGUI.ViewModel.Download;
using SekaiToolsGUI.ViewModel.Setting;

namespace SekaiToolsGUI.View.Translate;

public partial class TranslatePage
{
    private readonly System.Windows.Threading.DispatcherTimer _voiceProgressTimer = new()
    { Interval = TimeSpan.FromMilliseconds(50) };
    private bool _voicePlaybackActive;

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

    private MediaPlayer? _voicePlayer;
    private LineDialogModel? _voiceLine;
    private CancellationTokenSource? _voiceRequest;
    private string? _voicePath;

    private void StopVoice()
    {
        _voiceProgressTimer.Stop();
        _voiceProgressTimer.Tick -= UpdateVoiceProgress;
        SetVoicePlaybackActive(false);
        _voiceRequest?.Cancel();
        _voiceRequest?.Dispose();
        _voiceRequest = null;
        _voicePlayer?.Close();
        _voicePlayer = null;
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
        if (ReferenceEquals(line, _voiceLine) && line.VoiceLoading) { StopVoice(); return; }
        StopVoice();
        _voiceLine = line;
        var request = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        _voiceRequest = request;
        line.VoiceLoading = true;
        line.VoiceState = "正在加载语音；再次点击取消";
        try
        {
            Fetcher.Instance.SetProxy(SettingPageModel.Instance.GetProxy());
            var uri = ScenarioVoice.Locate(DownloadPageModel.Instance.CurrentSource.StorageBaseUrl,
                line.VoiceScenarioId, line.OriginalVoice!.Value.VoiceId);
            var path = await ScenarioVoice.LoadAsync(Fetcher.Instance, uri,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SekaiTools", "VoiceCache"), request.Token);
            if (request.IsCancellationRequested || !ReferenceEquals(_voiceRequest, request)) return;
            _voicePath = path;
            var player = new MediaPlayer();
            _voicePlayer = player;
            player.Volume = double.IsFinite(line.OriginalVoice.Value.Volume) ? Math.Clamp(line.OriginalVoice.Value.Volume, 0, 1) : 1;
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
            };
            player.MediaEnded += (_, _) => { if (ReferenceEquals(player, _voicePlayer)) StopVoice(); };
            player.MediaFailed += (_, args) =>
            {
                if (!ReferenceEquals(player, _voicePlayer)) return;
                var failedPath = _voicePath;
                StopVoice();
                try { if (failedPath != null) File.Delete(failedPath); } catch (IOException) { }
                line.VoiceState = "语音加载失败，点击重试：" + args.ErrorException.Message;
            };
            player.Open(new Uri(path));
        }
        catch (Exception ex)
        {
            if (!ReferenceEquals(_voiceRequest, request)) return;
            StopVoice();
            line.VoiceState = "语音加载失败，点击重试：" + ex.Message;
        }
    }
}
