// Core/Abstractions/IWindowManager.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using AutomationCore.Core.Models;

namespace AutomationCore.Core.Abstractions
{
    /// <summary>
    /// Интерфейс для управления окнами
    /// </summary>
    public interface IWindowManager
    {
        /// <summary>Находит окна по критериям поиска</summary>
        ValueTask<IReadOnlyList<WindowInfo>> FindWindowsAsync(WindowSearchCriteria criteria, CancellationToken ct = default);

        /// <summary>Получает информацию об окне</summary>
        ValueTask<WindowInfo?> GetWindowInfoAsync(WindowHandle handle, CancellationToken ct = default);

        /// <summary>Получает все видимые окна</summary>
        ValueTask<IReadOnlyList<WindowInfo>> GetAllWindowsAsync(CancellationToken ct = default);

        /// <summary>Проверяет, валидно ли окно</summary>
        ValueTask<bool> IsWindowValidAsync(WindowHandle handle, CancellationToken ct = default);

        /// <summary>Выполняет операцию с окном</summary>
        ValueTask<bool> PerformWindowOperationAsync(WindowHandle handle, WindowOperation operation, CancellationToken ct = default);

        /// <summary>Изменяет границы окна</summary>
        ValueTask<bool> SetWindowBoundsAsync(WindowHandle handle, Rectangle bounds, CancellationToken ct = default);

        /// <summary>Получает границы окна</summary>
        ValueTask<Rectangle?> GetWindowBoundsAsync(WindowHandle handle, CancellationToken ct = default);

        /// <summary>Ждет появления окна</summary>
        ValueTask<WindowInfo?> WaitForWindowAsync(WindowSearchCriteria criteria, TimeSpan timeout, CancellationToken ct = default);

        /// <summary>Отслеживает изменения окон</summary>
        IAsyncEnumerable<WindowChangedEvent> WatchWindowChangesAsync(WindowHandle handle, CancellationToken ct = default);

        /// <summary>События окон</summary>
        event EventHandler<WindowChangedEvent>? WindowChanged;
        event EventHandler<WindowInfo>? WindowCreated;
        event EventHandler<WindowHandle>? WindowDestroyed;
    }

    /// <summary>
    /// Низкоуровневые операции с платформой
    /// </summary>
    public interface IPlatformWindowOperations
    {
        /// <summary>Перечисляет все окна</summary>
        IEnumerable<WindowHandle> EnumerateWindows();

        /// <summary>Проверяет видимость окна</summary>
        bool IsWindowVisible(WindowHandle handle);

        /// <summary>Получает заголовок окна</summary>
        string GetWindowTitle(WindowHandle handle);

        /// <summary>Получает имя класса окна</summary>
        string GetWindowClassName(WindowHandle handle);

        /// <summary>Получает границы окна</summary>
        Rectangle GetWindowBounds(WindowHandle handle);

        /// <summary>Получает ID процесса окна</summary>
        int GetWindowProcessId(WindowHandle handle);

        /// <summary>Получает состояние окна</summary>
        WindowState GetWindowState(WindowHandle handle);

        /// <summary>Устанавливает границы окна</summary>
        bool SetWindowBounds(WindowHandle handle, Rectangle bounds);

        /// <summary>Показывает/скрывает окно</summary>
        bool ShowWindow(WindowHandle handle, WindowOperation operation);

        /// <summary>Устанавливает фокус на окно</summary>
        bool SetForegroundWindow(WindowHandle handle);

        /// <summary>Проверяет существование окна</summary>
        bool IsWindow(WindowHandle handle);
    }

    /// <summary>
    /// Менеджер активного окна
    /// </summary>
    public interface IActiveWindowTracker
    {
        /// <summary>Текущее активное окно</summary>
        WindowHandle CurrentActiveWindow { get; }

        /// <summary>Предыдущее активное окно</summary>
        WindowHandle PreviousActiveWindow { get; }

        /// <summary>Активирует окно</summary>
        ValueTask<bool> ActivateWindowAsync(WindowHandle handle, CancellationToken ct = default);

        /// <summary>Возвращается к предыдущему окну</summary>
        ValueTask<bool> RestorePreviousWindowAsync(CancellationToken ct = default);

        /// <summary>Событие смены активного окна</summary>
        event EventHandler<WindowChangedEvent>? ActiveWindowChanged;

        /// <summary>Начинает отслеживание</summary>
        void StartTracking();

        /// <summary>Останавливает отслеживание</summary>
        void StopTracking();
    }

    /// <summary>
    /// Помощник для работы с окнами приложений
    /// </summary>
    public interface IApplicationWindowHelper
    {
        /// <summary>Находит главное окно процесса</summary>
        ValueTask<WindowInfo?> FindMainWindowAsync(int processId, CancellationToken ct = default);

        /// <summary>Находит все окна процесса</summary>
        ValueTask<IReadOnlyList<WindowInfo>> FindProcessWindowsAsync(int processId, CancellationToken ct = default);

        /// <summary>Находит окна по имени процесса</summary>
        ValueTask<IReadOnlyList<WindowInfo>> FindProcessWindowsAsync(string processName, CancellationToken ct = default);

        /// <summary>Ждет запуска приложения</summary>
        ValueTask<WindowInfo?> WaitForApplicationAsync(string processName, TimeSpan timeout, CancellationToken ct = default);

        /// <summary>Закрывает все окна приложения</summary>
        ValueTask<bool> CloseApplicationAsync(string processName, bool force = false, CancellationToken ct = default);

        /// <summary>Сворачивает все окна</summary>
        ValueTask MinimizeAllWindowsAsync(CancellationToken ct = default);

        /// <summary>Восстанавливает все свернутые окна</summary>
        ValueTask RestoreAllWindowsAsync(CancellationToken ct = default);
    }

    /// <summary>
    /// Кэш информации о окнах
    /// </summary>
    public interface IWindowInfoCache
    {
        /// <summary>Получает информацию из кэша</summary>
        WindowInfo? GetCached(WindowHandle handle);

        /// <summary>Сохраняет информацию в кэш</summary>
        void SetCached(WindowHandle handle, WindowInfo info, TimeSpan? ttl = null);

        /// <summary>Удаляет из кэша</summary>
        bool RemoveFromCache(WindowHandle handle);

        /// <summary>Очищает кэш</summary>
        void ClearCache();

        /// <summary>Устанавливает TTL по умолчанию</summary>
        TimeSpan DefaultTtl { get; set; }
    }
}