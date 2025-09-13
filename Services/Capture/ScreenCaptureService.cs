// Services/Capture/ScreenCaptureService.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using AutomationCore.Core.Abstractions;
using AutomationCore.Core.Exceptions;
using AutomationCore.Core.Models;
using Microsoft.Extensions.Logging;

namespace AutomationCore.Services.Capture
{
    /// <summary>
    /// Основная реализация сервиса захвата экрана
    /// </summary>
    public sealed class ScreenCaptureService : IScreenCapture
    {
        private readonly ICaptureDevice _device;
        private readonly ICaptureSessionManager _sessionManager;
        private readonly ILogger<ScreenCaptureService> _logger;
        private bool _disposed;

        public bool IsSupported => _device.IsSupported;

        public ScreenCaptureService(
            ICaptureDevice device,
            ICaptureSessionManager sessionManager,
            ILogger<ScreenCaptureService> logger)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async ValueTask<CaptureResult> CaptureAsync(CaptureRequest request, CancellationToken ct = default)
        {
            ThrowIfDisposed();

            if (!IsSupported)
            {
                throw new CaptureNotSupportedException("Windows Graphics Capture is not available");
            }

            _logger.LogDebug("Starting single frame capture for target {Target}", request.Target.GetType().Name);

            var startTime = DateTime.UtcNow;

            try
            {
                // Создаем временную сессию для одиночного захвата
                using var session = await _device.CreateSessionAsync(request, ct);

                // Захватываем кадр с таймаутом
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(request.Options.CaptureTimeout);

                var frame = await session.CaptureFrameAsync(timeoutCts.Token);
                var duration = DateTime.UtcNow - startTime;

                _logger.LogDebug("Successfully captured frame {Width}x{Height} in {Duration}ms",
                    frame.Width, frame.Height, duration.TotalMilliseconds);

                return CaptureResult.Successful(frame, duration);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                _logger.LogDebug("Capture was cancelled by user");
                throw;
            }
            catch (OperationCanceledException)
            {
                var duration = DateTime.UtcNow - startTime;
                _logger.LogWarning("Capture timed out after {Duration}ms", duration.TotalMilliseconds);
                throw new CaptureTimeoutException(request.Options.CaptureTimeout, request.Target);
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                _logger.LogError(ex, "Capture failed after {Duration}ms", duration.TotalMilliseconds);
                return CaptureResult.Failed(new CaptureException("Failed to capture frame", ex, request.Target));
            }
        }

        public async ValueTask<ICaptureSession> StartSessionAsync(CaptureRequest request, CancellationToken ct = default)
        {
            ThrowIfDisposed();

            if (!IsSupported)
            {
                throw new CaptureNotSupportedException("Windows Graphics Capture is not available");
            }

            _logger.LogDebug("Starting capture session for target {Target}", request.Target.GetType().Name);

            try
            {
                var deviceSession = await _device.CreateSessionAsync(request, ct);
                var managedSession = _sessionManager.CreateManagedSession(deviceSession, request);

                _logger.LogDebug("Successfully created capture session");
                return managedSession;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start capture session");
                throw new CaptureException("Failed to start capture session", ex, request.Target);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ScreenCaptureService));
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _logger.LogDebug("Disposing ScreenCaptureService");

            _sessionManager?.Dispose();
            _device?.Dispose();

            _disposed = true;
        }
    }

    /// <summary>
    /// Интерфейс для низкоуровневого устройства захвата
    /// </summary>
    public interface ICaptureDevice : IDisposable
    {
        bool IsSupported { get; }
        ValueTask<ICaptureDeviceSession> CreateSessionAsync(CaptureRequest request, CancellationToken ct = default);
    }

    /// <summary>
    /// Низкоуровневая сессия захвата устройства
    /// </summary>
    public interface ICaptureDeviceSession : IDisposable
    {
        CaptureTarget Target { get; }
        bool IsActive { get; }
        ValueTask<CaptureFrame> CaptureFrameAsync(CancellationToken ct = default);
        void Start();
        void Stop();
    }

    /// <summary>
    /// Менеджер сессий захвата
    /// </summary>
    public interface ICaptureSessionManager : IDisposable
    {
        ICaptureSession CreateManagedSession(ICaptureDeviceSession deviceSession, CaptureRequest request);
        ValueTask<ICaptureSession?> GetSessionAsync(string sessionId, CancellationToken ct = default);
        ValueTask<bool> CloseSessionAsync(string sessionId, CancellationToken ct = default);
        ValueTask CloseAllSessionsAsync(CancellationToken ct = default);
    }
}