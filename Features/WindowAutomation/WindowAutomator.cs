// Features/WindowAutomation/WindowAutomator.cs
using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using AutomationCore.Core.Abstractions;
using AutomationCore.Core.Models;
using AutomationCore.Features.ImageSearch;
using Microsoft.Extensions.Logging;

namespace AutomationCore.Features.WindowAutomation
{
    /// <summary>
    /// Высокоуровневый автоматор для работы с конкретными окнами
    /// </summary>
    public sealed class WindowAutomator
    {
        private readonly IWindowManager _windowManager;
        private readonly IScreenCapture _capture;
        private readonly ITemplateMatcher _matcher;
        private readonly IInputSimulator _input;
        private readonly ImageSearchEngine _imageSearch;
        private readonly ILogger<WindowAutomator> _logger;

        public WindowAutomator(
            IWindowManager windowManager,
            IScreenCapture capture,
            ITemplateMatcher matcher,
            IInputSimulator input,
            ImageSearchEngine imageSearch,
            ILogger<WindowAutomator> logger)
        {
            _windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
            _capture = capture ?? throw new ArgumentNullException(nameof(capture));
            _matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _imageSearch = imageSearch ?? throw new ArgumentNullException(nameof(imageSearch));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Находит и активирует окно, затем кликает по изображению внутри него
        /// </summary>
        public async ValueTask<WindowOperationResult> ClickOnImageInWindowAsync(
            string windowTitle,
            string templateKey,
            WindowAutomationOptions? options = null,
            CancellationToken ct = default)
        {
            options ??= WindowAutomationOptions.Default;
            var startTime = DateTime.UtcNow;

            _logger.LogDebug("Attempting to click on image {TemplateKey} in window '{WindowTitle}'", templateKey, windowTitle);

            try
            {
                // Находим окно
                var window = await FindAndPrepareWindowAsync(windowTitle, options, ct);
                if (window == null)
                {
                    return WindowOperationResult.Failed($"Window '{windowTitle}' not found", startTime);
                }

                // Ищем изображение внутри окна
                var searchOptions = ImageSearchOptions.Default
                    .WithThreshold(options.MatchThreshold)
                    .InWindow(window.Handle);

                var searchResult = await _imageSearch.FindAsync(templateKey, searchOptions, ct);

                if (!searchResult.Success || searchResult.Location == null)
                {
                    return WindowOperationResult.Failed($"Image '{templateKey}' not found in window", startTime);
                }

                // Кликаем по найденному изображению
                var clickResult = await _input.Mouse.ClickAsync(MouseButton.Left, ct);

                var duration = DateTime.UtcNow - startTime;

                if (clickResult.Success)
                {
                    _logger.LogDebug("Successfully clicked on image {TemplateKey} at {Position} in window '{WindowTitle}' in {Duration}ms",
                        templateKey, searchResult.Location.Center, windowTitle, duration.TotalMilliseconds);

                    return WindowOperationResult.Success(duration, window, searchResult.Location.Center);
                }
                else
                {
                    return WindowOperationResult.Failed($"Failed to click: {clickResult.Error?.Message}", startTime);
                }
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                _logger.LogError(ex, "Error clicking on image {TemplateKey} in window '{WindowTitle}' after {Duration}ms",
                    templateKey, windowTitle, duration.TotalMilliseconds);
                return WindowOperationResult.Failed(ex.Message, startTime, ex);
            }
        }

        /// <summary>
        /// Печатает текст в указанном окне
        /// </summary>
        public async ValueTask<WindowOperationResult> TypeInWindowAsync(
            string windowTitle,
            string text,
            WindowAutomationOptions? options = null,
            CancellationToken ct = default)
        {
            options ??= WindowAutomationOptions.Default;
            var startTime = DateTime.UtcNow;

            _logger.LogDebug("Typing text in window '{WindowTitle}': {Text}", windowTitle,
                text.Length > 50 ? text[..50] + "..." : text);

            try
            {
                // Находим и активируем окно
                var window = await FindAndPrepareWindowAsync(windowTitle, options, ct);
                if (window == null)
                {
                    return WindowOperationResult.Failed($"Window '{windowTitle}' not found", startTime);
                }

                // Печатаем текст
                var typingOptions = new TypingOptions
                {
                    BaseDelay = options.TypingDelay,
                    UseHumanTiming = options.UseHumanInput
                };

                var typeResult = await _input.Keyboard.TypeAsync(text, typingOptions, ct);
                var duration = DateTime.UtcNow - startTime;

                if (typeResult.Success)
                {
                    _logger.LogDebug("Successfully typed {Length} characters in window '{WindowTitle}' in {Duration}ms",
                        text.Length, windowTitle, duration.TotalMilliseconds);

                    return WindowOperationResult.Success(duration, window);
                }
                else
                {
                    return WindowOperationResult.Failed($"Failed to type text: {typeResult.Error?.Message}", startTime);
                }
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                _logger.LogError(ex, "Error typing text in window '{WindowTitle}' after {Duration}ms",
                    windowTitle, duration.TotalMilliseconds);
                return WindowOperationResult.Failed(ex.Message, startTime, ex);
            }
        }

        /// <summary>
        /// Ждет появления изображения в окне и кликает по нему
        /// </summary>
        public async ValueTask<WindowOperationResult> WaitAndClickInWindowAsync(
            string windowTitle,
            string templateKey,
            TimeSpan timeout,
            WindowAutomationOptions? options = null,
            CancellationToken ct = default)
        {
            options ??= WindowAutomationOptions.Default;
            var startTime = DateTime.UtcNow;

            _logger.LogDebug("Waiting for image {TemplateKey} in window '{WindowTitle}' with timeout {Timeout}",
                templateKey, windowTitle, timeout);

            try
            {
                // Находим и подготавливаем окно
                var window = await FindAndPrepareWindowAsync(windowTitle, options, ct);
                if (window == null)
                {
                    return WindowOperationResult.Failed($"Window '{windowTitle}' not found", startTime);
                }

                // Ждем появления изображения
                var searchOptions = ImageSearchOptions.Default
                    .WithThreshold(options.MatchThreshold)
                    .InWindow(window.Handle);

                var searchResult = await _imageSearch.WaitForAsync(templateKey, timeout, searchOptions, ct);

                if (!searchResult.Success || searchResult.Location == null)
                {
                    var duration = DateTime.UtcNow - startTime;
                    return WindowOperationResult.Failed($"Image '{templateKey}' did not appear in window within {timeout}", startTime);
                }

                // Кликаем по найденному изображению
                var clickResult = await _input.Mouse.ClickAsync(MouseButton.Left, ct);
                var totalDuration = DateTime.UtcNow - startTime;

                if (clickResult.Success)
                {
                    _logger.LogDebug("Successfully waited and clicked on image {TemplateKey} in window '{WindowTitle}' after {Duration}ms",
                        templateKey, windowTitle, totalDuration.TotalMilliseconds);

                    return WindowOperationResult.Success(totalDuration, window, searchResult.Location.Center);
                }
                else
                {
                    return WindowOperationResult.Failed($"Failed to click after finding image: {clickResult.Error?.Message}", startTime);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                var duration = DateTime.UtcNow - startTime;
                _logger.LogDebug("Wait and click operation was cancelled after {Duration}ms", duration.TotalMilliseconds);
                return WindowOperationResult.Failed("Operation was cancelled", startTime);
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                _logger.LogError(ex, "Error waiting and clicking in window '{WindowTitle}' after {Duration}ms",
                    windowTitle, duration.TotalMilliseconds);
                return WindowOperationResult.Failed(ex.Message, startTime, ex);
            }
        }

        /// <summary>
        /// Выполняет комплексную операцию: клик по полю + ввод текста
        /// </summary>
        public async ValueTask<WindowOperationResult> ClickAndTypeInWindowAsync(
            string windowTitle,
            string fieldImageKey,
            string text,
            WindowAutomationOptions? options = null,
            CancellationToken ct = default)
        {
            options ??= WindowAutomationOptions.Default;
            var startTime = DateTime.UtcNow;

            _logger.LogDebug("Click and type operation in window '{WindowTitle}': field={FieldImage}, text length={TextLength}",
                windowTitle, fieldImageKey, text.Length);

            try
            {
                // Находим окно
                var window = await FindAndPrepareWindowAsync(windowTitle, options, ct);
                if (window == null)
                {
                    return WindowOperationResult.Failed($"Window '{windowTitle}' not found", startTime);
                }

                // Кликаем по полю
                var clickResult = await ClickOnImageInWindowAsync(windowTitle, fieldImageKey, options, ct);
                if (!clickResult.Success)
                {
                    return WindowOperationResult.Failed($"Failed to click on field: {clickResult.ErrorMessage}", startTime);
                }

                // Небольшая пауза для активации поля
                await Task.Delay(TimeSpan.FromMilliseconds(options.DelayAfterClick), ct);

                // Очищаем поле если нужно
                if (options.ClearFieldBeforeTyping)
                {
                    await _input.Keyboard.KeyCombinationAsync(new[] { VirtualKey.Control, VirtualKey.A }, ct);
                    await Task.Delay(50, ct);
                }

                // Вводим текст
                var typeResult = await TypeInWindowAsync(windowTitle, text, options, ct);
                var duration = DateTime.UtcNow - startTime;

                if (typeResult.Success)
                {
                    _logger.LogDebug("Successfully completed click and type operation in window '{WindowTitle}' in {Duration}ms",
                        windowTitle, duration.TotalMilliseconds);

                    return WindowOperationResult.Success(duration, window, clickResult.ClickPoint);
                }
                else
                {
                    return WindowOperationResult.Failed($"Failed to type after click: {typeResult.ErrorMessage}", startTime);
                }
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                _logger.LogError(ex, "Error in click and type operation in window '{WindowTitle}' after {Duration}ms",
                    windowTitle, duration.TotalMilliseconds);
                return WindowOperationResult.Failed(ex.Message, startTime, ex);
            }
        }

        /// <summary>
        /// Закрывает окно (мягко или принудительно)
        /// </summary>
        public async ValueTask<WindowOperationResult> CloseWindowAsync(
            string windowTitle,
            bool force = false,
            CancellationToken ct = default)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                var criteria = WindowSearchCriteria.WithTitle(windowTitle);
                var windows = await _windowManager.FindWindowsAsync(criteria, ct);

                if (windows.Count == 0)
                {
                    return WindowOperationResult.Failed($"Window '{windowTitle}' not found", startTime);
                }

                var window = windows[0];
                var operation = force ? WindowOperation.Close : WindowOperation.Close; // В реальности может быть разные способы

                var result = await _windowManager.PerformWindowOperationAsync(window.Handle, operation, ct);
                var duration = DateTime.UtcNow - startTime;

                if (result)
                {
                    _logger.LogDebug("Successfully closed window '{WindowTitle}' in {Duration}ms", windowTitle, duration.TotalMilliseconds);
                    return WindowOperationResult.Success(duration, window);
                }
                else
                {
                    return WindowOperationResult.Failed($"Failed to close window '{windowTitle}'", startTime);
                }
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                _logger.LogError(ex, "Error closing window '{WindowTitle}' after {Duration}ms", windowTitle, duration.TotalMilliseconds);
                return WindowOperationResult.Failed(ex.Message, startTime, ex);
            }
        }

