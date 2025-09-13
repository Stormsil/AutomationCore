// Core/Abstractions/IScreenCapture.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutomationCore.Core.Models;

namespace AutomationCore.Core.Abstractions
{
    /// <summary>
    /// Основной интерфейс для захвата экрана
    /// </summary>
    public interface IScreenCapture : IDisposable
    {
        /// <summary>Поддерживается ли захват на текущей системе</summary>
        bool IsSupported { get; }

        /// <summary>Захватывает один кадр</summary>
        ValueTask<CaptureResult> CaptureAsync(CaptureRequest request, CancellationToken ct = default);

        /// <summary>Создает сессию потокового захвата</summary>
        ValueTask<ICaptureSession> StartSessionAsync(CaptureRequest request, CancellationToken ct = default);
    }

    /// <summary>
    /// Сессия потокового захвата
    /// </summary>
    public interface ICaptureSession : IDisposable
    {
        /// <summary>Целевой объект захвата</summary>
        CaptureTarget Target { get; }

        /// <summary>Активна ли сессия</summary>
        bool IsActive { get; }

        /// <summary>Получает следующий кадр</summary>
        ValueTask<CaptureFrame> GetNextFrameAsync(CancellationToken ct = default);

        /// <summary>Поток кадров в реальном времени</summary>
        IAsyncEnumerable<CaptureFrame> GetFrameStreamAsync(CancellationToken ct = default);

        /// <summary>Получает последний захваченный кадр (если есть)</summary>
        CaptureFrame? GetLastFrame();

        /// <summary>Получает метрики сессии</summary>
        CaptureMetrics GetMetrics();

        /// <summary>Подписка на события захвата</summary>
        event EventHandler<FrameCapturedEventArgs>? FrameCaptured;
        event EventHandler<CaptureErrorEventArgs>? CaptureError;

        /// <summary>Останавливает захват</summary>
        void Stop();
    }

    /// <summary>
    /// Метрики захвата
    /// </summary>
    public sealed record CaptureMetrics
    {
        public long TotalFrames { get; init; }
        public long DroppedFrames { get; init; }
        public double CurrentFps { get; init; }
        public double AverageFps { get; init; }
        public DateTime StartTime { get; init; }
        public TimeSpan Uptime { get; init; }
        public long TotalBytes { get; init; }

        public double DropRate => TotalFrames > 0 ? (double)DroppedFrames / TotalFrames : 0;
    }

    /// <summary>
    /// Событие захвата кадра
    /// </summary>
    public sealed class FrameCapturedEventArgs : EventArgs
    {
        public required CaptureFrame Frame { get; init; }
        public required CaptureMetrics Metrics { get; init; }
    }

    /// <summary>
    /// Событие ошибки захвата
    /// </summary>
    public sealed class CaptureErrorEventArgs : EventArgs
    {
        public required Exception Exception { get; init; }
        public required CaptureTarget Target { get; init; }
        public bool IsFatal { get; init; }
    }
}