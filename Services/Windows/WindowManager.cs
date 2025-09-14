// Services/Windows/WindowManager.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AutomationCore.Core.Abstractions;
using AutomationCore.Core.Exceptions;
using AutomationCore.Core.Models;
using Microsoft.Extensions.Logging;

namespace AutomationCore.Services.Windows
{
    /// <summary>
    /// Основная реализация менеджера окон
    /// </summary>
    public sealed class WindowManager : IWindowManager
    {
        private readonly IPlatformWindowOperations _platform;
        private readonly IWindowInfoCache _cache;
        private readonly ILogger<WindowManager> _logger;

        // События
        public event EventHandler<WindowChangedEvent>? WindowChanged;
        public event EventHandler<WindowInfo>? WindowCreated;
        public event EventHandler<WindowHandle>? WindowDestroyed;

        public WindowManager(
            IPlatformWindowOperations platform,
            IWindowInfoCache cache,
            ILogger<WindowManager> logger)
        {
            _platform = platform ?? throw new ArgumentNullException(nameof(platform));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async ValueTask<IReadOnlyList<WindowInfo>> FindWindowsAsync(
            WindowSearchCriteria criteria,
            CancellationToken ct = default)
        {
            _logger.LogDebug("Searching for windows with criteria: {@Criteria}", criteria);

            await Task.Yield(); // Делаем метод асинхронным для будущих расширений

            var allWindows = _platform.EnumerateWindows();
            var results = new List<WindowInfo>();

            foreach (var handle in allWindows)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    var info = GetWindowInfoInternal(handle);
                    if (info != null && MatchesCriteria(info, criteria))
                    {
                        results.Add(info);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get info for window 0x{Handle:X}", handle.Value);
                }
            }

            _logger.LogDebug("Found {Count} windows matching criteria", results.Count);
            return results;
        }

        public async ValueTask<WindowInfo?> GetWindowInfoAsync(
            WindowHandle handle,
            CancellationToken ct = default)
        {
            await Task.Yield();

            // Проверяем кэш
            var cached = _cache.GetCached(handle);
            if (cached != null)
            {
                _logger.LogTrace("Window info found in cache for 0x{Handle:X}", handle.Value);
                return cached;
            }

            return GetWindowInfoInternal(handle);
        }

        public async ValueTask<IReadOnlyList<WindowInfo>> GetAllWindowsAsync(CancellationToken ct = default)
        {
            _logger.LogDebug("Getting all windows");

            var criteria = new WindowSearchCriteria(); // Пустые критерии = все окна
            return await FindWindowsAsync(criteria, ct);
        }

        public async ValueTask<bool> IsWindowValidAsync(WindowHandle handle, CancellationToken ct = default)
        {
            await Task.Yield();
            return _platform.IsWindow(handle);
        }

