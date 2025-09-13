// Infrastructure/Platform/WindowsPlatformService.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using AutomationCore.Core.Abstractions;
using AutomationCore.Core.Models;
using AutomationCore.Infrastructure.Platform.Win32;

namespace AutomationCore.Infrastructure.Platform
{
    /// <summary>
    /// Реализация платформенных операций для Windows
    /// </summary>
    internal sealed class WindowsPlatformService : IPlatformWindowOperations
    {
        public IEnumerable<WindowHandle> EnumerateWindows()
        {
            var windows = new List<WindowHandle>();

            User32.EnumWindows((hwnd, _) =>
            {
                windows.Add(new WindowHandle(hwnd));
                return true; // Продолжить перечисление
            }, IntPtr.Zero);

            return windows;
        }

        public bool IsWindowVisible(WindowHandle handle)
        {
            return User32.IsWindowVisible(handle.Value);
        }

        public string GetWindowTitle(WindowHandle handle)
        {
            int length = User32.GetWindowTextLength(handle.Value);
            if (length == 0) return string.Empty;

            var buffer = new StringBuilder(length + 1);
            User32.GetWindowText(handle.Value, buffer, buffer.Capacity);
            return buffer.ToString();
        }

        public string GetWindowClassName(WindowHandle handle)
        {
            var buffer = new StringBuilder(256);
            User32.GetClassName(handle.Value, buffer, buffer.Capacity);
            return buffer.ToString();
        }

        public Rectangle GetWindowBounds(WindowHandle handle)
        {
            if (User32.GetWindowRect(handle.Value, out var rect))
            {
                return rect;
            }
            return Rectangle.Empty;
        }

        public int GetWindowProcessId(WindowHandle handle)
        {
            User32.GetWindowThreadProcessId(handle.Value, out uint processId);
            return (int)processId;
        }

        public WindowState GetWindowState(WindowHandle handle)
        {
            var state = WindowState.Normal;

            if (!IsWindow(handle))
                return state;

            if (!IsWindowVisible(handle))
                state |= WindowState.Hidden;

            if (User32.IsIconic(handle.Value))
                state |= WindowState.Minimized;

            if (User32.IsZoomed(handle.Value))
                state |= WindowState.Maximized;

            // Проверяем Topmost стиль
            int exStyle = User32.GetWindowLong(handle.Value, User32.GWL_EXSTYLE);
            if ((exStyle & User32.WS_EX_TOPMOST) != 0)
                state |= WindowState.Topmost;

            return state;
        }

        public bool SetWindowBounds(WindowHandle handle, Rectangle bounds)
        {
            return User32.MoveWindow(handle.Value, bounds.X, bounds.Y, bounds.Width, bounds.Height, true);
        }

        public bool ShowWindow(WindowHandle handle, WindowOperation operation)
        {
            int command = operation switch
            {
                WindowOperation.Show => User32.SW_SHOW,
                WindowOperation.Hide => User32.SW_HIDE,
                WindowOperation.Minimize => User32.SW_MINIMIZE,
                WindowOperation.Maximize => User32.SW_MAXIMIZE,
                WindowOperation.Restore => User32.SW_RESTORE,
                WindowOperation.BringToFront => User32.SW_SHOWNORMAL,
                WindowOperation.SendToBack => User32.SW_SHOWNOACTIVATE,
                _ => User32.SW_SHOWNORMAL
            };

            bool result = User32.ShowWindow(handle.Value, command);

            // Дополнительные операции для некоторых команд
            switch (operation)
            {
                case WindowOperation.SetTopmost:
                    User32.SetWindowPos(handle.Value, User32.HWND_TOPMOST, 0, 0, 0, 0,
                        User32.SWP_NOMOVE | User32.SWP_NOSIZE | User32.SWP_NOACTIVATE);
                    break;

                case WindowOperation.RemoveTopmost:
                    User32.SetWindowPos(handle.Value, User32.HWND_NOTOPMOST, 0, 0, 0, 0,
                        User32.SWP_NOMOVE | User32.SWP_NOSIZE | User32.SWP_NOACTIVATE);
                    break;

                case WindowOperation.BringToFront:
                    User32.SetForegroundWindow(handle.Value);
                    break;

                case WindowOperation.SendToBack:
                    User32.SetWindowPos(handle.Value, User32.HWND_BOTTOM, 0, 0, 0, 0,
                        User32.SWP_NOMOVE | User32.SWP_NOSIZE | User32.SWP_NOACTIVATE);
                    break;

                case WindowOperation.Close:
                    // Отправляем WM_CLOSE для корректного закрытия
                    return User32.CloseWindow(handle.Value);
            }

            return result;
        }

