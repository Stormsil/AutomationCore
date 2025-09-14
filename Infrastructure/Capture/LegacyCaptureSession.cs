// Legacy CaptureSession (extracted from monolith)
// Адаптер между старым API и новой архитектурой
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AutomationCore.Core.Abstractions;
using AutomationCore.Core.Models;

namespace AutomationCore.Infrastructure.Capture
{
    /// <summary>
    /// Сессия захвата для обратной совместимости (legacy)
    /// </summary>
    public class LegacyCaptureSession : ICaptureSession
    {
        private readonly EnhancedWindowsGraphicsCapture _capture;
        private readonly IntPtr _windowHandle;
        private bool _disposed = false;

        public string SessionId { get; }
        public CaptureTarget Target { get; }
        public bool IsActive => _capture != null && !_disposed;

        public event EventHandler<FrameCapturedEventArgs>? FrameCaptured;
        public event EventHandler<CaptureErrorEventArgs>? CaptureError;
        public event EventHandler? SessionEnded;

        internal LegacyCaptureSession(EnhancedWindowsGraphicsCapture capture, IntPtr windowHandle)
        {
            _capture = capture ?? throw new ArgumentNullException(nameof(capture));
            _windowHandle = windowHandle;
            SessionId = Guid.NewGuid().ToString();
            Target = new WindowCaptureTarget(new WindowHandle(windowHandle));

            // Подключаем события
            _capture.FrameCaptured += OnFrameCaptured;
            _capture.CaptureError += OnCaptureError;
        }

        private void OnFrameCaptured(object? sender, FrameCapturedEventArgs e)
        {
            FrameCaptured?.Invoke(this, e);
        }

        private void OnCaptureError(object? sender, CaptureErrorEventArgs e)
        {
            CaptureError?.Invoke(this, e);
        }

        /// <summary>Получает следующий кадр</summary>
        public async ValueTask<CaptureFrame> GetNextFrameAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(LegacyCaptureSession));

            return await _capture.GetNextFrameAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Получает поток кадров</summary>
        public async IAsyncEnumerable<CaptureFrame> GetFrameStreamAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(LegacyCaptureSession));

            while (!cancellationToken.IsCancellationRequested && IsActive)
            {
                var frame = await GetNextFrameAsync(cancellationToken).ConfigureAwait(false);
                if (frame != null)
                    yield return frame;

                // Небольшая задержка между кадрами
                await Task.Delay(16, cancellationToken).ConfigureAwait(false); // ~60 FPS
            }
        }

        /// <summary>Получает последний кадр</summary>
        public CaptureFrame? GetLastFrame() => _capture?.GetLastFrame();

        /// <summary>Получает метрики</summary>
        public CaptureMetrics GetMetrics() => _capture?.GetMetrics() ?? new CaptureMetrics();

        /// <summary>Останавливает захват</summary>
        public void Stop()
        {
            if (!_disposed)
            {
                _capture?.StopCapture();
                SessionEnded?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            Stop();

            // Отключаем события
            if (_capture != null)
            {
                _capture.FrameCaptured -= OnFrameCaptured;
                _capture.CaptureError -= OnCaptureError;
                _capture?.Dispose();
            }

            _disposed = true;
        }
    }
}