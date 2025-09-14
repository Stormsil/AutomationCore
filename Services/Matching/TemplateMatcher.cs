// Services/Matching/TemplateMatcher.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AutomationCore.Core.Abstractions;
using AutomationCore.Core.Exceptions;
using AutomationCore.Core.Models;
using Microsoft.Extensions.Logging;
using MatchOptions = AutomationCore.Core.Models.MatchOptions;
using PreprocessingOptions = AutomationCore.Core.Models.PreprocessingOptions;

namespace AutomationCore.Services.Matching
{
    /// <summary>
    /// Основная реализация сопоставления шаблонов
    /// </summary>
    public sealed class TemplateMatcher : ITemplateMatcher
    {
        private readonly ITemplateStorage _storage;
        private readonly IImagePreprocessor _preprocessor;
        private readonly IMatchingEngine _engine;
        private readonly IMatchCache _cache;
        private readonly ILogger<TemplateMatcher> _logger;

        public TemplateMatcher(
            ITemplateStorage storage,
            IImagePreprocessor preprocessor,
            IMatchingEngine engine,
            IMatchCache cache,
            ILogger<TemplateMatcher> logger)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _preprocessor = preprocessor ?? throw new ArgumentNullException(nameof(preprocessor));
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async ValueTask<MatchingResult> FindBestMatchAsync(MatchRequest request, CancellationToken ct = default)
        {
            _logger.LogDebug("Finding best match for template {TemplateKey}", request.TemplateKey);

            var startTime = DateTime.UtcNow;

            try
            {
                // Проверяем кэш
                var cacheKey = GenerateCacheKey(request);
                var cachedResult = await _cache.GetAsync(cacheKey, ct);
                if (cachedResult != null)
                {
                    _logger.LogTrace("Found cached result for {TemplateKey}", request.TemplateKey);
                    return cachedResult;
                }

                // Загружаем шаблон
                var templateData = await LoadTemplateAsync(request.TemplateKey, ct);

                // Выполняем поиск
                var matches = await PerformMatchingAsync(request, templateData, ct);

                var duration = DateTime.UtcNow - startTime;
                var result = matches.Length > 0
                    ? MatchingResult.Found(matches, duration, request.TemplateKey)
                    : MatchingResult.NotFound(duration, request.TemplateKey);

                // Кэшируем результат
                await _cache.SetAsync(cacheKey, result, TimeSpan.FromSeconds(2), ct);

                _logger.LogDebug("Matching completed in {Duration}ms, found {Count} matches",
                    duration.TotalMilliseconds, matches.Length);

                return result;
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                _logger.LogError(ex, "Matching failed after {Duration}ms", duration.TotalMilliseconds);
                return MatchingResult.Failed(new TemplateMatchingException("Matching failed", ex, request.TemplateKey));
            }
        }

        public async ValueTask<MatchingResult> FindAllMatchesAsync(MatchRequest request, CancellationToken ct = default)
        {
            _logger.LogDebug("Finding all matches for template {TemplateKey} (max: {MaxResults})",
                request.TemplateKey, request.Options.MaxResults);

            var startTime = DateTime.UtcNow;

            try
            {
                // Загружаем шаблон
                var templateData = await LoadTemplateAsync(request.TemplateKey, ct);

                // Выполняем поиск всех совпадений
                var matches = await PerformMatchingAsync(request, templateData, ct);

                // Применяем NMS (Non-Maximum Suppression) для фильтрации перекрывающихся результатов
                var filteredMatches = ApplyNonMaximumSuppression(matches, request.Options.NmsOverlapThreshold);

                // Ограничиваем количество результатов
                var finalMatches = filteredMatches.Take(request.Options.MaxResults).ToArray();

                var duration = DateTime.UtcNow - startTime;
                var result = finalMatches.Length > 0
                    ? MatchingResult.Found(finalMatches, duration, request.TemplateKey)
                    : MatchingResult.NotFound(duration, request.TemplateKey);

                _logger.LogDebug("Found {Count} matches after filtering", finalMatches.Length);
                return result;
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                _logger.LogError(ex, "Multi-matching failed after {Duration}ms", duration.TotalMilliseconds);
                return MatchingResult.Failed(new TemplateMatchingException("Multi-matching failed", ex, request.TemplateKey));
            }
        }

        public async ValueTask<MatchingResult> WaitForMatchAsync(WaitForMatchRequest request, CancellationToken ct = default)
        {
            _logger.LogDebug("Waiting for template {TemplateKey} with timeout {Timeout}",
                request.TemplateKey, request.Timeout);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(request.Timeout);

            var startTime = DateTime.UtcNow;
            var lastMatch = MatchingResult.NotFound(TimeSpan.Zero, request.TemplateKey);

            try
            {
                while (!timeoutCts.Token.IsCancellationRequested)
                {
                    // TODO: Здесь нужно интегрироваться с захватом экрана
                    // Пока что возвращаем заглушку
                    await Task.Delay(request.CheckInterval, timeoutCts.Token);

                    // Симуляция поиска (нужно заменить на реальный захват + поиск)
                    lastMatch = MatchingResult.NotFound(DateTime.UtcNow - startTime, request.TemplateKey);

                    if (lastMatch.Success)
                    {
                        _logger.LogDebug("Template found after {Duration}ms",
                            (DateTime.UtcNow - startTime).TotalMilliseconds);
                        return lastMatch;
                    }
                }

                var duration = DateTime.UtcNow - startTime;
                _logger.LogDebug("Template not found within timeout {Duration}ms", duration.TotalMilliseconds);
                return MatchingResult.NotFound(duration, request.TemplateKey);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                _logger.LogDebug("Wait for match was cancelled by user");
                throw;
            }
            catch (OperationCanceledException)
            {
                var duration = DateTime.UtcNow - startTime;
                _logger.LogDebug("Wait for match timed out after {Duration}ms", duration.TotalMilliseconds);
                return MatchingResult.NotFound(duration, request.TemplateKey);
            }
        }