        public bool SetForegroundWindow(WindowHandle handle)
        {
            return User32.SetForegroundWindow(handle.Value);
        }

        public bool IsWindow(WindowHandle handle)
        {
            return User32.IsWindow(handle.Value);
        }

        /// <summary>
        /// Получает дескриптор активного окна
        /// </summary>
        public WindowHandle GetForegroundWindow()
        {
            return new WindowHandle(User32.GetForegroundWindow());
        }

        /// <summary>
        /// Получает информацию о размещении окна
        /// </summary>
        public WindowPlacement GetWindowPlacement(WindowHandle handle)
        {
            var placement = new User32.WINDOWPLACEMENT
            {
                length = Marshal.SizeOf<User32.WINDOWPLACEMENT>()
            };

            if (User32.GetWindowPlacement(handle.Value, ref placement))
            {
                return new WindowPlacement
                {
                    ShowState = (WindowShowState)placement.showCmd,
                    MinPosition = placement.ptMinPosition,
                    MaxPosition = placement.ptMaxPosition,
                    NormalPosition = placement.rcNormalPosition
                };
            }

            return new WindowPlacement();
        }

        /// <summary>
        /// Получает список всех мониторов
        /// </summary>
        public IReadOnlyList<MonitorInfo> GetMonitors()
        {
            var monitors = new List<MonitorInfo>();

            User32.MonitorEnumProc callback = (IntPtr hMonitor, IntPtr hdcMonitor, ref User32.RECT lprcMonitor, IntPtr dwData) =>
            {
                var info = new User32.MONITORINFO
                {
                    cbSize = Marshal.SizeOf<User32.MONITORINFO>()
                };

                if (User32.GetMonitorInfo(hMonitor, ref info))
                {
                    monitors.Add(new MonitorInfo
                    {
                        Handle = hMonitor,
                        Bounds = info.rcMonitor,
                        WorkArea = info.rcWork,
                        IsPrimary = (info.dwFlags & 1) != 0
                    });
                }

                return true;
            };

            User32.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
            return monitors;
        }

        /// <summary>
        /// Получает имя процесса по ID
        /// </summary>
        public string GetProcessName(int processId)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                return process.ProcessName;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Строит полную информацию об окне
        /// </summary>
        public WindowInfo BuildWindowInfo(WindowHandle handle)
        {
            if (!IsWindow(handle))
                throw new InvalidOperationException($"Invalid window handle: 0x{handle.Value:X}");

            var title = GetWindowTitle(handle);
            var className = GetWindowClassName(handle);
            var bounds = GetWindowBounds(handle);
            var processId = GetWindowProcessId(handle);
            var processName = GetProcessName(processId);
            var state = GetWindowState(handle);

            return new WindowInfo
            {
                Handle = handle,
                Title = title,
                ClassName = className,
                Bounds = bounds,
                ProcessId = processId,
                ProcessName = processName,
                IsMinimized = state.HasFlag(WindowState.Minimized),
                IsMaximized = state.HasFlag(WindowState.Maximized),
                IsVisible = IsWindowVisible(handle),
                IsTopmost = state.HasFlag(WindowState.Topmost)
            };
        }
    }

    #region Supporting Types

    /// <summary>
    /// Информация о размещении окна
    /// </summary>
    public sealed record WindowPlacement
    {
        public WindowShowState ShowState { get; init; }
        public Point MinPosition { get; init; }
        public Point MaxPosition { get; init; }
        public Rectangle NormalPosition { get; init; }
    }

    /// <summary>
    /// Состояние показа окна
    /// </summary>
    public enum WindowShowState
    {
        Hidden = 0,
        Normal = 1,
        Minimized = 2,
        Maximized = 3,
        ShowNoActivate = 4,
        Show = 5,
        Minimize = 6,
        ShowMinNoActive = 7,
        ShowNA = 8,
        Restore = 9,
        ShowDefault = 10
    }

    /// <summary>
    /// Информация о мониторе
    /// </summary>
    public sealed record MonitorInfo
    {
        public required IntPtr Handle { get; init; }
        public required Rectangle Bounds { get; init; }
        public required Rectangle WorkArea { get; init; }
        public required bool IsPrimary { get; init; }
    }

    #endregion
}