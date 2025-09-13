// ========== Core/Abstractions/IScreenCapture.cs ==========
using AutomationCore.Capture;
using OpenCvSharp;
using System;
using System.Windows.Media;

namespace AutomationCore.Core.Abstractions
{
    /// <summary>
    /// Основной интерфейс для захвата экрана
    /// </summary>
    public interface IScreenCapture : IDisposable
    {
        Task<CaptureResult> CaptureAsync(CaptureRequest request);
        Task<ICaptureSession> StartSessionAsync(CaptureRequest request);
        bool IsSupported { get; }
    }

    /// <summary>
    /// Запрос на захват с типизированными параметрами
    /// </summary>
    public class CaptureRequest
    {
        public CaptureTarget Target { get; set; }
        public CaptureSettings Settings { get; set; }
        public CancellationToken CancellationToken { get; set; }

        public static CaptureRequest ForWindow(WindowHandle window)
            => new() { Target = new WindowTarget(window) };

        public static CaptureRequest ForScreen(int monitorIndex = 0)
            => new() { Target = new ScreenTarget(monitorIndex) };
    }

    /// <summary>
    /// Результат захвата с метаданными
    /// </summary>
    public class CaptureResult
    {
        public byte[] Data { get; set; }
        public ImageMetadata Metadata { get; set; }
        public CaptureStatistics Statistics { get; set; }

        public Mat ToMat() => MatConverter.FromBytes(Data, Metadata);
        public Bitmap ToBitmap() => BitmapConverter.FromBytes(Data, Metadata);
    }

    public abstract class CaptureTarget { }

    public class WindowTarget : CaptureTarget
    {
        public WindowHandle Window { get; }
        public WindowTarget(WindowHandle window) => Window = window;
    }

    public class ScreenTarget : CaptureTarget
    {
        public int MonitorIndex { get; }
        public ScreenTarget(int index) => MonitorIndex = index;
    }
}