        public async ValueTask<bool> PerformWindowOperationAsync(
            WindowHandle handle,
            WindowOperation operation,
            CancellationToken ct = default)
        {
            _logger.LogDebug("Performing operation {Operation} on window 0x{Handle:X}", operation, handle.Value);

            await Task.Yield();

            if (!_platform.IsWindow(handle))
            {
                throw new WindowNotFoundException(handle);
            }

            try
            {
                bool result = _platform.ShowWindow(handle, operation);

                if (result)
                {
                    // Инвалидируем кэш после изменения состояния
                    _cache.RemoveFromCache(handle);

                    // Генерируем событие
                    var changeType = operation switch
                    {
                        WindowOperation.Minimize => WindowChangeType.Minimized,
                        WindowOperation.Maximize => WindowChangeType.Maximized,
                        WindowOperation.Restore => WindowChangeType.Restored,
                        WindowOperation.Close => WindowChangeType.Closed,
                        _ => WindowChangeType.Moved
                    };

                    WindowChanged?.Invoke(this, new WindowChangedEvent
                    {
                        Handle = handle,
                        ChangeType = changeType,
                        Timestamp = DateTime.UtcNow
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to perform operation {Operation} on window 0x{Handle:X}", operation, handle.Value);
                throw new WindowNotAccessibleException(handle, operation);
            }
        }

        public async ValueTask<bool> SetWindowBoundsAsync(
            WindowHandle handle,
            Rectangle bounds,
            CancellationToken ct = default)
        {
            _logger.LogDebug("Setting bounds for window 0x{Handle:X} to {Bounds}", handle.Value, bounds);

            await Task.Yield();

            if (!_platform.IsWindow(handle))
            {
                throw new WindowNotFoundException(handle);
            }

            var oldBounds = _platform.GetWindowBounds(handle);
            bool result = _platform.SetWindowBounds(handle, bounds);

            if (result)
            {
                // Инвалидируем кэш
                _cache.RemoveFromCache(handle);

                // Генерируем событие
                WindowChanged?.Invoke(this, new WindowChangedEvent
                {
                    Handle = handle,
                    ChangeType = WindowChangeType.Resized,
                    OldBounds = oldBounds,
                    NewBounds = bounds,
                    Timestamp = DateTime.UtcNow
                });
            }

            return result;
        }

        public async ValueTask<Rectangle?> GetWindowBoundsAsync(
            WindowHandle handle,
            CancellationToken ct = default)
        {
            await Task.Yield();

            if (!_platform.IsWindow(handle))
            {
                return null;
            }

            var bounds = _platform.GetWindowBounds(handle);
            return bounds.IsEmpty ? null : bounds;
        }

        public async ValueTask<WindowInfo?> WaitForWindowAsync(
            WindowSearchCriteria criteria,
            TimeSpan timeout,
            CancellationToken ct = default)
        {
            _logger.LogDebug("Waiting for window with timeout {Timeout}", timeout);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);

            const int checkInterval = 100; // мс

            while (!cts.Token.IsCancellationRequested)
            {
                var windows = await FindWindowsAsync(criteria, cts.Token);
                if (windows.Count > 0)
                {
                    _logger.LogDebug("Found window after waiting");
                    return windows[0];
                }

                try
                {
                    await Task.Delay(checkInterval, cts.Token);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw; // Пробрасываем отмену пользователя
                }
                catch (OperationCanceledException)
                {
                    // Таймаут
                    break;
                }
            }

            _logger.LogDebug("Window not found within timeout");
            return null;
        }

        public async IAsyncEnumerable<WindowChangedEvent> WatchWindowChangesAsync(
            WindowHandle handle,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            // Заглушка для будущей реализации через Windows hooks
            await Task.Yield();

            _logger.LogWarning("Window change watching is not yet implemented");
            yield break;
        }

        #region Private Methods

        private WindowInfo? GetWindowInfoInternal(WindowHandle handle)
        {
            try
            {
                if (!_platform.IsWindow(handle))
                {
                    return null;
                }

                var title = _platform.GetWindowTitle(handle);
                var className = _platform.GetWindowClassName(handle);
                var bounds = _platform.GetWindowBounds(handle);
                var processId = _platform.GetWindowProcessId(handle);
                var state = _platform.GetWindowState(handle);

                var info = new WindowInfo
                {
                    Handle = handle,
                    Title = title,
                    ClassName = className,
                    Bounds = bounds,
                    ProcessId = processId,
                    ProcessName = GetProcessName(processId),
                    IsMinimized = state.HasFlag(WindowState.Minimized),
                    IsMaximized = state.HasFlag(WindowState.Maximized),
                    IsVisible = _platform.IsWindowVisible(handle),
                    IsTopmost = state.HasFlag(WindowState.Topmost)
                };

                // Кэшируем результат
                _cache.SetCached(handle, info);

                return info;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to build window info for 0x{Handle:X}", handle.Value);
                return null;
            }
        }

        private static bool MatchesCriteria(WindowInfo window, WindowSearchCriteria criteria)
        {
            // Фильтр видимости
            if (criteria.VisibleOnly && !window.IsVisible)
                return false;

            // Фильтр свернутых окон
            if (!criteria.IncludeMinimized && window.IsMinimized)
                return false;

            // Фильтр по заголовку
            if (!string.IsNullOrEmpty(criteria.TitlePattern))
            {
                bool matches = criteria.ExactMatch
                    ? string.Equals(window.Title, criteria.TitlePattern, StringComparison.OrdinalIgnoreCase)
                    : window.Title.Contains(criteria.TitlePattern, StringComparison.OrdinalIgnoreCase);

                if (!matches)
                    return false;
            }

            // Фильтр по классу
            if (!string.IsNullOrEmpty(criteria.ClassName))
            {
                if (!string.Equals(window.ClassName, criteria.ClassName, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // Фильтр по процессу
            if (!string.IsNullOrEmpty(criteria.ProcessName))
            {
                if (!string.Equals(window.ProcessName, criteria.ProcessName, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // Фильтр по ID процесса
            if (criteria.ProcessIdFilter.HasValue)
            {
                if (window.ProcessId != criteria.ProcessIdFilter.Value)
                    return false;
            }

            // Фильтр по границам
            if (criteria.BoundsFilter.HasValue)
            {
                var filter = criteria.BoundsFilter.Value;
                if (!filter.IntersectsWith(window.Bounds))
                    return false;
            }

            return true;
        }

        private static string GetProcessName(int processId)
        {
            try
            {
                using var process = System.Diagnostics.Process.GetProcessById(processId);
                return process.ProcessName;
            }
            catch
            {
                return string.Empty;
            }
        }


        #endregion
    }
}