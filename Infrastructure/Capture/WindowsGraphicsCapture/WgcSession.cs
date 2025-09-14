// Infrastructure/Capture/WindowsGraphicsCapture/WgcSession.cs - заглушка для компиляции
using System;
using AutomationCore.Core.Abstractions;
using AutomationCore.Core.Models;

namespace AutomationCore.Infrastructure.Capture.WindowsGraphicsCapture
{
    /// <summary>
    /// Заглушка WgcSession - будет реализована позже
    /// </summary>
    internal sealed class WgcSession : ICaptureDeviceSession
    {
        public CaptureTarget Target { get; }
        public System.Drawing.Size CaptureSize { get; }
        public bool IsActive { get; }

        public event EventHandler<FrameCapturedEventArgs>? FrameCaptured;
        public event EventHandler<CaptureErrorEventArgs>? CaptureError;
        public event EventHandler? SessionEnded;

        internal WgcSession(object d3d11Device, object winrtDevice, object captureItem, CaptureTarget target, CaptureOptions options)
        {
            Target = target;
            // TODO: Реализовать позже
        }

        public System.Threading.Tasks.ValueTask StartAsync(System.Threading.CancellationToken ct = default)
            => throw new NotImplementedException();

        public System.Threading.Tasks.ValueTask StopAsync(System.Threading.CancellationToken ct = default)
            => throw new NotImplementedException();

        public System.Threading.Tasks.ValueTask<CaptureFrame> CaptureFrameAsync(System.Threading.CancellationToken ct = default)
            => throw new NotImplementedException();

        public CaptureMetrics GetMetrics()
            => throw new NotImplementedException();

        public void Dispose() { }
    }
}