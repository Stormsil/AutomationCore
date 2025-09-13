// Features/ImageSearch/ImageSearchEngine.cs
using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using AutomationCore.Core.Abstractions;
using AutomationCore.Core.Models;
using Microsoft.Extensions.Logging;

namespace AutomationCore.Features.ImageSearch
{
    /// <summary>
    /// Высокоуровневый движок для поиска изображений на экране
    /// </summary>
    public sealed class ImageSearchEngine
    {
        private readonly IScreenCapture _capture;
        private readonly ITemplateMatcher _matcher;
        private readonly ILogger<ImageSearchEngine> _logger;

        public ImageSearchEngine(
            IScreenCapture capture,
            ITemplateMatcher matcher,
            ILogger<ImageSearchEngine> logger)
        {
            _capture = capture ?? throw new ArgumentNullException(nameof(capture));
            _matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Ищет изображение на экране
        /// </summary>
        public async ValueTask<ImageSearchResult> FindAsync(
            string templateKey,
            ImageSearchOptions? options = null,
            CancellationToken ct = default)
        {
            options ??= ImageSearchOptions.Default;
            _logger.LogDebug("Searching for image {TemplateKey} with options {@Options}", templateKey, options);

            var startTime = DateTime.UtcNow;

            try
            {
                // Захватываем экран или окно
                var captureRequest = CreateCaptureRequest(options);
                var captureResult = await _capture.CaptureAsync(captureRequest, ct);

                if (!captureResult.Success || !captureResult.Frame.HasValue)
                {
                    return ImageSearchResult.Failed("Failed to capture screen", startTime);
                }

                // Выполняем поиск
                var matchRequest = MatchRequest.Create(templateKey, captureResult.Frame.Value, MapToMatchOptions(options));
                var matchResult = await _matcher.FindBestMatchAsync(matchRequest, ct);

                var duration = DateTime.UtcNow - startTime;

                if (matchResult.Success && matchResult.BestMatch.HasValue)
                {
                    var match = matchResult.BestMatch.Value;
                    var location = new ImageLocation
                    {
                        Bounds = match.Bounds,
                        Center = match.Center,
                        Confidence = match.Confidence,
                        Scale = match.Scale
                    };

                    _logger.LogDebug("Image {TemplateKey} found at {Center} with confidence {Confidence:F3} in {Duration}ms",
                        templateKey, match.Center, match.Confidence, duration.TotalMilliseconds);

                    return ImageSearchResult.Found(location, duration, templateKey);
                }
                else
                {
                    _logger.LogDebug("Image {TemplateKey} not found in {Duration}ms", templateKey, duration.TotalMilliseconds);
                    return ImageSearchResult.NotFound(duration, templateKey);
                }
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                _logger.LogError(ex, "Error searching for image {TemplateKey} after {Duration}ms", templateKey, duration.TotalMilliseconds);
                return ImageSearchResult.Failed(ex.Message, startTime, ex);
            }
        }

        /// <summary>
        /// Ищет все вхождения изображения на экране
        /// </summary>
        public async ValueTask<ImageSearchResult> FindAllAsync(
            string templateKey,
            ImageSearchOptions? options = null,
            CancellationToken ct = default)
        {
            options ??= ImageSearchOptions.Default;
            _logger.LogDebug("Searching for all instances of {TemplateKey}", templateKey);

            var startTime = DateTime.UtcNow;

            try
            {
                var captureRequest = CreateCaptureRequest(options);
                var captureResult = await _capture.CaptureAsync(captureRequest, ct);

                if (!captureResult.Success || !captureResult.Frame.HasValue)
                {
                    return ImageSearchResult.Failed("Failed to capture screen", startTime);
                }

                var matchOptions = MapToMatchOptions(options) with { MaxResults = options.MaxResults };
                var matchRequest = MatchRequest.Create(templateKey, captureResult.Frame.Value, matchOptions);
                var matchResult = await _matcher.FindAllMatchesAsync(matchRequest, ct);

                var duration = DateTime.UtcNow - startTime;

                if (matchResult.Success && matchResult.AllMatches.Length > 0)
                {
                    var locations = Array.ConvertAll(matchResult.AllMatches, match => new ImageLocation
                    {
                        Bounds = match.Bounds,
                        Center = match.Center,
                        Confidence = match.Confidence,
                        Scale = match.Scale
                    });

                    _logger.LogDebug("Found {Count} instances of {TemplateKey} in {Duration}ms",
                        locations.Length, templateKey, duration.TotalMilliseconds);

                    return ImageSearchResult.FoundMultiple(locations, duration, templateKey);
                }
                else
                {
                    return ImageSearchResult.NotFound(duration, templateKey);
                }
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                _logger.LogError(ex, "Error searching for all instances of {TemplateKey} after {Duration}ms", templateKey, duration.TotalMilliseconds);
                return ImageSearchResult.Failed(ex.Message, startTime, ex);
            }
        }

        /// <summary>
        /// Ждет появления изображения на экране
        /// </summary>
        public async ValueTask<ImageSearchResult> WaitForAsync(
            string templateKey,
            TimeSpan timeout,
            ImageSearchOptions? options = null,
            CancellationToken ct = default)
        {
            options ??= ImageSearchOptions.Default;
            _logger.LogDebug("Waiting for image {TemplateKey} with timeout {Timeout}", templateKey, timeout);

            var startTime = DateTime.UtcNow;

            try
            {
                var waitRequest = WaitForMatchRequest.Create(templateKey, timeout, MapToMatchOptions(options));
                var matchResult = await _matcher.WaitForMatchAsync(waitRequest, ct);

                var duration = DateTime.UtcNow - startTime;

                if (matchResult.Success && matchResult.BestMatch.HasValue)
                {
                    var match = matchResult.BestMatch.Value;
                    var location = new ImageLocation
                    {
                        Bounds = match.Bounds,
                        Center = match.Center,
                        Confidence = match.Confidence,
                        Scale = match.Scale
                    };

                    _logger.LogDebug("Image {TemplateKey} appeared after {Duration}ms", templateKey, duration.TotalMilliseconds);
                    return ImageSearchResult.Found(location, duration, templateKey);
                }
                else
                {
                    _logger.LogDebug("Image {TemplateKey} did not appear within timeout {Timeout}", templateKey, timeout);
                    return ImageSearchResult.NotFound(duration, templateKey);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                var duration = DateTime.UtcNow - startTime;
                _logger.LogDebug("Wait for {TemplateKey} was cancelled after {Duration}ms", templateKey, duration.TotalMilliseconds);
                return ImageSearchResult.Failed("Operation was cancelled", startTime);
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                _logger.LogError(ex, "Error waiting for image {TemplateKey} after {Duration}ms", templateKey, duration.TotalMilliseconds);
                return ImageSearchResult.Failed(ex.Message, startTime, ex);
            }
        }

        /// <summary>
        /// Проверяет наличие изображения на экране (быстрая проверка)
        /// </summary>
        public async ValueTask<bool> ExistsAsync(
            string templateKey,
            ImageSearchOptions? options = null,
            CancellationToken ct = default)
        {
            var result = await FindAsync(templateKey, options, ct);
            return result.Success && result.Location != null;
        }

        /// <summary>
        /// Ждет исчезновения изображения с экрана
        /// </summary>
        public async ValueTask<bool> WaitForDisappearAsync(
            string templateKey,
            TimeSpan timeout,
            ImageSearchOptions? options = null,
            CancellationToken ct = default)
        {
            _logger.LogDebug("Waiting for image {TemplateKey} to disappear with timeout {Timeout}", templateKey, timeout);

            var deadline = DateTime.UtcNow + timeout;
            var checkInterval = TimeSpan.FromMilliseconds(500);

            while (DateTime.UtcNow < deadline)
            {
                ct.ThrowIfCancellationRequested();

                var exists = await ExistsAsync(templateKey, options, ct);
                if (!exists)
                {
                    _logger.LogDebug("Image {TemplateKey} has disappeared", templateKey);
                    return true;
                }

                try
                {
                    await Task.Delay(checkInterval, ct);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
            }

            _logger.LogDebug("Image {TemplateKey} did not disappear within timeout", templateKey);
            return false;
        }

        #region Private Methods

        private static CaptureRequest CreateCaptureRequest(ImageSearchOptions options)
        {
            if (options.WindowHandle.HasValue)
            {
                return CaptureRequest.ForWindow(options.WindowHandle.Value);
            }
            else if (options.Region.HasValue)
            {
                return CaptureRequest.ForRegion(options.Region.Value);
            }
            else
            {
                return CaptureRequest.ForScreen(options.MonitorIndex);
            }
        }

        private static MatchOptions MapToMatchOptions(ImageSearchOptions options)
        {
            return new MatchOptions
            {
                Threshold = options.Threshold,
                SearchRegion = options.SearchRegion,
                Algorithm = options.Algorithm,
                UseMultiScale = options.UseMultiScale,
                ScaleRange = options.ScaleRange,
                Preprocessing = options.Preprocessing,
                MaxResults = 1, // Для FindAsync всегда 1
                NmsOverlapThreshold = options.NmsOverlapThreshold
            };
        }

        #endregion
    }

    #region Supporting Types

    /// <summary>
    /// Настройки поиска изображений
    /// </summary>
    public sealed record ImageSearchOptions
    {
        public static ImageSearchOptions Default { get; } = new();

        /// <summary>Пороговое значение уверенности (0.0 - 1.0)</summary>
        public double Threshold { get; init; } = 0.9;

        /// <summary>Область поиска на экране</summary>
        public Rectangle? SearchRegion { get; init; }

        /// <summary>Конкретное окно для поиска</summary>
        public WindowHandle? WindowHandle { get; init; }

        /// <summary>Индекс монитора</summary>
        public int MonitorIndex { get; init; } = 0;

        /// <summary>Регион экрана для захвата</summary>
        public Rectangle? Region { get; init; }

        /// <summary>Алгоритм сопоставления</summary>
        public OpenCvSharp.TemplateMatchModes Algorithm { get; init; } = OpenCvSharp.TemplateMatchModes.CCoeffNormed;

        /// <summary>Использовать мульти-масштабирование</summary>
        public bool UseMultiScale { get; init; } = true;

        /// <summary>Диапазон масштабирования</summary>
        public ScaleRange ScaleRange { get; init; } = new(0.95, 1.05, 0.01);

        /// <summary>Настройки предобработки</summary>
        public PreprocessingOptions Preprocessing { get; init; } = PreprocessingOptions.Default;

        /// <summary>Максимальное количество результатов для FindAll</summary>
        public int MaxResults { get; init; } = 10;

        /// <summary>Порог перекрытия для подавления немаксимумов</summary>
        public double NmsOverlapThreshold { get; init; } = 0.3;

        // Fluent API
        public ImageSearchOptions WithThreshold(double threshold)
            => this with { Threshold = threshold };

        public ImageSearchOptions InRegion(Rectangle region)
            => this with { SearchRegion = region };

        public ImageSearchOptions InWindow(WindowHandle window)
            => this with { WindowHandle = window };

        public ImageSearchOptions OnMonitor(int monitorIndex)
            => this with { MonitorIndex = monitorIndex };
    }

    /// <summary>
    /// Расположение найденного изображения
    /// </summary>
    public sealed record ImageLocation
    {
        public required Rectangle Bounds { get; init; }
        public required Point Center { get; init; }
        public required double Confidence { get; init; }
        public required double Scale { get; init; }

        public int Width => Bounds.Width;
        public int Height => Bounds.Height;
        public Point TopLeft => Bounds.Location;
        public Point BottomRight => new(Bounds.Right, Bounds.Bottom);
    }

    /// <summary>
    /// Результат поиска изображения
    /// </summary>
    public sealed record ImageSearchResult
    {
        public bool Success { get; init; }
        public ImageLocation? Location { get; init; }
        public ImageLocation[]? AllLocations { get; init; }
        public TimeSpan Duration { get; init; }
        public string? TemplateKey { get; init; }
        public string? ErrorMessage { get; init; }
        public Exception? Exception { get; init; }

        public bool IsFound => Success && (Location != null || AllLocations?.Length > 0);
        public int Count => AllLocations?.Length ?? (Location != null ? 1 : 0);

        internal static ImageSearchResult Found(ImageLocation location, TimeSpan duration, string templateKey)
            => new() { Success = true, Location = location, Duration = duration, TemplateKey = templateKey };

        internal static ImageSearchResult FoundMultiple(ImageLocation[] locations, TimeSpan duration, string templateKey)
            => new() { Success = true, AllLocations = locations, Location = locations.Length > 0 ? locations[0] : null, Duration = duration, TemplateKey = templateKey };

        internal static ImageSearchResult NotFound(TimeSpan duration, string templateKey)
            => new() { Success = false, Duration = duration, TemplateKey = templateKey };

        internal static ImageSearchResult Failed(string error, DateTime startTime, Exception? ex = null)
            => new() { Success = false, ErrorMessage = error, Exception = ex, Duration = DateTime.UtcNow - startTime };
    }

    #endregion
}