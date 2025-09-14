// WindowInfoProvider - специализированный сервис для получения информации об окнах
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using AutomationCore.Core.Models;

namespace AutomationCore.Core.Domain.Windows
{
    /// <summary>
    /// Сервис для получения информации об окнах системы
    /// </summary>
    public sealed class WindowInfoProvider
    {
        /// <summary>Находит все окна по части заголовка</summary>
        public WindowInfo[] FindWindows(string titlePattern, bool exactMatch = false)
        {
            var windows = new List<WindowInfo>();
            var pattern = exactMatch ? titlePattern : Normalize(titlePattern);

            EnumWindows((hwnd, _) =>
            {
                if (!IsWindowVisible(hwnd)) return true;

                var info = GetWindowInfo(hwnd);
                if (info == null) return true;

                var normalizedTitle = Normalize(info.Title);

                bool matches = exactMatch
                    ? normalizedTitle.Equals(pattern, StringComparison.OrdinalIgnoreCase)
                    : normalizedTitle.Contains(pattern);

                if (matches) windows.Add(info);
                return true;
            }, IntPtr.Zero);

            return windows.ToArray();
        }

        /// <summary>Получает информацию об окне</summary>
        public WindowInfo GetWindowInfo(IntPtr hwnd)
        {
            if (!IsWindow(hwnd)) return null;

            var info = new WindowInfo { Handle = new WindowHandle(hwnd) };

            // Title
            int len = GetWindowTextLength(hwnd);
            if (len > 0)
            {
                var sb = new StringBuilder(len + 1);
                GetWindowText(hwnd, sb, sb.Capacity);
                info.Title = sb.ToString();
            }

            // Class name
            var className = new StringBuilder(256);
            GetClassName(hwnd, className, className.Capacity);
            info.ClassName = className.ToString();

            // Bounds
            GetWindowRect(hwnd, out var rect);
            info.Bounds = new Rectangle(rect.Left, rect.Top, rect.Width, rect.Height);

            // State
            var placement = new WINDOWPLACEMENT();
            placement.length = Marshal.SizeOf(placement);
            GetWindowPlacement(hwnd, ref placement);

            info.IsMinimized = placement.showCmd == SW_MINIMIZE || placement.showCmd == SW_SHOWMINIMIZED;
            info.IsMaximized = placement.showCmd == SW_MAXIMIZE || placement.showCmd == SW_SHOWMAXIMIZED;
            info.IsVisible = IsWindowVisible(hwnd);

            return info;
        }

        private static string Normalize(string input)
        {
            return input?.ToLowerInvariant().Trim() ?? string.Empty;
        }

        // P/Invoke declarations
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public System.Drawing.Point ptMinPosition;
            public System.Drawing.Point ptMaxPosition;
            public RECT rcNormalPosition;
        }

        private const int SW_MINIMIZE = 6;
        private const int SW_SHOWMINIMIZED = 2;
        private const int SW_MAXIMIZE = 3;
        private const int SW_SHOWMAXIMIZED = 3;
    }
}