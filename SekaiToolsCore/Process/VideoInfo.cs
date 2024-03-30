using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;

namespace SekaiToolsCore.Process;

public class VideoInfo
{
    public readonly string Path;
    public readonly Size Resolution;
    public readonly double FrameRatio;
    public readonly FrameRate Fps;
    public readonly int FrameCount;

    public VideoInfo(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("File not found", path);
        Path = path;

        using var video = new VideoCapture(path);
        Resolution = new Size((int)video.Get(CapProp.FrameWidth), (int)video.Get(CapProp.FrameHeight));
        FrameRatio = Resolution.Width / (double)Resolution.Height;
        Fps = new FrameRate((int)video.Get(CapProp.Fps));
        FrameCount = (int)video.Get(CapProp.FrameCount);
    }
}