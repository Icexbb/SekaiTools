using Emgu.CV;
using Emgu.CV.CvEnum;

namespace SekaiToolsCore.Utils;

public static class FrameProcess
{
    public static void Process(Mat frame)
    {
        if (frame.IsEmpty)
            return;

        // 转换为灰度图
        CvInvoke.CvtColor(frame, frame, ColorConversion.Bgr2Gray);
    }
}