// Public/Facades/AutomationClient.cs
using AutomationCore.Core;
using AutomationCore.Core.Abstractions;
using AutomationCore.Core.Models;
using AutomationCore.Core.Configuration;
using AutomationCore.Features.ImageSearch;
using AutomationCore.Features.WindowAutomation;
using AutomationCore.Features.Workflows;
using AutomationCore.Public.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using WorkflowBuilder = AutomationCore.Features.Workflows.WorkflowBuilder;
using WindowInfo = AutomationCore.Core.Models.WindowInfo;
using WindowHandle = AutomationCore.Core.Models.WindowHandle;
using IWorkflowBuilder = AutomationCore.Features.Workflows.IWorkflowBuilder;

namespace AutomationCore.Public.Facades
{
    /// <summary>
    /// Главный клиент автоматизации - простой API для пользователей
    /// </summary>
    public sealed class AutomationClient : IDisposable
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<AutomationClient> _logger;
        private bool _disposed;

        // Высокоуровневые компоненты
        public ImageSearchEngine Images { get; }
        public WindowAutomator Windows { get; }
        public WorkflowBuilder Workflows { get; }

        // Низкоуровневые сервисы (для продвинутого использования)
        public IScreenCapture Capture { get; }
        public ITemplateMatcher Matcher { get; }
        public IInputSimulator Input { get; }
        public IWindowManager WindowManager { get; }

        private AutomationClient(IServiceProvider services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _logger = services.GetRequiredService<ILogger<AutomationClient>>();

            // Получаем сервисы
            Capture = services.GetRequiredService<IScreenCapture>();
            Matcher = services.GetRequiredService<ITemplateMatcher>();
            Input = services.GetRequiredService<IInputSimulator>();
            WindowManager = services.GetRequiredService<IWindowManager>();

            // Создаем высокоуровневые компоненты
            Images = services.GetRequiredService<ImageSearchEngine>();
            Windows = services.GetRequiredService<WindowAutomator>();
            Workflows = services.GetRequiredService<WorkflowBuilder>();

            _logger.LogInformation("AutomationClient initialized successfully");
        }

        /// <summary>
        /// Создает новый клиент автоматизации с настройками по умолчанию
        /// </summary>
        public static AutomationClient Create(Action<AutomationCore.Public.Configuration.AutomationOptions>? configure = null)
        {
            var options = new AutomationCore.Public.Configuration.AutomationOptions();
            configure?.Invoke(options);

            var services = new ServiceCollection();
            services.AddAutomationCore(configure);

            var provider = services.BuildServiceProvider();
            return new AutomationClient(provider);
        }

        #region Simple API

