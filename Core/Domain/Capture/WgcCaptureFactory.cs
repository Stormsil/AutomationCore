// AutomationCore/Core/Capture/WgcCaptureFactory.cs
using System;
using System.Drawing;
using System.Threading.Tasks;
using AutomationCore.Core.Abstractions;
using AutomationCore.Core.Models;
using AutomationCore.Infrastructure.Capture;
using AutomationCore.Core.Domain.Capture;
using Microsoft.Extensions.Logging;
using System.Threading;

namespace AutomationCore.Core.Capture
{
    /// <summary>Фабрика, выдающая адаптер над EnhancedScreenCapture под IScreenCapture.</summary>
    public class WgcCaptureFactory : ICaptureFactory
    {
        private readonly ILogger<WgcScreenCapture>? _logger;

        public WgcCaptureFactory(ILogger<WgcScreenCapture>? logger = null)
        {
            _logger = logger;
        }

        public IScreenCapture CreateCapture() => new WgcScreenCapture(_logger);
    }

    /// <summary>
    /// Лёгкий адаптер IScreenCapture над EnhancedWindowsGraphicsCapture/EnhancedScreenCapture.
    /// Для одиночного кадра используем WGC напрямую; для стримов — EnhancedScreenCapture.
    /// </summary>
    public sealed class WgcScreenCapture : IScreenCapture
    {
        private readonly ILogger<WgcScreenCapture> _logger;

        public WgcScreenCapture(ILogger<WgcScreenCapture>? logger = null)
        {
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<WgcScreenCapture>.Instance;
        }

        public bool IsSupported => EnhancedWindowsGraphicsCapture.IsSupported();

        public async ValueTask<CaptureResult> CaptureAsync(CaptureRequest request, CancellationToken ct = default)
        {
            var options = request.Options;

            // Temporary stub implementation - needs proper WGC capture
            throw new NotImplementedException("WGC capture factory needs proper implementation");
        }

        public async ValueTask<ICaptureSession> StartSessionAsync(CaptureRequest request, CancellationToken ct = default)
        {
            var options = request.Options;

            // Temporary stub implementation - needs proper WGC session
            throw new NotImplementedException("WGC capture session factory needs proper implementation");
        }

        public void Dispose() { /* no-op */ }
    }
}
