// Infrastructure/Capture/CaptureSessionManager.cs
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutomationCore.Core.Abstractions;
using AutomationCore.Core.Models;
using AutomationCore.Infrastructure.Capture.WindowsGraphicsCapture;
using Microsoft.Extensions.Logging;

namespace AutomationCore.Infrastructure.Capture
{
    /// <summary>
    /// Менеджер сессий захвата - управляет множественными сессиями и выбирает устройство захвата
    /// </summary>
    public sealed class CaptureSessionManager : ICaptureSessionManager
    {
        private readonly ICaptureDevice[] _captureDevices;
        private readonly ILogger<CaptureSessionManager> _logger;
        private readonly ConcurrentDictionary<string, HighLevelCaptureSession> _sessions = new();
        private bool _disposed;

        public CaptureSessionManager(ILogger<CaptureSessionManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Инициализируем доступные устройства захвата
            _captureDevices = new ICaptureDevice[]
            {
                new WgcDevice() // В будущем можно добавить другие технологии
            };

            _logger.LogInformation("CaptureSessionManager initialized with {DeviceCount} capture devices", _captureDevices.Length);
        }

        public async ValueTask<ICaptureSession> CreateSessionAsync(CaptureRequest request, CancellationToken ct = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(CaptureSessionManager));

            _logger.LogDebug("Creating capture session for target {TargetType}", request.Target.GetType().Name);

            // Находим подходящее устройство захвата
            var device = FindSuitableDevice(request);
            if (device == null)
            {
                throw new NotSupportedException($"No suitable capture device found for target {request.Target.GetType().Name}");
            }

            // Создаем низкоуровневую сессию
            var deviceSession = await device.CreateSessionAsync(request, ct);

            // Оборачиваем в высокоуровневую сессию
            var sessionId = Guid.NewGuid().ToString();
            var session = new HighLevelCaptureSession(sessionId, deviceSession, _logger);

            // Регистрируем сессию
            _sessions[sessionId] = session;

            // Подписываемся на завершение сессии для очистки
            session.SessionEnded += (s, e) =>
            {
                if (s is HighLevelCaptureSession endedSession)
                {
                    _sessions.TryRemove(endedSession.SessionId, out _);
                    _logger.LogDebug("Session {SessionId} removed from registry", endedSession.SessionId);
                }
            };

            _logger.LogDebug("Created session {SessionId} for {TargetType}", sessionId, request.Target.GetType().Name);
            return session;
        }

        public ICaptureSession[] GetActiveSessions()
        {
            return _sessions.Values.Where(s => s.IsActive).ToArray<ICaptureSession>();
        }

        public async ValueTask StopAllSessionsAsync(CancellationToken ct = default)
        {
            _logger.LogInformation("Stopping all active sessions");

            var sessions = _sessions.Values.ToArray();
            var tasks = sessions.Select(s => StopSessionSafelyAsync(s, ct));

            await Task.WhenAll(tasks);

            _sessions.Clear();
            _logger.LogInformation("All sessions stopped");
        }

        private async Task StopSessionSafelyAsync(HighLevelCaptureSession session, CancellationToken ct)
        {
            try
            {
                session.Stop();
                await Task.Delay(100, ct); // Даем время на корректное завершение
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error stopping session {SessionId}", session.SessionId);
            }
        }

        private ICaptureDevice? FindSuitableDevice(CaptureRequest request)
        {
            // Выбираем устройство в порядке приоритета
            return _captureDevices.FirstOrDefault(device => device.IsSupported);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                // Останавливаем все сессии синхронно
                var sessions = _sessions.Values.ToArray();
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

                _sessions.Clear();

                // Освобождаем устройства
                foreach (var device in _captureDevices)
                {
                    try
                    {
                        device.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error disposing capture device {DeviceType}", device.GetType().Name);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during CaptureSessionManager disposal");
            }

            _disposed = true;
        }
    }
}