        #region Private Methods

        private async ValueTask<WindowInfo?> FindAndPrepareWindowAsync(
            string windowTitle,
            WindowAutomationOptions options,
            CancellationToken ct)
        {
            // Находим окно
            var criteria = WindowSearchCriteria.WithTitle(windowTitle, options.ExactTitleMatch);
            var windows = await _windowManager.FindWindowsAsync(criteria, ct);

            if (windows.Count == 0)
            {
                _logger.LogWarning("Window '{WindowTitle}' not found", windowTitle);
                return null;
            }

            var window = windows[0];

            // Подготавливаем окно к работе
            if (options.ActivateWindow)
            {
                if (window.IsMinimized)
                {
                    await _windowManager.PerformWindowOperationAsync(window.Handle, WindowOperation.Restore, ct);
                    await Task.Delay(options.DelayAfterRestore, ct);
                }

                await _windowManager.PerformWindowOperationAsync(window.Handle, WindowOperation.BringToFront, ct);
                await Task.Delay(options.DelayAfterActivation, ct);
            }

            return window;
        }

        #endregion
    }

    #region Supporting Types

    /// <summary>
    /// Настройки автоматизации окон
    /// </summary>
    public sealed record WindowAutomationOptions
    {
        public static WindowAutomationOptions Default { get; } = new();

