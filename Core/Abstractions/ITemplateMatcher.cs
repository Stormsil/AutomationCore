using OpenCvSharp;
using static AutomationCore.Core.EnhancedScreenCapture;

namespace AutomationCore.Core.Abstractions
{
    /// <summary>
    /// Интерфейс для поиска изображений
    /// </summary>
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
        public MatchOptions Options { get; set; }

        // Fluent API
        public MatchRequest WithThreshold(double threshold)
        {
            Options ??= new MatchOptions();
            Options.Threshold = threshold;
            return this;
        }

        public MatchRequest InRegion(Rectangle region)
        {
            Options ??= new MatchOptions();
            Options.SearchRegion = region;
            return this;
        }
    }

    public class MatchOptions
    {
        public double Threshold { get; set; } = 0.9;
        public Rectangle? SearchRegion { get; set; }
        public bool UseMultiScale { get; set; } = true;
        public ScaleRange ScaleRange { get; set; } = new(0.97, 1.03);
        public PreprocessingOptions Preprocessing { get; set; } = PreprocessingOptions.Default;
    }
}