// Core/Models/MatchingModels.cs
using System;
using System.Drawing;
using OpenCvSharp;

namespace AutomationCore.Core.Models
{
    /// <summary>
    /// Результат сопоставления шаблона
    /// </summary>
    public readonly record struct MatchResult(
        Rectangle Bounds,
        System.Drawing.Point Center,
        double Score,
        double Scale,
        bool IsMatch)
    {
        // Alias for backward compatibility
        public double Confidence => Score;
        public static MatchResult NotFound => new(Rectangle.Empty, System.Drawing.Point.Empty, 0.0, 1.0, false);

        public bool IsEmpty => Bounds.IsEmpty;
        public System.Drawing.Size Size => Bounds.Size;
    }

    /// <summary>
    /// Запрос на поиск шаблона
    /// </summary>
    public sealed record MatchRequest(
        string TemplateKey,
        ReadOnlyMemory<byte> SourceData,
        int SourceWidth,
        int SourceHeight,
        MatchOptions Options)
    {
        public static MatchRequest Create(
            string templateKey,
            CaptureFrame sourceFrame,
            MatchOptions? options = null)
            => new(templateKey, sourceFrame.Data, sourceFrame.Width, sourceFrame.Height, options ?? MatchOptions.Default);
    }

    /// <summary>
    /// Настройки поиска шаблона
    /// </summary>
    public sealed record MatchOptions
    {
        public static MatchOptions Default { get; } = new();

        public double Threshold { get; init; } = 0.9;
        public Rectangle? SearchRegion { get; set; }
        public TemplateMatchModes Algorithm { get; init; } = TemplateMatchModes.CCoeffNormed;
        public Mat? Mask { get; init; }

        // Мульти-масштабирование
        public bool UseMultiScale { get; init; } = true;
        public ScaleRange ScaleRange { get; init; } = new(0.97, 1.03, 0.01);

        // Предобработка
        public PreprocessingOptions Preprocessing { get; init; } = PreprocessingOptions.Default;

        // Поиск множественных совпадений
        public int MaxResults { get; init; } = 1;
        public double NmsOverlapThreshold { get; init; } = 0.3;

        public bool HigherIsBetter => Algorithm != TemplateMatchModes.SqDiff &&
                                    Algorithm != TemplateMatchModes.SqDiffNormed;
    }

    /// <summary>
    /// Диапазон масштабирования для поиска
    /// </summary>
    public readonly record struct ScaleRange(double Min, double Max, double Step = 0.01)
    {
        public static ScaleRange None => new(1.0, 1.0, 0.01);
        public static ScaleRange Small => new(0.95, 1.05, 0.01);
        public static ScaleRange Medium => new(0.8, 1.2, 0.01);
        public static ScaleRange Large => new(0.5, 1.5, 0.02);
    }

    /// <summary>
    /// Настройки предобработки изображений
    /// </summary>
    public sealed record PreprocessingOptions
    {
        public static PreprocessingOptions Default { get; } = new();

        public bool UseGray { get; init; } = true;
        public bool UseCanny { get; init; } = false;
        public OpenCvSharp.Size? Blur { get; init; } = new(3, 3);

        // Дополнительные настройки
        public double CannyThreshold1 { get; init; } = 80;
        public double CannyThreshold2 { get; init; } = 160;
        public double GaussianSigma { get; init; } = 0;

        public static PreprocessingOptions Fast => Default with
        {
            UseGray = true,
            UseCanny = false,
            Blur = null
        };

        public static PreprocessingOptions Accurate => Default with
        {
            UseGray = true,
            UseCanny = false,
            Blur = new(5, 5)
        };
    }

    /// <summary>
    /// Запрос на ожидание появления шаблона
    /// </summary>
    public sealed record WaitForMatchRequest(
        string TemplateKey,
        TimeSpan Timeout,
        MatchOptions MatchOptions)
    {
        public TimeSpan CheckInterval { get; init; } = TimeSpan.FromMilliseconds(120);
        public bool AllowPartialMatch { get; init; } = false;

        public static WaitForMatchRequest Create(
            string templateKey,
            TimeSpan timeout,
            MatchOptions? options = null)
            => new(templateKey, timeout, options ?? MatchOptions.Default);
    }


    /// <summary>
    /// Результат операции поиска
    /// </summary>
    public sealed record MatchingResult
    {
        public bool Success { get; init; } = true;
        public MatchResult? BestMatch { get; init; }
        public MatchResult[] AllMatches { get; init; } = Array.Empty<MatchResult>();
        public Exception? Error { get; init; }
        public TimeSpan Duration { get; init; }
        public string? TemplateKey { get; init; }

        public static MatchingResult Found(MatchResult match, TimeSpan duration, string? templateKey = null)
            => new() { BestMatch = match, AllMatches = new[] { match }, Duration = duration, TemplateKey = templateKey };

        public static MatchingResult Found(MatchResult[] matches, TimeSpan duration, string? templateKey = null)
            => new()
            {
                BestMatch = matches.Length > 0 ? matches[0] : null,
                AllMatches = matches,
                Duration = duration,
                TemplateKey = templateKey
            };

        public static MatchingResult NotFound(TimeSpan duration, string? templateKey = null)
            => new() { Success = false, Duration = duration, TemplateKey = templateKey };

        public static MatchingResult Failed(Exception error, string? templateKey = null)
            => new() { Success = false, Error = error, TemplateKey = templateKey };
    }

    // Дополнительные типы для обратной совместимости
    /// <summary>
    /// Настройки поиска шаблона (для обратной совместимости)
    /// </summary>
    public sealed class TemplateMatchOptions
    {
        public double Threshold { get; set; } = 0.9;
        public double ScaleMin { get; set; } = 0.97;
        public double ScaleMax { get; set; } = 1.03;
        public double ScaleStep { get; set; } = 0.01;
        public bool UseGray { get; set; } = true;
        public bool UseCanny { get; set; } = false;
        public OpenCvSharp.Size? Blur { get; set; } = new(3, 3);
        public TemplateMatchModes Algorithm { get; set; } = TemplateMatchModes.CCoeffNormed;
        public Mat? Mask { get; set; }
        public OpenCvSharp.Rect? Roi { get; set; }
    }

    /// <summary>
    /// Опции сохранения шаблонов
    /// </summary>
    public sealed class TemplateStoreOptions
    {
        public string BasePath { get; set; } = "templates";
        public bool WatchForChanges { get; set; } = true;
        public bool EnableCaching { get; set; } = true;
    }
}