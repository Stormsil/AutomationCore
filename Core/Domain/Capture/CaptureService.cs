using AutomationCore.Core.Abstractions;
using AutomationCore.Core.Services;
using AutomationCore.Core.Models;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace AutomationCore.Core.Capture
{
    /// <summary>
    /// Фасад для работы с захватом экрана
    /// </summary>
    public class CaptureService : ICaptureService
    {
        private readonly ICaptureFactory _captureFactory;
        private readonly IWindowService _windowService;
        private readonly ILogger<CaptureService> _logger;
        private readonly ConcurrentDictionary<string, ICaptureSession> _sessions;

        public CaptureService(
            ICaptureFactory captureFactory,
            IWindowService windowService,
            ILogger<CaptureService> logger)
        {
            _captureFactory = captureFactory;
            _windowService = windowService;
            _logger = logger;
            _sessions = new();
        }

        public async Task<CaptureResult> CaptureWindowAsync(
            string windowTitle,
            CaptureSettings settings = null)
        {
            _logger.LogInformation("Capturing window: {Title}", windowTitle);

            var windows = _windowService.FindWindows(
                WindowSearchCriteria.WithTitle(windowTitle));

            if (windows.Count == 0)
                throw new WindowNotFoundException($"Window '{windowTitle}' not found");
          
            var request = CaptureRequest.ForWindow(windows[0].Handle);
            request.Settings = settings ?? CaptureSettings.Default;


            using var capture = _captureFactory.CreateCapture();
            return await capture.CaptureAsync(request);
        }

        public async Task<string> StartSessionAsync(
            string sessionId,
            CaptureRequest request)
        {
            if (_sessions.ContainsKey(sessionId))
                throw new InvalidOperationException($"Session '{sessionId}' already exists");

            var capture = _captureFactory.CreateCapture();
            var session = await capture.StartSessionAsync(request);

            _sessions[sessionId] = session;
            _logger.LogInformation("Started capture session: {SessionId}", sessionId);

            return sessionId;
        }

        // ... остальные методы
    }
}