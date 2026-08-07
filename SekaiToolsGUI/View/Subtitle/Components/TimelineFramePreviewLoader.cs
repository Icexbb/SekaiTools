using System.Windows.Media.Imaging;
using Emgu.CV;
using Emgu.CV.CvEnum;

namespace SekaiToolsGUI.View.Subtitle.Components;

internal sealed class TimelineFramePreviewLoader(string videoPath) : IDisposable
{
    private readonly object _sync = new();
    private VideoCapture? _capture;
    private int _nextFrameIndex = -1;
    private bool _disposed;

    public Task<BitmapSource?> LoadFrameAsync(int frameIndex, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                cancellationToken.ThrowIfCancellationRequested();
                _capture ??= new VideoCapture(videoPath);
                var frameCount = Math.Max(1, (int)_capture.Get(CapProp.FrameCount));
                return frameIndex >= 0 && frameIndex < frameCount
                    ? ReadFrame(frameIndex)
                    : null;
            }
        }, cancellationToken);
    }

    public Task<(BitmapSource? First, BitmapSource? Second)> LoadPairAsync(
        int firstFrame,
        int secondFrame,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                cancellationToken.ThrowIfCancellationRequested();
                _capture ??= new VideoCapture(videoPath);
                var frameCount = Math.Max(1, (int)_capture.Get(CapProp.FrameCount));
                var firstIsValid = firstFrame >= 0 && firstFrame < frameCount;
                var secondIsValid = secondFrame >= 0 && secondFrame < frameCount;
                var first = firstIsValid ? ReadFrame(firstFrame) : null;
                cancellationToken.ThrowIfCancellationRequested();
                var second = secondIsValid switch
                {
                    false => null,
                    _ when firstIsValid && secondFrame == firstFrame => first,
                    _ when firstIsValid && secondFrame == firstFrame + 1 => ReadCurrentFrame(),
                    _ => ReadFrame(secondFrame)
                };
                return (first, second);
            }
        }, cancellationToken);
    }

    private BitmapSource? ReadFrame(int frameIndex)
    {
        if (_capture == null)
            return null;
        if (_nextFrameIndex != frameIndex)
        {
            if (!_capture.Set(CapProp.PosFrames, frameIndex))
                return null;
            _nextFrameIndex = frameIndex;
        }

        return ReadCurrentFrame();
    }

    private BitmapSource? ReadCurrentFrame()
    {
        if (_capture == null)
            return null;

        using var frame = new Mat();
        if (!_capture.Read(frame) || frame.IsEmpty)
        {
            _nextFrameIndex = -1;
            return null;
        }

        _nextFrameIndex++;
        var source = frame.ToBitmapSource();
        source.Freeze();
        return source;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;
            _disposed = true;
            _capture?.Dispose();
            _capture = null;
            _nextFrameIndex = -1;
        }
    }
}
