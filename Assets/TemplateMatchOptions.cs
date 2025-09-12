// AutomationCore/Assets/TemplateMatchOptions.cs
using OpenCvSharp;
using CvSize = OpenCvSharp.Size;
using CvRect = OpenCvSharp.Rect;

namespace AutomationCore.Assets
{
    /// <summary>
    /// Опции матчинга шаблонов (универсальный пресет использует их по умолчанию).
    /// </summary>
    public record TemplateMatchOptions(
        double Threshold = 0.90,
        bool UseGray = true,
        bool UseCanny = false,
        CvSize? Blur = null,          // 👈 было Size?
        CvRect? Roi = null,           // 👈 было Rect?
        TemplateMatchModes Mode = TemplateMatchModes.CCoeffNormed,
        bool AdaptiveThreshold = false,
        double AdaptiveK = 3.0,
        double ScaleMin = 1.0,
        double ScaleMax = 1.0,
        double ScaleStep = 0.01,
        int ConsecutiveHits = 1,
        Mat? Mask = null
    )
    {
        public bool HigherIsBetter =>
            Mode != TemplateMatchModes.SqDiff &&
            Mode != TemplateMatchModes.SqDiffNormed;
    }
}
