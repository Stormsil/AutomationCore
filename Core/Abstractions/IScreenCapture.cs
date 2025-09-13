// ========== Core/Abstractions/IScreenCapture.cs ==========
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AutomationCore.Capture;
using OpenCvSharp;

namespace AutomationCore.Core.Abstractions
{
    /// <summary>Основной интерфейс для захвата экрана.</summary>
    public interface IScreenCapture : IDisposable
    {
        Task<CaptureResult> CaptureAsync(CaptureRequest request);
        Task<ICaptureSession> StartSessionAsync(CaptureRequest request);
        bool IsSupported { get; }
    }

    // ---- Запрос/цели ----
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

    // ---- Результат кадра в удобном формате (BGRA 8bpp) ----
    public class CaptureResult
    {
        public byte[] Data { get; set; } = Array.Empty<byte>(); // BGRA
        public int Width { get; set; }
        public int Height { get; set; }
        public int Stride { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public Mat ToMat()
        {
            if (Data is null || Data.Length == 0) return new Mat();

            GCHandle handle = default;
            try
            {
                handle = GCHandle.Alloc(Data, GCHandleType.Pinned);
                using var bgra = Mat.FromPixelData(Height, Width, MatType.CV_8UC4, handle.AddrOfPinnedObject(), Stride);
                var bgr = new Mat();
                Cv2.CvtColor(bgra, bgr, ColorConversionCodes.BGRA2BGR);
                return bgr;
            }
            finally
            {
                if (handle.IsAllocated) handle.Free();
            }
        }

        public Bitmap ToBitmap()
        {
            if (Data is null || Data.Length == 0) return new Bitmap(1, 1);

            var bmp = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, Width, Height);
            var bmpData = bmp.LockBits(rect, ImageLockMode.WriteOnly, bmp.PixelFormat);

            try
            {
                int rowBytes = Width * 4;
                unsafe
                {
                    fixed (byte* pSrcBase = Data)
                    {
                        var src = pSrcBase;
                        var dst = (byte*)bmpData.Scan0;
                        for (int y = 0; y < Height; y++)
                        {
                            Buffer.MemoryCopy(src + y * Stride, dst + y * bmpData.Stride, bmpData.Stride, rowBytes);
                        }
                    }
                }
            }
            finally
            {
                bmp.UnlockBits(bmpData);
            }
            return bmp;
        }
    }

    // ---- Сессия стрим-захвата ----
    public interface ICaptureSession : IDisposable
    {
        void OnFrameCaptured(EventHandler<FrameCapturedEventArgs> handler);
        Task<CaptureFrame> GetNextFrameAsync(CancellationToken cancellationToken = default);
        CaptureFrame GetLastFrame();
        CaptureMetrics GetMetrics();
        void Stop();
    }
}
