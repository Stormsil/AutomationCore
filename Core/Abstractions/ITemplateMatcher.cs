// AutomationCore/Core/Abstractions/ITemplateMatcher.cs
using System;
using System.Drawing;
using System.Threading.Tasks;
using OpenCvSharp;
using static AutomationCore.Core.EnhancedScreenCapture;

namespace AutomationCore.Core.Abstractions
{
    /// <summary>Интерфейс для поиска изображений (вне DI-слоя сервиса).</summary>
    public interface ITemplateMatcher
    {
        Task<MatchResult> FindBestAsync(MatchRequest request);
        Task<IReadOnlyList<MatchResult>> FindAllAsync(MatchRequest request);
        Task<MatchResult> WaitForAsync(WaitForMatchRequest request);
    }

    public class MatchRequest
    {
        public string TemplateKey { get; set; }
        public Mat SourceImage { get; set; }
        public MatchOptions Options { get; set; } = MatchOptions.Default;

        // Fluent API
        public MatchRequest WithThreshold(double threshold)
        {
            Options.Threshold = threshold;
            return this;
        }

        public MatchRequest InRegion(Rectangle region)
        {
            Options.SearchRegion = region;
            return this;
        }
    }

    public class MatchOptions
    {
        public static MatchOptions Default => new();
        public TemplateMatchModes Mode { get; set; } = TemplateMatchModes.CCoeffNormed;
        public Mat Mask { get; set; } = null;
        public double Threshold { get; set; } = 0.9;
        public Rectangle? SearchRegion { get; set; }
        public bool UseMultiScale { get; set; } = true;
        public ScaleRange ScaleRange { get; set; } = new(0.97, 1.03, 0.01);
        public PreprocessingOptions Preprocessing { get; set; } = PreprocessingOptions.Default;
    }


    public readonly record struct ScaleRange(double Min, double Max, double Step = 0.01);

    public class PreprocessingOptions
    {
        public static PreprocessingOptions Default => new();
        public bool UseGray { get; set; } = true;
        public bool UseCanny { get; set; } = false;
        public OpenCvSharp.Size? Blur { get; set; } = new OpenCvSharp.Size(3, 3);
    }

    public class WaitForMatchRequest
    {
        public string TemplateKey { get; set; }
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
        public TimeSpan CheckInterval { get; set; } = TimeSpan.FromMilliseconds(120);
        public MatchOptions MatchOptions { get; set; } = MatchOptions.Default;
    }
}
