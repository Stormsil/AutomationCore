// Core/Abstractions/ICaptureDevice.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using AutomationCore.Core.Models;

namespace AutomationCore.Core.Abstractions
{
    /// <summary>
    /// Устройство захвата - низкоуровневый интерфейс для конкретной технологии захвата
    /// </summary>
    public interface ICaptureDevice : IDisposable
    {
        /// <summary>Поддерживается ли данная технология захвата на системе</summary>
        bool IsSupported { get; }

        /// <summary>Создает новую сессию захвата</summary>
        ValueTask<ICaptureDeviceSession> CreateSessionAsync(CaptureRequest request, CancellationToken ct = default);
    }

    /// <summary>
    /// Сессия устройства захвата - представляет активный захват
    /// </summary>
    public interface ICaptureDeviceSession : IDisposable
    {
        /// <summary>Цель захвата</summary>
        CaptureTarget Target { get; }

        /// <summary>Размеры захвата</summary>
        System.Drawing.Size CaptureSize { get; }

        /// <summary>Активна ли сессия</summary>
        bool IsActive { get; }

        /// <summary>Запускает захват</summary>
        ValueTask StartAsync(CancellationToken ct = default);

        /// <summary>Останавливает захват</summary>
        ValueTask StopAsync(CancellationToken ct = default);

        /// <summary>Захватывает один кадр</summary>
        ValueTask<CaptureFrame> CaptureFrameAsync(CancellationToken ct = default);

        /// <summary>Получает метрики сессии</summary>
        CaptureMetrics GetMetrics();

        /// <summary>События сессии</summary>
        event EventHandler<FrameCapturedEventArgs>? FrameCaptured;
        event EventHandler<CaptureErrorEventArgs>? CaptureError;
        event EventHandler? SessionEnded;
    }

    /// <summary>
    /// Менеджер сессий захвата - управляет множественными сессиями
    /// </summary>
    public interface ICaptureSessionManager : IDisposable
    {
        /// <summary>Создает новую сессию захвата</summary>
        ValueTask<ICaptureSession> CreateSessionAsync(CaptureRequest request, CancellationToken ct = default);

        /// <summary>Получает все активные сессии</summary>
        ICaptureSession[] GetActiveSessions();

        /// <summary>Останавливает все сессии</summary>
        ValueTask StopAllSessionsAsync(CancellationToken ct = default);
    }
}