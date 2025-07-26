using Emgu.CV;
using Emgu.CV.CvEnum;

namespace SekaiToolsCore.Process;

public static class MatProcessor
{
    public static void LaplaceSharpen(Mat mat, double alpha = 1.0, double beta = 0.0)
    {
        // Convert the image to grayscale if it is not already
        if (mat.NumberOfChannels > 1)
        {
            CvInvoke.CvtColor(mat, mat, ColorConversion.Bgr2Gray);
        }

        // Apply Laplacian filter
        var laplacian = new Mat();
        CvInvoke.Laplacian(mat, laplacian, DepthType.Cv8U);

        // Sharpen the image
        CvInvoke.AddWeighted(mat, alpha, laplacian, beta, 0, mat);
    }
}