using Emgu.CV;

namespace SekaiToolsCore.Abstractions;

/// <summary>Creates a capture whose ownership transfers to the processor.</summary>
public interface IVideoCaptureFactory
{
    IVideoCaptureSession Open(string videoPath, int previewInterval);
}

/// <summary>
/// Retains external native dependencies until all processor resources are disposed.
/// Dispose must be idempotent and tolerate Capture having already been disposed.
/// </summary>
public interface IVideoCaptureSession : IDisposable
{
    VideoCapture Capture { get; }

    /// <summary>Stable qualified runtime, plugin and decoder settings identity.</summary>
    string Identity { get; }

    /// <summary>True when individual frames may alternate between gray and BGR.</summary>
    bool HasMixedFrameChannels { get; }
}
