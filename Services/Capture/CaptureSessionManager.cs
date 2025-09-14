// Services/Capture/CaptureSessionManager.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AutomationCore.Core.Abstractions;
using AutomationCore.Core.Models;
using Microsoft.Extensions.Logging;

namespace AutomationCore.Services.Capture
{
    /// <summary>
    /// Менеджер сессий захвата с автоматической очисткой
    /// </summary>
    public sealed class CaptureSessionManager : ICaptureSessionManager
    {
        private readonly ConcurrentDictionary<string, ManagedCaptureSession> _sessions = new();
        private readonly ILogger<CaptureSessionManager> _logger;
        private readonly System.Threading.Timer _cleanupTimer;
        private bool _disposed;

        public CaptureSessionManager(ILogger<CaptureSessionManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Очистка неактивных сессий каждые 30 секунд
            _cleanupTimer = new System.Threading.Timer(CleanupInactiveSessions, null,
                TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }

        public ICaptureSession CreateManagedSession(ICaptureDeviceSession deviceSession, CaptureRequest request)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(CaptureSessionManager));

            var sessionId = Guid.NewGuid().ToString("N");
            var managedSession = new ManagedCaptureSession(sessionId, deviceSession, request, _logger);

            _sessions[sessionId] = managedSession;
            _logger.LogDebug("Created managed session {SessionId}", sessionId);

            return managedSession;
        }

        public ValueTask<ICaptureSession?> GetSessionAsync(string sessionId, CancellationToken ct = default)
        {
            var session = _sessions.TryGetValue(sessionId, out var managedSession) ? managedSession : null;
            return ValueTask.FromResult<ICaptureSession?>(session);
        }

        public ValueTask<bool> CloseSessionAsync(string sessionId, CancellationToken ct = default)
        {
            if (_sessions.TryRemove(sessionId, out var session))
            {
                _logger.LogDebug("Closing session {SessionId}", sessionId);
                session.Dispose();
                return ValueTask.FromResult(true);
            }

            return ValueTask.FromResult(false);
        }

        public async ValueTask CloseAllSessionsAsync(CancellationToken ct = default)
        {
            _logger.LogDebug("Closing all capture sessions");

            var sessions = _sessions.Values.ToArray();
            _sessions.Clear();

            await Task.Run(() =>
            {
                foreach (var session in sessions)
                {
                    try
                    {
                        session.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error disposing session {SessionId}", session.SessionId);
                    }
                }
            }, ct);
        }

        private void CleanupInactiveSessions(object? state)
        {
            if (_disposed) return;

            var inactiveSessions = new List<string>();

            foreach (var kvp in _sessions)
            {
                if (!kvp.Value.IsActive)
                {
                    inactiveSessions.Add(kvp.Key);
                }
            }

            foreach (var sessionId in inactiveSessions)
            {
                if (_sessions.TryRemove(sessionId, out var session))
                {
                    _logger.LogDebug("Cleaning up inactive session {SessionId}", sessionId);
                    session.Dispose();
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _logger.LogDebug("Disposing CaptureSessionManager");

            _cleanupTimer?.Dispose();

            try
            {
                CloseAllSessionsAsync().AsTask().Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during disposal");
            }

            _disposed = true;
        }

        /// <summary>
        /// Управляемая сессия захвата с дополнительными возможностями
        /// </summary>
        private sealed class ManagedCaptureSession : ICaptureSession
        {
            private readonly ICaptureDeviceSession _deviceSession;
            private readonly CaptureRequest _request;
            private readonly ILogger _logger;
            private readonly CaptureMetricsCollector _metrics;
            private bool _disposed;

            public string SessionId { get; }
            public CaptureTarget Target => _deviceSession.Target;
            public bool IsActive => _deviceSession.IsActive && !_disposed;

            // События
            public event EventHandler<FrameCapturedEventArgs>? FrameCaptured;
            public event EventHandler<CaptureErrorEventArgs>? CaptureError;

            public ManagedCaptureSession(
                string sessionId,
                ICaptureDeviceSession deviceSession,
                CaptureRequest request,
                ILogger logger)
            {
                SessionId = sessionId;
                _deviceSession = deviceSession;
                _request = request;
                _logger = logger;
                _metrics = new CaptureMetricsCollector();
            }

            public async ValueTask<CaptureFrame> GetNextFrameAsync(CancellationToken ct = default)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(ManagedCaptureSession));

                try
                {
                    var frame = await _deviceSession.CaptureFrameAsync(ct);
                    _metrics.RecordFrame(frame);

                    FrameCaptured?.Invoke(this, new FrameCapturedEventArgs
                    {
                        Frame = frame,
                        Metrics = _metrics.GetSnapshot()
                    });

                    return frame;
                }
                catch (Exception ex)
                {
                    _metrics.RecordError();

                    CaptureError?.Invoke(this, new CaptureErrorEventArgs
                    {
                        Exception = ex,
                        Target = Target,
                        IsFatal = false
                    });

                    throw;
                }
            }

            public async IAsyncEnumerable<CaptureFrame> GetFrameStreamAsync(
                [EnumeratorCancellation] CancellationToken ct = default)
            {
                while (IsActive && !ct.IsCancellationRequested)
                {
                    CaptureFrame frame;
                    try
                    {
                        frame = await GetNextFrameAsync(ct);
                    }
                    catch (OperationCanceledException)
                    {
                        yield break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error in frame stream for session {SessionId}", SessionId);
                        continue; // Продолжаем стрим, не прерываем из-за одной ошибки
                    }

                    yield return frame;
                }
            }

            public CaptureFrame? GetLastFrame()
            {
                // TODO: Реализовать хранение последнего кадра
                return null;
            }

            public CaptureMetrics GetMetrics() => _metrics.GetSnapshot();

            public void Stop()
            {
                if (!_disposed)
                {
                    _logger.LogDebug("Stopping session {SessionId}", SessionId);
                    _deviceSession.Stop();
                }
            }

            public void Dispose()
            {
                if (_disposed) return;

                _logger.LogDebug("Disposing session {SessionId}", SessionId);

                try
                {
                    _deviceSession?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disposing device session");
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// Сборщик метрик захвата
        /// </summary>
        private sealed class CaptureMetricsCollector
        {
            private long _totalFrames;
            private long _totalErrors;
            private long _totalBytes;
            private double _currentFps;
            private double _averageFps;
            private readonly DateTime _startTime = DateTime.UtcNow;

            private DateTime _lastFrameTime = DateTime.UtcNow;
            private readonly object _lock = new();

            public void RecordFrame(CaptureFrame frame)
            {
                lock (_lock)
                {
                    var now = DateTime.UtcNow;
                    var elapsed = (now - _lastFrameTime).TotalSeconds;

                    _totalFrames++;
                    _totalBytes += frame.Data.Length;

                    if (elapsed > 0)
                    {
                        _currentFps = 1.0 / elapsed;
                        _averageFps = _totalFrames / (now - _startTime).TotalSeconds;
                    }

                    _lastFrameTime = now;
                }
            }

            public void RecordError()
            {
                lock (_lock)
                {
                    _totalErrors++;
                }
            }

            public CaptureMetrics GetSnapshot()
            {
                lock (_lock)
                {
                    return new CaptureMetrics
                    {
                        TotalFrames = _totalFrames,
                        DroppedFrames = _totalErrors, // Упрощение для примера
                        CurrentFps = _currentFps,
                        AverageFps = _averageFps,
                        StartTime = _startTime,
                        Uptime = DateTime.UtcNow - _startTime,
                        TotalBytes = _totalBytes
                    };
                }
            }
        }
    }
}