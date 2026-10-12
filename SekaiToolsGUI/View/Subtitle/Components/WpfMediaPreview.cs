using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SekaiToolsBase;

namespace SekaiToolsGUI.View.Subtitle.Components;

/// <summary>Uses one WPF player for audio and video, rendered directly by the preview Image.</summary>
internal static class WpfMediaPreview
{
    public static async Task PlayAsync(string path, TimeSpan start, TimeSpan end, bool video,
        Action<ImageSource> showFrame, Action<TimeSpan> reportPosition, CancellationToken cancellationToken)
    {
        if (start < TimeSpan.Zero || end <= start)
            throw new ArgumentOutOfRangeException(nameof(end));
        cancellationToken.ThrowIfCancellationRequested();

        var player = new MediaPlayer();
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var timer = new DispatcherTimer(DispatcherPriority.Normal)
        {
            Interval = TimeSpan.FromMilliseconds(10)
        };
        EventHandler openedHandler = (_, _) => opened.TrySetResult();
        EventHandler endedHandler = (_, _) => completed.TrySetResult();
        EventHandler<ExceptionEventArgs> failedHandler = (_, e) =>
        {
            opened.TrySetException(e.ErrorException);
            completed.TrySetException(e.ErrorException);
        };
        EventHandler tickHandler = (_, _) =>
        {
            try
            {
                var position = player.Position;
                reportPosition(position);
                if (position >= end)
                {
                    player.Pause();
                    timer.Stop();
                    completed.TrySetResult();
                }
            }
            catch (Exception ex)
            {
                completed.TrySetException(ex);
            }
        };
        player.MediaOpened += openedHandler;
        player.MediaEnded += endedHandler;
        player.MediaFailed += failedHandler;
        timer.Tick += tickHandler;
        using var cancellation = cancellationToken.Register(() => completed.TrySetCanceled(cancellationToken));
        try
        {
            player.Open(new Uri(Path.GetFullPath(path)));
            await opened.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (video && (!player.HasVideo || player.NaturalVideoWidth <= 0 || player.NaturalVideoHeight <= 0))
                throw new InvalidOperationException("系统播放器无法解码视频画面");
            if (player.NaturalDuration.HasTimeSpan && start >= player.NaturalDuration.TimeSpan)
                throw new InvalidOperationException("语句起点超出媒体时长");

            Logger.Log($"[语句预览] WPF 媒体已打开 | 文件={path} | 视频={player.HasVideo} | 音频={player.HasAudio} | 分辨率={player.NaturalVideoWidth}×{player.NaturalVideoHeight} | 时长={player.NaturalDuration}");

            if (video)
            {
                // Keep this drawing live: freezing it or copying frames during playback would
                // separate video presentation from the player's audio clock again.
                showFrame(new DrawingImage(new VideoDrawing
                {
                    Player = player,
                    Rect = new Rect(0, 0, player.NaturalVideoWidth, player.NaturalVideoHeight)
                }));
            }
            player.Position = start;
            player.Play();
            timer.Start();
            await completed.Task;
            cancellationToken.ThrowIfCancellationRequested();
            player.Pause();
            if (video)
                showFrame(CaptureFrame(player));
        }
        finally
        {
            timer.Stop();
            timer.Tick -= tickHandler;
            player.MediaOpened -= openedHandler;
            player.MediaEnded -= endedHandler;
            player.MediaFailed -= failedHandler;
            player.Close();
            // Observe a failure reported before opening even if the open task threw first.
            if (completed.Task.IsFaulted) _ = completed.Task.Exception;
        }
    }

    private static BitmapSource CaptureFrame(MediaPlayer player)
    {
        var scale = Math.Min(1, 1280d / player.NaturalVideoWidth);
        var width = Math.Max(1, (int)(player.NaturalVideoWidth * scale));
        var height = Math.Max(1, (int)(player.NaturalVideoHeight * scale));
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
            context.DrawVideo(player, new Rect(0, 0, width, height));
        var frame = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        frame.Render(visual);
        frame.Freeze();
        return frame;
    }
}
