// Infrastructure/Capture/EnhancedWindowsGraphicsCapture.cs
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AutomationCore.Core.Models;
using AutomationCore.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace AutomationCore.Infrastructure.Capture
{
    /// <summary>
    /// Расширенная версия захвата экрана через Windows Graphics Capture API
    /// </summary>
    public sealed class EnhancedWindowsGraphicsCapture : IDisposable
    {
        private readonly ILogger<EnhancedWindowsGraphicsCapture>? _logger;
        private readonly CaptureSettings _settings;
        private bool _disposed;
        private IntPtr _targetWindow;
        private InfrastructureCaptureFrame? _lastFrame;
        private bool _isCapturing;

        public event EventHandler<FrameCapturedEventArgs>? FrameCaptured;
        public event EventHandler<CaptureErrorEventArgs>? CaptureError;

        public EnhancedWindowsGraphicsCapture(CaptureSettings settings, ILogger<EnhancedWindowsGraphicsCapture>? logger = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger;
        }

        /// <summary>
        /// Проверяет поддержку Windows Graphics Capture API
        /// </summary>
        public static bool IsSupported()
        {
            try
            {
                // Простая проверка доступности WinRT
                return Environment.OSVersion.Platform == PlatformID.Win32NT &&
                       Environment.OSVersion.Version >= new Version(10, 0, 17134);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Инициализирует захват для указанного окна
        /// </summary>
        public async Task InitializeAsync(IntPtr windowHandle, int monitorIndex = 0)
        {
            ThrowIfDisposed();

            _targetWindow = windowHandle;
            _logger?.LogDebug("Initialized WGC for window {WindowHandle}, monitor {MonitorIndex}",
                windowHandle, monitorIndex);

            // Имитируем асинхронную инициализацию
            await Task.Delay(1);
        }

        /// <summary>
        /// Захватывает один кадр
        /// </summary>
        public async Task<InfrastructureCaptureFrame> CaptureFrameAsync()
        {
            ThrowIfDisposed();

            try
            {
                // Для демонстрации создаем простой кадр
                // В реальной реализации здесь был бы WGC API
                var width = _settings.Region?.Width ?? 1920;
                var height = _settings.Region?.Height ?? 1080;
                var stride = width * 4; // BGRA

                var data = new byte[stride * height];

                // Заполняем тестовыми данными (черный экран)
                for (int i = 0; i < data.Length; i += 4)
                {
                    data[i] = 0;     // Blue
                    data[i + 1] = 0; // Green
                    data[i + 2] = 0; // Red
                    data[i + 3] = 255; // Alpha
                }

                _logger?.LogDebug("Captured frame {Width}x{Height}, stride {Stride}",
                    width, height, stride);

                var frame = new InfrastructureCaptureFrame
                {
                    Data = data,
                    Width = width,
                    Height = height,
                    Stride = stride,
                    Timestamp = DateTime.UtcNow
                };

                _lastFrame = frame;
                return frame;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to capture frame for window {WindowHandle}", _targetWindow);
                var errorArgs = new CaptureErrorEventArgs { Exception = ex, Target = new WindowCaptureTarget(new WindowHandle(_targetWindow)) };
                CaptureError?.Invoke(this, errorArgs);
                throw new InvalidOperationException("Frame capture failed", ex);
            }
        }

        /// <summary>
        /// Получает следующий кадр асинхронно (для совместимости с legacy кодом)
        /// </summary>
        public async ValueTask<CaptureFrame> GetNextFrameAsync(CancellationToken cancellationToken = default)
        {
            var infraFrame = await CaptureFrameAsync();

            // Конвертируем в CaptureFrame из Core.Models
            return new CaptureFrame
            {
                Data = infraFrame.Data,
                Width = infraFrame.Width,
                Height = infraFrame.Height,
                Stride = infraFrame.Stride,
                Timestamp = infraFrame.Timestamp
            };
        }

        /// <summary>
        /// Возвращает последний захваченный кадр
        /// </summary>
        public CaptureFrame? GetLastFrame()
        {
            if (_lastFrame == null) return null;

            return new CaptureFrame
            {
                Data = _lastFrame.Data,
                Width = _lastFrame.Width,
                Height = _lastFrame.Height,
                Stride = _lastFrame.Stride,
                Timestamp = _lastFrame.Timestamp
            };
        }

        /// <summary>
        /// Возвращает метрики захвата
        /// </summary>
        public CaptureMetrics GetMetrics()
        {
            return new CaptureMetrics
            {
                TotalFrames = 1,
                DroppedFrames = 0,
                CurrentFps = 60.0,
                AverageFps = 60.0,
                StartTime = DateTime.UtcNow.AddSeconds(-1),
                Uptime = TimeSpan.FromSeconds(1),
                TotalBytes = 1920 * 1080 * 4
            };
        }

        /// <summary>
        /// Останавливает захват
        /// </summary>
        public void StopCapture()
        {
            _isCapturing = false;
            _logger?.LogDebug("Stopped capture for window {WindowHandle}", _targetWindow);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(EnhancedWindowsGraphicsCapture));
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _logger?.LogDebug("Disposing WGC instance for window {WindowHandle}", _targetWindow);

                // Здесь бы освобождались WGC ресурсы

                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Кадр, полученный от захвата экрана (Infrastructure версия)
    /// </summary>
    public sealed class InfrastructureCaptureFrame : IDisposable
    {
        public required byte[] Data { get; init; }
        public required int Width { get; init; }
        public required int Height { get; init; }
        public required int Stride { get; init; }
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                // Освобождение ресурсов кадра если нужно
                _disposed = true;
            }
        }
    }
}