        /// <summary>Точное совпадение заголовка окна</summary>
        public bool ExactTitleMatch { get; init; } = false;

        /// <summary>Активировать окно перед операциями</summary>
        public bool ActivateWindow { get; init; } = true;

        /// <summary>Пороговое значение для поиска изображений</summary>
        public double MatchThreshold { get; init; } = 0.9;

        /// <summary>Использовать человекоподобный ввод</summary>
        public bool UseHumanInput { get; init; } = true;

        /// <summary>Задержка печати между символами</summary>
        public TimeSpan TypingDelay { get; init; } = TimeSpan.FromMilliseconds(50);

        /// <summary>Задержка после активации окна</summary>
        public TimeSpan DelayAfterActivation { get; init; } = TimeSpan.FromMilliseconds(100);

        /// <summary>Задержка после восстановления окна</summary>
        public TimeSpan DelayAfterRestore { get; init; } = TimeSpan.FromMilliseconds(200);

        /// <summary>Задержка после клика</summary>
        public int DelayAfterClick { get; init; } = 100;

        /// <summary>Очищать поле перед вводом текста</summary>
        public bool ClearFieldBeforeTyping { get; init; } = true;

        // Fluent API
        public WindowAutomationOptions WithThreshold(double threshold)
            => this with { MatchThreshold = threshold };

        public WindowAutomationOptions WithExactTitleMatch()
            => this with { ExactTitleMatch = true };

        public WindowAutomationOptions WithoutActivation()
            => this with { ActivateWindow = false };

        public WindowAutomationOptions WithFastInput()
            => this with { UseHumanInput = false, TypingDelay = TimeSpan.FromMilliseconds(10) };
    }

    /// <summary>
    /// Результат операции с окном
    /// </summary>
    public sealed record WindowOperationResult
    {
        public bool Success { get; init; }
        public TimeSpan Duration { get; init; }
        public WindowInfo? Window { get; init; }
        public Point? ClickPoint { get; init; }
        public string? ErrorMessage { get; init; }
        public Exception? Exception { get; init; }

        internal static WindowOperationResult Success(TimeSpan duration, WindowInfo window, Point? clickPoint = null)
            => new() { Success = true, Duration = duration, Window = window, ClickPoint = clickPoint };

        internal static WindowOperationResult Failed(string error, DateTime startTime, Exception? ex = null)
            => new()
            {
                Success = false,
                ErrorMessage = error,
                Exception = ex,
                Duration = DateTime.UtcNow - startTime
            };
    }

    #endregion
}