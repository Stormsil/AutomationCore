// AutomationCore/Assets/TemplatePresets.cs
using OpenCvSharp;
using CvSize = OpenCvSharp.Size;
using CvRect = OpenCvSharp.Rect;

namespace AutomationCore.Assets
{
    public static class TemplatePresets
    {
        public static readonly TemplateMatchOptions Universal = new TemplateMatchOptions(
            Threshold: 0.90,
            UseGray: true,
            UseCanny: false,
            Blur: new CvSize(3, 3),
            Roi: null,
            Mode: TemplateMatchModes.CCoeffNormed,
            AdaptiveThreshold: false,
            AdaptiveK: 3.0,
            ScaleMin: 0.97,
            ScaleMax: 1.03,
            ScaleStep: 0.01,
            ConsecutiveHits: 2,
            Mask: null
        );
    }
}
