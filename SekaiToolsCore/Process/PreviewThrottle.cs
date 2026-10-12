using System.Diagnostics;

namespace SekaiToolsCore.Process;

// Bound display work by elapsed time even when recognition runs faster than playback.
internal sealed class PreviewThrottle
{
    private readonly long _interval = (Stopwatch.Frequency + 4) / 5;
    private long? _lastTimestamp;

    internal bool TryAcquire(long timestamp)
    {
        if (_lastTimestamp is { } previous && timestamp - previous < _interval) return false;
        _lastTimestamp = timestamp;
        return true;
    }
}
