// AutomationCore/Core/Matching/Contracts.cs
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using AutomationCore.Core.Models;
using MatchOptions = AutomationCore.Core.Models.MatchOptions;
using PreprocessingOptions = AutomationCore.Core.Models.PreprocessingOptions;
using MatchResult = AutomationCore.Core.Models.MatchResult;

namespace AutomationCore.Core.Domain.Matching
{

    public class WaitForMatchOptions
    {
        public static WaitForMatchOptions Default => new();

        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
        public TimeSpan CheckInterval { get; set; } = TimeSpan.FromMilliseconds(120);
        public MatchOptions MatchOptions { get; set; } = MatchOptions.Default;
    }

    public interface IPreprocessor
    {
        Task<Mat> ProcessAsync(Mat src, PreprocessingOptions options);
    }

    public interface IMatchingEngine
    {
        Task<MatchResult> FindBestMatchAsync(Mat sourceProcessed, Mat templateProcessed, MatchOptions options);
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