        /// <summary>
        /// Находит изображение на экране и кликает по нему
        /// </summary>
        /// <param name="templateKey">Ключ шаблона изображения</param>
        /// <param name="timeout">Максимальное время ожидания</param>
        /// <param name="ct">Токен отмены</param>
        /// <returns>True если изображение найдено и клик выполнен</returns>
        public async ValueTask<bool> ClickOnImageAsync(
            string templateKey,
            TimeSpan? timeout = null,
            CancellationToken ct = default)
        {
            ThrowIfDisposed();
            _logger.LogDebug("Attempting to click on image: {TemplateKey}", templateKey);

            try
            {
                // Захватываем экран
                var captureRequest = CaptureRequest.ForScreen();
                var captureResult = await Capture.CaptureAsync(captureRequest, ct);

                if (!captureResult.Success || captureResult.Frame == null)
                {
                    _logger.LogWarning("Failed to capture screen for image search");
                    return false;
                }

                // Ищем изображение
                var matchRequest = MatchRequest.Create(templateKey, captureResult.Frame.Value);
                var matchResult = await Matcher.FindBestMatchAsync(matchRequest, ct);

                if (!matchResult.Success || matchResult.BestMatch == null || !matchResult.BestMatch.Value.IsMatch)
                {
                    _logger.LogDebug("Image {TemplateKey} not found on screen", templateKey);
                    return false;
                }

                // Кликаем по центру найденного изображения
                var center = matchResult.BestMatch.Value.Center;
                var clickResult = await Input.Mouse.ClickAsync(MouseButton.Left, ct);

                if (clickResult.Success)
                {
                    _logger.LogDebug("Successfully clicked on image {TemplateKey} at {Position}", templateKey, center);
                    return true;
                }
                else
                {
                    _logger.LogWarning("Failed to click at position {Position}: {Error}", center, clickResult.Error?.Message);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while clicking on image {TemplateKey}", templateKey);
                return false;
            }
        }

        /// <summary>
        /// Ждет появления изображения на экране и кликает по нему
        /// </summary>
        public async ValueTask<bool> WaitAndClickOnImageAsync(
            string templateKey,
            TimeSpan timeout,
            CancellationToken ct = default)
        {
            ThrowIfDisposed();
            _logger.LogDebug("Waiting for image {TemplateKey} with timeout {Timeout}", templateKey, timeout);

            try
            {
                var waitRequest = WaitForMatchRequest.Create(templateKey, timeout);
                var result = await Matcher.WaitForMatchAsync(waitRequest, ct);

                if (!result.Success || result.BestMatch == null)
                {
                    _logger.LogDebug("Image {TemplateKey} did not appear within timeout", templateKey);
                    return false;
                }

                // Кликаем по найденному изображению
                var center = result.BestMatch.Value.Center;
                var clickResult = await Input.Mouse.ClickAsync(MouseButton.Left, ct);

                return clickResult.Success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while waiting and clicking on image {TemplateKey}", templateKey);
                return false;
            }
        }

        /// <summary>
        /// Печатает текст с человекоподобной скоростью
        /// </summary>
        public async ValueTask<bool> TypeAsync(
            string text,
            TypingOptions? options = null,
            CancellationToken ct = default)
        {
            ThrowIfDisposed();
            _logger.LogDebug("Typing text: {Text}", text.Length > 50 ? text[..50] + "..." : text);

            try
            {
                var result = await Input.Keyboard.TypeAsync(text, options, ct);
                if (result.Success)
                {
                    _logger.LogDebug("Successfully typed {Length} characters", text.Length);
                    return true;
                }
                else
                {
                    _logger.LogWarning("Failed to type text: {Error}", result.Error?.Message);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while typing text");
                return false;
            }
        }

        /// <summary>
        /// Перемещает мышь в указанную точку
        /// </summary>
        public async ValueTask<bool> MoveMouseToAsync(
            Point target,
            MouseMoveOptions? options = null,
            CancellationToken ct = default)
        {
            ThrowIfDisposed();

            try
            {
                var result = await Input.Mouse.MoveToAsync(target, options, ct);
                return result.Success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while moving mouse to {Target}", target);
                return false;
            }
        }

        /// <summary>
        /// Делает скриншот экрана
        /// </summary>
        public async ValueTask<byte[]?> TakeScreenshotAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();

            try
            {
                var request = CaptureRequest.ForScreen();
                var result = await Capture.CaptureAsync(request, ct);

                if (result.Success && result.Frame.HasValue)
                {
                    // Конвертируем кадр в byte array (упрощенная реализация)
                    return result.Frame.Value.Data.ToArray();
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while taking screenshot");
                return null;
            }
        }

        /// <summary>
        /// Находит окно по заголовку
        /// </summary>
        public async ValueTask<WindowInfo?> FindWindowAsync(
            string titlePattern,
            bool exactMatch = false,
            CancellationToken ct = default)
        {
            ThrowIfDisposed();

            try
            {
                var criteria = WindowSearchCriteria.WithTitle(titlePattern, exactMatch);
                var windows = await WindowManager.FindWindowsAsync(criteria, ct);

                return windows.Count > 0 ? windows[0] : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while finding window with title pattern {Pattern}", titlePattern);
                return null;
            }
        }

        /// <summary>
        /// Активирует окно
        /// </summary>
        public async ValueTask<bool> ActivateWindowAsync(
            WindowHandle handle,
            CancellationToken ct = default)
        {
            ThrowIfDisposed();

            try
            {
                return await WindowManager.PerformWindowOperationAsync(handle, WindowOperation.BringToFront, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while activating window {Handle}", handle);
                return false;
            }
        }

        #endregion

        #region Workflow API

        /// <summary>
        /// Создает новый workflow
        /// </summary>
        public IWorkflowBuilder CreateWorkflow(string name)
        {
            ThrowIfDisposed();
            return Workflows.CreateNew(name);
        }

        /// <summary>
        /// Создает простой workflow для клика по изображению
        /// </summary>
        public IWorkflowBuilder CreateClickWorkflow(string name, string imageKey, TimeSpan? timeout = null)
        {
            return CreateWorkflow(name)
                .WaitForImage(imageKey, timeout ?? TimeSpan.FromSeconds(10))
                .ClickOnImage(imageKey);
        }

        #endregion

        #region Diagnostics

        /// <summary>
        /// Проверяет состояние системы автоматизации
        /// </summary>
        public ValueTask<SystemStatus> GetSystemStatusAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();

            var status = new SystemStatus
            {
                IsCaptureSupported = Capture.IsSupported,
                IsInputSupported = true, // Упрощение
                TemplateStorageAccessible = true, // Нужна проверка
                Timestamp = DateTime.UtcNow
            };

            return ValueTask.FromResult(status);
        }

        /// <summary>
        /// Получает метрики производительности
        /// </summary>
        public ValueTask<PerformanceMetrics> GetPerformanceMetricsAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();

            // TODO: Собрать реальные метрики
            var metrics = new PerformanceMetrics
            {
                AverageCaptureTime = TimeSpan.FromMilliseconds(50),
                AverageMatchTime = TimeSpan.FromMilliseconds(100),
                AverageInputTime = TimeSpan.FromMilliseconds(20),
                TotalOperations = 0,
                SuccessRate = 1.0
            };

            return ValueTask.FromResult(metrics);
        }

        #endregion

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(AutomationClient));
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _logger.LogInformation("Disposing AutomationClient");

            try
            {
                // Dispose сервисов через ServiceProvider
                if (_services is IDisposable disposableServices)
                {
                    disposableServices.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during AutomationClient disposal");
            }

            _disposed = true;
        }
    }

    #region Supporting Types

    /// <summary>
    /// Состояние системы автоматизации
    /// </summary>
    public sealed record SystemStatus
    {
        public bool IsCaptureSupported { get; init; }
        public bool IsInputSupported { get; init; }
        public bool TemplateStorageAccessible { get; init; }
        public DateTime Timestamp { get; init; }

        public bool IsHealthy => IsCaptureSupported && IsInputSupported && TemplateStorageAccessible;
    }

    /// <summary>
    /// Метрики производительности
    /// </summary>
    public sealed record PerformanceMetrics
    {
        public TimeSpan AverageCaptureTime { get; init; }
        public TimeSpan AverageMatchTime { get; init; }
        public TimeSpan AverageInputTime { get; init; }
        public long TotalOperations { get; init; }
        public double SuccessRate { get; init; }
    }

    #endregion
}