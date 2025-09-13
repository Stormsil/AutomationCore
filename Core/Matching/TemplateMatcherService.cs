using AutomationCore.Assets;
using AutomationCore.Core.Abstractions;
using OpenCvSharp;
using static AutomationCore.Core.EnhancedScreenCapture;

namespace AutomationCore.Core.Matching
{
    /// <summary>
    /// Сервис поиска шаблонов с кешированием и оптимизациями
    /// </summary>
    public class TemplateMatcherService : ITemplateMatcherService
    {
        private readonly ITemplateStore _templateStore;
        private readonly IPreprocessor _preprocessor;
        private readonly IMatchingEngine _engine;
        private readonly IMatchCache _cache;
        private readonly ILogger<TemplateMatcherService> _logger;

        public async Task<MatchResult> FindAsync(
            string templateKey,
            Mat sourceImage,
            MatchOptions options = null)
        {
            options ??= MatchOptions.Default;

            // Проверяем кеш
            var cacheKey = BuildCacheKey(templateKey, sourceImage, options);
            if (_cache.TryGet(cacheKey, out var cached))
            {
                _logger.LogDebug("Cache hit for template: {Key}", templateKey);
                return cached;
            }

            // Загружаем шаблон
            using var template = _templateStore.GetTemplate(templateKey);

            // Препроцессинг
            using var processedSource = await _preprocessor.ProcessAsync(
                sourceImage, options.Preprocessing);
            using var processedTemplate = await _preprocessor.ProcessAsync(
                template, options.Preprocessing);

            // Поиск
            var result = await _engine.FindBestMatchAsync(
                processedSource, processedTemplate, options);

            // Кешируем результат
            _cache.Set(cacheKey, result, TimeSpan.FromSeconds(5));

            return result;
        }

        public async Task<MatchResult> WaitForAsync(
            string templateKey,
            WaitForMatchOptions options = null)
        {
            options ??= WaitForMatchOptions.Default;

            using var cts = new CancellationTokenSource(options.Timeout);
            var attempts = 0;

            while (!cts.Token.IsCancellationRequested)
            {
                attempts++;
                _logger.LogDebug("Match attempt {Attempt} for {Key}", attempts, templateKey);

                // Захватываем экран
                using var screen = await CaptureCurrentScreen();

                // Ищем совпадение
                var result = await FindAsync(templateKey, screen, options.MatchOptions);

                if (result.Score >= options.MatchOptions.Threshold)
                {
                    _logger.LogInformation(
                        "Found match for {Key} after {Attempts} attempts",
                        templateKey, attempts);
                    return result;
                }

                await Task.Delay(options.CheckInterval, cts.Token);
            }

            throw new TimeoutException(
                $"Template '{templateKey}' not found after {options.Timeout}");
        }
    }
}