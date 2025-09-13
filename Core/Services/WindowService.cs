// AutomationCore/Core/Services/WindowService.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using AutomationCore.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;



namespace AutomationCore.Core.Services
{
    /// <summary>
    /// Сервис для работы с окнами (изолирует P/Invoke).
    /// </summary>
    public interface IWindowService
    {
        IReadOnlyList<WindowInfo> GetAllWindows();
        IReadOnlyList<WindowInfo> FindWindows(WindowSearchCriteria criteria);
        WindowInfo GetWindowInfo(WindowHandle handle);
        bool IsWindowValid(WindowHandle handle);
        Rectangle GetWindowBounds(WindowHandle handle);
    }

    public class WindowSearchCriteria
    {
        public string TitlePattern { get; set; }
        public string ProcessName { get; set; }
        public string ClassName { get; set; }
        public bool ExactMatch { get; set; }
        public bool VisibleOnly { get; set; } = true;

        // Fluent API
        public static WindowSearchCriteria WithTitle(string pattern)
            => new() { TitlePattern = pattern };
    }

    public class WindowService : IWindowService
    {
        private readonly IPInvokeWrapper _pinvoke;
        private readonly ILogger<WindowService> _logger;

        public WindowService(IPInvokeWrapper pinvoke, ILogger<WindowService> logger)
        {
            _pinvoke = pinvoke ?? throw new ArgumentNullException(nameof(pinvoke));
            _logger = logger ?? NullLogger<WindowService>.Instance;
        }

        public IReadOnlyList<WindowInfo> GetAllWindows()
        {
            _logger.LogDebug("Enumerating all windows");
            var windows = new List<WindowInfo>();

            _pinvoke.EnumWindows((hwnd, _) =>
            {
                if (_pinvoke.IsWindowVisible(hwnd))
                {
                    var info = BuildWindowInfo(new WindowHandle(hwnd));
                    if (info != null) windows.Add(info);
                }
                return true;
            });

            _logger.LogDebug("Found {Count} windows", windows.Count);
            return windows;
        }

        public IReadOnlyList<WindowInfo> FindWindows(WindowSearchCriteria criteria)
        {
            criteria ??= new WindowSearchCriteria();
            var result = new List<WindowInfo>();
            var titlePattern = criteria.TitlePattern?.Trim() ?? string.Empty;
            var classNameFilter = criteria.ClassName?.Trim();
            var processFilter = criteria.ProcessName?.Trim();

            _pinvoke.EnumWindows((hwnd, _) =>
            {
                if (criteria.VisibleOnly && !_pinvoke.IsWindowVisible(hwnd))
                    return true;

                var info = BuildWindowInfo(new WindowHandle(hwnd));
                if (info == null) return true;

                bool ok = true;

                if (!string.IsNullOrEmpty(titlePattern))
                {
                    ok &= criteria.ExactMatch
                        ? string.Equals(info.Title, titlePattern, StringComparison.OrdinalIgnoreCase)
                        : (info.Title?.IndexOf(titlePattern, StringComparison.OrdinalIgnoreCase) >= 0);
                }

                if (!string.IsNullOrEmpty(classNameFilter))
                {
                    ok &= string.Equals(info.ClassName, classNameFilter, StringComparison.OrdinalIgnoreCase);
                }

                if (!string.IsNullOrEmpty(processFilter))
                {
                    ok &= string.Equals(info.ProcessName, processFilter, StringComparison.OrdinalIgnoreCase);
                }

                if (ok) result.Add(info);
                return true;
            });

            return result;
        }

        public WindowInfo GetWindowInfo(WindowHandle handle) => BuildWindowInfo(handle);

        public bool IsWindowValid(WindowHandle handle)
        {
            try
            {
                var r = GetWindowBounds(handle);
                return r.Width > 0 && r.Height > 0;
            }
            catch { return false; }
        }

        public Rectangle GetWindowBounds(WindowHandle handle)
        {
            if (!_pinvoke.GetWindowRect(handle, out var rect))
                throw new InvalidOperationException("GetWindowRect failed");
            return rect;
        }

        private WindowInfo BuildWindowInfo(WindowHandle hwnd)
        {
            var titleLen = _pinvoke.GetWindowTextLength(hwnd);
            var title = string.Empty;
            if (titleLen > 0)
            {
                var sb = new StringBuilder(titleLen + 1);
                _pinvoke.GetWindowText(hwnd, sb, sb.Capacity);
                title = sb.ToString();
            }

            var cls = new StringBuilder(256);
            _pinvoke.GetClassName(hwnd, cls, cls.Capacity);

            _pinvoke.GetWindowRect(hwnd, out var rect);
            var placement = new PInvokeWrapper.WINDOWPLACEMENT { length = System.Runtime.InteropServices.Marshal.SizeOf<PInvokeWrapper.WINDOWPLACEMENT>() };
            _pinvoke.GetWindowPlacement(hwnd, ref placement);

            _pinvoke.GetWindowThreadProcessId(hwnd, out var pid);
            string procName = null;
            try { procName = Process.GetProcessById((int)pid).ProcessName; } catch { }

            return new WindowInfo
            {
                Handle = hwnd,
                Title = title,
                ClassName = cls.ToString(),
                Bounds = rect,
                ProcessId = (int)pid,
                ProcessName = procName,
                IsMinimized = placement.showCmd == 6, // SW_MINIMIZE
                IsMaximized = placement.showCmd == 3  // SW_MAXIMIZE
            };
        }
    }
}
