// Infrastructure/Capture/HighLevelCaptureSession.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutomationCore.Core.Abstractions;
using AutomationCore.Core.Models;
using Microsoft.Extensions.Logging;

namespace AutomationCore.Infrastructure.Capture
{
    /// <summary>
    /// Высокоуровневая сессия захвата - адаптер между ICaptureSession и ICaptureDeviceSession
    /// </summary>
    internal sealed class HighLevelCaptureSession : ICaptureSession
    {
        private readonly ICaptureDeviceSession _deviceSession;
        private readonly ILogger _logger;
        private readonly SemaphoreSlim _frameSemaphore = new(1, 1);
        private CaptureFrame? _lastFrame;
        private bool _disposed;

        public string SessionId { get; }
        public CaptureTarget Target => _deviceSession.Target;
        public bool IsActive => _deviceSession.IsActive && !_disposed;

        public event EventHandler<FrameCapturedEventArgs>? FrameCaptured;
        public event EventHandler<CaptureErrorEventArgs>? CaptureError;
        public event EventHandler? SessionEnded;

        internal HighLevelCaptureSession(string sessionId, ICaptureDeviceSession deviceSession, ILogger logger)
        {
            SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
            _deviceSession = deviceSession ?? throw new ArgumentNullException(nameof(deviceSession));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Подписываемся на события низкоуровневой сессии
            _deviceSession.FrameCaptured += OnDeviceFrameCaptured;
            _deviceSession.CaptureError += OnDeviceCaptureError;
            _deviceSession.SessionEnded += OnDeviceSessionEnded;

            // Автоматически запускаем сессию
            _ = Task.Run(async () =>
            {
                try
                {
                    await _deviceSession.StartAsync();
                    _logger.LogDebug("Session {SessionId} started successfully", SessionId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to start session {SessionId}", SessionId);
                    OnDeviceCaptureError(_deviceSession, new CaptureErrorEventArgs
                    {
                        Exception = ex,
                        Target = Target,
                        IsFatal = true
                    });
                }
            });
        }

        public async ValueTask<CaptureFrame> GetNextFrameAsync(CancellationToken ct = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(HighLevelCaptureSession));

            if (!IsActive)
                throw new InvalidOperationException("Session is not active");

            return await _deviceSession.CaptureFrameAsync(ct);
        }

        public async IAsyncEnumerable<CaptureFrame> GetFrameStreamAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(HighLevelCaptureSession));

            var frameReceived = new TaskCompletionSource<CaptureFrame>();
            var currentTcs = frameReceived;

            void OnFrameHandler(object? sender, FrameCapturedEventArgs e)
            {
                var tcs = Interlocked.Exchange(ref currentTcs, new TaskCompletionSource<CaptureFrame>());
                tcs.TrySetResult(e.Frame);
            }

            void OnErrorHandler(object? sender, CaptureErrorEventArgs e)
            {
                var tcs = Interlocked.Exchange(ref currentTcs, new TaskCompletionSource<CaptureFrame>());
                tcs.TrySetException(e.Exception);
            }

            FrameCaptured += OnFrameHandler;
            CaptureError += OnErrorHandler;

            try
            {
                while (!ct.IsCancellationRequested && IsActive)
                {
                    CaptureFrame frame;
                    try
                    {
                        frame = await currentTcs.Task.WaitAsync(ct);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    yield return frame;
                }
            }
            finally
            {
                FrameCaptured -= OnFrameHandler;
                CaptureError -= OnErrorHandler;
            }
        }

        public CaptureFrame? GetLastFrame()
        {
            return _lastFrame;
        }

        public CaptureMetrics GetMetrics()
        {
            return _deviceSession.GetMetrics();
        }

        public void Stop()
        {
            if (_disposed || !IsActive)
                return;

            try
            {
                _ = _deviceSession.StopAsync();
                _logger.LogDebug("Session {SessionId} stopped", SessionId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error stopping session {SessionId}", SessionId);
            }
        }

        private void OnDeviceFrameCaptured(object? sender, FrameCapturedEventArgs e)
        {
            if (_disposed)
                return;

            // Обновляем последний кадр
            _lastFrame = e.Frame;

            // Передаем событие наверх
            FrameCaptured?.Invoke(this, e);
        }

        private void OnDeviceCaptureError(object? sender, CaptureErrorEventArgs e)
        {
            if (_disposed)
                return;

            _logger.LogWarning(e.Exception, "Capture error in session {SessionId}: {Message}", SessionId, e.Exception.Message);

            // Передаем событие наверх
            CaptureError?.Invoke(this, e);

            if (e.IsFatal)
            {
                _logger.LogError("Fatal error in session {SessionId}, stopping session", SessionId);
                Stop();
            }
        }

        private void OnDeviceSessionEnded(object? sender, EventArgs e)
        {
            if (_disposed)
                return;

            _logger.LogDebug("Device session ended for {SessionId}", SessionId);
            SessionEnded?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                Stop();

                // Отписываемся от событий
                _deviceSession.FrameCaptured -= OnDeviceFrameCaptured;
                _deviceSession.CaptureError -= OnDeviceCaptureError;
                _deviceSession.SessionEnded -= OnDeviceSessionEnded;

                // Освобождаем ресурсы
                _deviceSession.Dispose();
                _frameSemaphore.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing session {SessionId}", SessionId);
            }

            _disposed = true;
        }
    }
}