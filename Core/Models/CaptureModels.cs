// Core/Models/CaptureModels.cs
using System;
using System.Drawing;
using Windows.Graphics.DirectX;

namespace AutomationCore.Core.Models
{
    /// <summary>
    /// Кадр захвата с автоматическим управлением памятью
    /// </summary>
    public readonly record struct CaptureFrame(
        ReadOnlyMemory<byte> Data,
        int Width,
        int Height,
        int Stride,
        DateTime Timestamp)
    {
        public bool IsEmpty => Data.IsEmpty || Width <= 0 || Height <= 0;
        public int BytesPerPixel => Stride / Width;
        public Size Size => new(Width, Height);
    }

    /// <summary>
    /// Запрос на захват экрана
    /// </summary>
    public sealed record CaptureRequest(
        CaptureTarget Target,
        CaptureOptions Options)
    {
        public static CaptureRequest ForWindow(WindowHandle window, CaptureOptions? options = null)
            => new(new WindowCaptureTarget(window), options ?? CaptureOptions.Default);

        public static CaptureRequest ForScreen(int monitorIndex = 0, CaptureOptions? options = null)
            => new(new ScreenCaptureTarget(monitorIndex), options ?? CaptureOptions.Default);

        public static CaptureRequest ForRegion(Rectangle region, CaptureOptions? options = null)
            => new(new RegionCaptureTarget(region), options ?? CaptureOptions.Default);
    }

    /// <summary>
    /// Базовый класс для целей захвата
    /// </summary>
    public abstract record CaptureTarget;

    /// <summary>
    /// Захват окна
    /// </summary>
    public sealed record WindowCaptureTarget(WindowHandle Handle) : CaptureTarget;

    /// <summary>
    /// Захват экрана/монитора
    /// </summary>
    public sealed record ScreenCaptureTarget(int MonitorIndex = 0) : CaptureTarget;

    /// <summary>
    /// Захват региона экрана
    /// </summary>
    public sealed record RegionCaptureTarget(Rectangle Region) : CaptureTarget;

    /// <summary>
    /// Настройки захвата
    /// </summary>
    public sealed record CaptureOptions
    {
        public static CaptureOptions Default { get; } = new();

        public DirectXPixelFormat PixelFormat { get; init; } = DirectXPixelFormat.B8G8R8A8UIntNormalized;
        public int FramePoolSize { get; init; } = 2;
        public int TargetFps { get; init; } = 60;
        public TimeSpan CaptureTimeout { get; init; } = TimeSpan.FromSeconds(5);
        public bool AutoTrackWindow { get; init; } = true;
        public bool UseHardwareAcceleration { get; init; } = true;
        public bool? IsCursorCaptureEnabled { get; init; }
        public bool? IsBorderRequired { get; init; }
        public Rectangle? RegionOfInterest { get; init; }

        public static CaptureOptions HighPerformance => Default with
        {
            FramePoolSize = 3,
            TargetFps = 144,
            UseHardwareAcceleration = true
        };

        public static CaptureOptions LowLatency => Default with
        {
            FramePoolSize = 1,
            TargetFps = 30,
            CaptureTimeout = TimeSpan.FromSeconds(1)
        };
    }

    /// <summary>
    /// Результат операции захвата
    /// </summary>
    public sealed record CaptureResult
    {
        public bool Success { get; init; } = true;
        public CaptureFrame? Frame { get; init; }
        public Exception? Error { get; init; }
        public TimeSpan Duration { get; init; }

        public static CaptureResult Successful(CaptureFrame frame, TimeSpan duration)
            => new() { Frame = frame, Duration = duration };

        public static CaptureResult Failed(Exception error)
            => new() { Success = false, Error = error };
    }
}