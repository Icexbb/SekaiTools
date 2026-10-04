using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Emgu.CV;
using Emgu.CV.CvEnum;

namespace SekaiToolsCore.Process;

/// <summary>Single decoder owner; bounded, reusable frames consumed in decode order.</summary>
public sealed class BufferedVideoReader : IDisposable
{
    private readonly VideoCapture _capture;
    private readonly CancellationTokenSource _stop;
    private readonly Channel<DecodedVideoFrame>? _frames;
    private readonly ConcurrentQueue<Mat> _free = new();
    private readonly List<Mat> _buffers = [];
    private readonly SemaphoreSlim _slots;
    private readonly Task? _producer;
    private bool _disposed;

    public BufferedVideoReader(VideoCapture capture, int capacity, CancellationToken token)
    {
        _capture = capture;
        _stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        Capacity = Math.Clamp(capacity, 0, 32);
        var count = Math.Max(1, Capacity);
        _slots = new SemaphoreSlim(count, count);
        for (var i = 0; i < count; i++)
        {
            var buffer = new Mat();
            _buffers.Add(buffer);
            _free.Enqueue(buffer);
        }
        if (Capacity == 0) return;
        _frames = Channel.CreateBounded<DecodedVideoFrame>(new BoundedChannelOptions(count)
        {
            SingleWriter = true, SingleReader = true, FullMode = BoundedChannelFullMode.Wait
        });
        var frameCount = capture.Get(CapProp.FrameCount);
        _producer = Task.Run(() => ProduceAsync(frameCount));
    }

    public int Capacity { get; }

    public DecodedVideoFrame Read()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_frames != null)
            return _frames.Reader.ReadAsync(_stop.Token).AsTask().GetAwaiter().GetResult();
        _slots.Wait(_stop.Token);
        _free.TryDequeue(out var buffer);
        return Decode(buffer!);
    }

    private DecodedVideoFrame Decode(Mat buffer)
    {
        try
        {
            var start = Stopwatch.GetTimestamp();
            var opened = _capture.IsOpened;
            var succeeded = opened && _capture.Read(buffer);
            var duration = Stopwatch.GetElapsedTime(start);
            return new DecodedVideoFrame(buffer, succeeded, _capture.IsOpened,
                (int)_capture.Get(CapProp.PosFrames), _capture.Get(CapProp.PosMsec), duration, Return);
        }
        catch
        {
            Return(buffer);
            throw;
        }
    }

    private async Task ProduceAsync(double frameCount)
    {
        Exception? error = null;
        try
        {
            var retries = 0;
            while (true)
            {
                await _slots.WaitAsync(_stop.Token).ConfigureAwait(false);
                _free.TryDequeue(out var buffer);
                var frame = Decode(buffer!);
                var terminal = !frame.Succeeded && VideoReadFailureClassifier.Classify(
                    frame.CaptureOpened, frame.FrameIndex, frameCount, retries, 2) != VideoReadFailureAction.Retry;
                retries = frame.Succeeded ? 0 : retries + 1;
                try { await _frames!.Writer.WriteAsync(frame, _stop.Token).ConfigureAwait(false); }
                catch { frame.Dispose(); throw; }
                if (terminal) break;
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (Exception exception) { error = exception; }
        finally { _frames!.Writer.TryComplete(error); }
    }

    private void Return(Mat frame)
    {
        _free.Enqueue(frame);
        _slots.Release();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _stop.Cancel();
        _producer?.GetAwaiter().GetResult();
        while (_frames?.Reader.TryRead(out var pending) == true) pending.Dispose();
        foreach (var buffer in _buffers) buffer.Dispose();
        _stop.Dispose();
        _slots.Dispose();
        _disposed = true;
    }

    public static long AvailableMemoryBytes()
    {
        if (OperatingSystem.IsWindows())
        {
            var status = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
            if (GlobalMemoryStatusEx(ref status)) return (long)Math.Min(status.AvailablePhysical, (ulong)long.MaxValue);
        }
        var info = GC.GetGCMemoryInfo();
        return Math.Max(0, info.TotalAvailableMemoryBytes - info.MemoryLoadBytes);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length, Load;
        public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile,
            TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
}

public sealed class DecodedVideoFrame(Mat image, bool succeeded, bool captureOpened,
    int frameIndex, double timecode, TimeSpan decodeDuration, Action<Mat> release) : IDisposable
{
    private Action<Mat>? _release = release;
    public Mat Image { get; } = image;
    public bool Succeeded { get; } = succeeded;
    public bool CaptureOpened { get; } = captureOpened;
    public int FrameIndex { get; } = frameIndex;
    public double Timecode { get; } = timecode;
    public TimeSpan DecodeDuration { get; } = decodeDuration;
    public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke(Image);
}
