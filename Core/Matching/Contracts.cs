// AutomationCore/Core/Matching/Contracts.cs
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using static AutomationCore.Core.EnhancedScreenCapture;

namespace AutomationCore.Core.Matching
{

    public class WaitForMatchOptions
    {
        public static WaitForMatchOptions Default => new();

        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
        public TimeSpan CheckInterval { get; set; } = TimeSpan.FromMilliseconds(120);
        public AutomationCore.Core.Abstractions.MatchOptions MatchOptions { get; set; } = AutomationCore.Core.Abstractions.MatchOptions.Default;
    }

    public interface IPreprocessor
    {
        Task<Mat> ProcessAsync(Mat src, AutomationCore.Core.Abstractions.PreprocessingOptions options);
    }

    public interface IMatchingEngine
    {
        Task<MatchResult> FindBestMatchAsync(Mat sourceProcessed, Mat templateProcessed, AutomationCore.Core.Abstractions.MatchOptions options);
    }

    public interface IMatchCache
    {
        bool TryGet(string key, out MatchResult result);
        void Set(string key, MatchResult result, TimeSpan ttl);
    }

    public class MatchCacheOptions
    {
        public int Capacity { get; set; } = 256;
        public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromSeconds(5);
    }
}