        public async IAsyncEnumerable<MatchingResult> WatchForMatchesAsync(
            MatchRequest request,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            _logger.LogDebug("Starting to watch for matches of template {TemplateKey}", request.TemplateKey);

            while (!ct.IsCancellationRequested)
            {
                MatchingResult result;
                try
                {
                    result = await FindBestMatchAsync(request, ct);
                }
                catch (OperationCanceledException)
                {
                    yield break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error in watch stream for template {TemplateKey}", request.TemplateKey);
                    continue;
                }

                yield return result;

                // Небольшая задержка между попытками
                try
                {
                    await Task.Delay(100, ct);
                }
                catch (OperationCanceledException)
                {
                    yield break;
                }
            }
        }

        #region Private Methods

        private async ValueTask<Core.Abstractions.TemplateData> LoadTemplateAsync(string templateKey, CancellationToken ct)
        {
            if (!await _storage.ContainsAsync(templateKey, ct))
            {
                throw new TemplateNotFoundException(templateKey);
            }

            var template = await _storage.LoadAsync(templateKey, ct);
            if (template.IsEmpty)
            {
                throw new InvalidTemplateException(templateKey, "Template data is empty");
            }

            return template;
        }

        private async ValueTask<MatchResult[]> PerformMatchingAsync(
            MatchRequest request,
            Core.Abstractions.TemplateData templateData,
            CancellationToken ct)
        {
            // Предобработка исходного изображения
            using var processedSource = await _preprocessor.ProcessAsync(
                request.SourceData,
                request.SourceWidth,
                request.SourceHeight,
                4, // BGRA
                request.Options.Preprocessing,
                ct);

            // Предобработка шаблона
            using var processedTemplate = await _preprocessor.ProcessAsync(
                templateData.Data,
                templateData.Width,
                templateData.Height,
                templateData.Channels,
                request.Options.Preprocessing,
                ct);

            // Выполняем поиск с мультимасштабированием если необходимо
            if (request.Options.UseMultiScale)
            {
                return await _engine.FindMultiScaleMatchesAsync(
                    processedSource,
                    processedTemplate,
                    request.Options,
                    ct);
            }
            else
            {
                var singleMatch = await _engine.FindBestMatchAsync(
                    processedSource,
                    processedTemplate,
                    request.Options,
                    ct);

                return singleMatch.HasValue ? new[] { singleMatch.Value } : Array.Empty<MatchResult>();
            }
        }

        private static string GenerateCacheKey(MatchRequest request)
        {
            // Упрощенный ключ кэша (в реальности нужно учитывать больше параметров)
            var sourceHash = request.SourceData.Length.ToString();
            var optionsHash = request.Options.GetHashCode().ToString();

            return $"{request.TemplateKey}_{sourceHash}_{optionsHash}";
        }

        private static MatchResult[] ApplyNonMaximumSuppression(MatchResult[] matches, double overlapThreshold)
        {
            if (matches.Length <= 1) return matches;

            var sorted = matches.OrderByDescending(m => m.Confidence).ToArray();
            var result = new List<MatchResult>();

            foreach (var match in sorted)
            {
                bool shouldKeep = true;

                foreach (var existing in result)
                {
                    var overlap = CalculateOverlap(match.Bounds, existing.Bounds);
                    if (overlap > overlapThreshold)
                    {
                        shouldKeep = false;
                        break;
                    }
                }

                if (shouldKeep)
                {
                    result.Add(match);
                }
            }

            return result.ToArray();
        }

        private static double CalculateOverlap(System.Drawing.Rectangle rect1, System.Drawing.Rectangle rect2)
        {
            var intersection = System.Drawing.Rectangle.Intersect(rect1, rect2);
            if (intersection.IsEmpty) return 0.0;

            var area1 = rect1.Width * rect1.Height;
            var area2 = rect2.Width * rect2.Height;
            var intersectionArea = intersection.Width * intersection.Height;
            var unionArea = area1 + area2 - intersectionArea;

            return unionArea > 0 ? (double)intersectionArea / unionArea : 0.0;
        }

        #endregion
    }

    /// <summary>
    /// Интерфейс движка сопоставления
    /// </summary>
    public interface IMatchingEngine
    {
        ValueTask<MatchResult?> FindBestMatchAsync(
            ProcessedImage source,
            ProcessedImage template,
            MatchOptions options,
            CancellationToken ct = default);

        ValueTask<MatchResult[]> FindMultiScaleMatchesAsync(
            ProcessedImage source,
            ProcessedImage template,
            MatchOptions options,
            CancellationToken ct = default);
    }
}