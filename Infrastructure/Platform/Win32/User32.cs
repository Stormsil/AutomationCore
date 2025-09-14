// Infrastructure/Platform/Win32/User32.cs
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace AutomationCore.Infrastructure.Platform.Win32
{
    /// <summary>
    /// P/Invoke обертки для User32.dll
    /// </summary>
    internal static class User32
    {
        private const string DLL_NAME = "user32.dll";

        #region Window Enumeration

        internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport(DLL_NAME)]
        internal static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport(DLL_NAME)]
        internal static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

        #endregion

        #region Window Information

        [DllImport(DLL_NAME, CharSet = CharSet.Unicode)]
        internal static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);

        [DllImport(DLL_NAME)]
        internal static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport(DLL_NAME, CharSet = CharSet.Unicode)]
        internal static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport(DLL_NAME)]
        internal static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport(DLL_NAME)]
        internal static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport(DLL_NAME)]
        internal static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [DllImport(DLL_NAME)]
        internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        #endregion

        #region Window State

        [DllImport(DLL_NAME)]
        internal static extern bool IsWindow(IntPtr hWnd);

        [DllImport(DLL_NAME)]
        internal static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport(DLL_NAME)]
        internal static extern bool IsIconic(IntPtr hWnd);

        [DllImport(DLL_NAME)]
        internal static extern bool IsZoomed(IntPtr hWnd);

        [DllImport(DLL_NAME)]
        internal static extern IntPtr GetForegroundWindow();

        [DllImport(DLL_NAME)]
        internal static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport(DLL_NAME)]
        internal static extern IntPtr GetActiveWindow();

        #endregion

        #region Window Operations

        [DllImport(DLL_NAME)]
        internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport(DLL_NAME)]
        internal static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        [DllImport(DLL_NAME)]
        internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport(DLL_NAME)]
        internal static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport(DLL_NAME)]
        internal static extern bool CloseWindow(IntPtr hWnd);

        [DllImport(DLL_NAME)]
        internal static extern bool DestroyWindow(IntPtr hWnd);

        #endregion

        #region Input Simulation

        [DllImport(DLL_NAME)]
        internal static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport(DLL_NAME)]
        internal static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport(DLL_NAME)]
        internal static extern bool SetCursorPos(int X, int Y);

        [DllImport(DLL_NAME)]
        internal static extern short VkKeyScan(char ch);

        [DllImport(DLL_NAME)]
        internal static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport(DLL_NAME)]
        internal static extern IntPtr GetMessageExtraInfo();

        #endregion

        #region Monitor Information

        [DllImport(DLL_NAME)]
        internal static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        internal delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [DllImport(DLL_NAME)]
        internal static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        #endregion

        #region Window Extended Styles

        [DllImport(DLL_NAME)]
        internal static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport(DLL_NAME)]
        internal static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport(DLL_NAME)]
        internal static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport(DLL_NAME)]
        internal static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        #endregion

        #region Constants

        // ShowWindow commands
        internal const int SW_HIDE = 0;
        internal const int SW_SHOWNORMAL = 1;
        internal const int SW_NORMAL = 1;
        internal const int SW_SHOWMINIMIZED = 2;
        internal const int SW_SHOWMAXIMIZED = 3;
        internal const int SW_MAXIMIZE = 3;
        internal const int SW_SHOWNOACTIVATE = 4;
        internal const int SW_SHOW = 5;
        internal const int SW_MINIMIZE = 6;
        internal const int SW_SHOWMINNOACTIVE = 7;
        internal const int SW_SHOWNA = 8;
        internal const int SW_RESTORE = 9;

        // SetWindowPos flags
        internal const uint SWP_NOSIZE = 0x0001;
        internal const uint SWP_NOMOVE = 0x0002;
        internal const uint SWP_NOZORDER = 0x0004;
        internal const uint SWP_NOREDRAW = 0x0008;
        internal const uint SWP_NOACTIVATE = 0x0010;
        internal const uint SWP_FRAMECHANGED = 0x0020;
        internal const uint SWP_SHOWWINDOW = 0x0040;
        internal const uint SWP_HIDEWINDOW = 0x0080;
        internal const uint SWP_NOCOPYBITS = 0x0100;
        internal const uint SWP_NOOWNERZORDER = 0x0200;
        internal const uint SWP_NOSENDCHANGING = 0x0400;

        // Special window handles
        internal static readonly IntPtr HWND_TOP = new IntPtr(0);
        internal static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        internal static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        internal static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

        // GetWindowLong indices
        internal const int GWL_EXSTYLE = -20;
        internal const int GWL_STYLE = -16;
        internal const int GWL_WNDPROC = -4;

        // Extended window styles
        internal const int WS_EX_TRANSPARENT = 0x00000020;
        internal const int WS_EX_TOOLWINDOW = 0x00000080;
        internal const int WS_EX_NOACTIVATE = 0x08000000;
        internal const int WS_EX_TOPMOST = 0x00000008;

        // Input types
        internal const uint INPUT_MOUSE = 0;
        internal const uint INPUT_KEYBOARD = 1;
        internal const uint INPUT_HARDWARE = 2;

        // Mouse event flags
        internal const uint MOUSEEVENTF_MOVE = 0x0001;
        internal const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        internal const uint MOUSEEVENTF_LEFTUP = 0x0004;
        internal const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        internal const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        internal const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        internal const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
        internal const uint MOUSEEVENTF_XDOWN = 0x0080;
        internal const uint MOUSEEVENTF_XUP = 0x0100;
        internal const uint MOUSEEVENTF_WHEEL = 0x0800;
        internal const uint MOUSEEVENTF_HWHEEL = 0x1000;
        internal const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

        // Keyboard event flags
        internal const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        internal const uint KEYEVENTF_KEYUP = 0x0002;
        internal const uint KEYEVENTF_UNICODE = 0x0004;
        internal const uint KEYEVENTF_SCANCODE = 0x0008;

        #endregion

        #region Structures

        [StructLayout(LayoutKind.Sequential)]
        internal struct POINT
        {
            internal int X;
            internal int Y;

            internal POINT(int x, int y)
            {
                X = x;
                Y = y;
            }

            public static implicit operator Point(POINT point) => new(point.X, point.Y);
            public static implicit operator POINT(Point point) => new(point.X, point.Y);
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT
        {
            internal int Left;
            internal int Top;
            internal int Right;
            internal int Bottom;

            internal int Width => Right - Left;
            internal int Height => Bottom - Top;

            internal RECT(int left, int top, int right, int bottom)
            {
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }

            public static implicit operator Rectangle(RECT rect) =>
                new(rect.Left, rect.Top, rect.Width, rect.Height);

            public static implicit operator RECT(Rectangle rect) =>
                new(rect.Left, rect.Top, rect.Right, rect.Bottom);
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct WINDOWPLACEMENT
        {
            internal int length;
            internal int flags;
            internal int showCmd;
            internal POINT ptMinPosition;
            internal POINT ptMaxPosition;
            internal RECT rcNormalPosition;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct INPUT
        {
            internal uint type;
            internal InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct InputUnion
        {
            [FieldOffset(0)]
            internal MOUSEINPUT mi;
            [FieldOffset(0)]
            internal KEYBDINPUT ki;
            [FieldOffset(0)]
            internal HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MOUSEINPUT
        {
            internal int dx;
            internal int dy;
            internal uint mouseData;
            internal uint dwFlags;
            internal uint time;
            internal IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct KEYBDINPUT
        {
            internal ushort wVk;
            internal ushort wScan;
            internal uint dwFlags;
            internal uint time;
            internal IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct HARDWAREINPUT
        {
            internal uint uMsg;
            internal ushort wParamL;
            internal ushort wParamH;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        internal struct MONITORINFO
        {
            internal int cbSize;
            internal RECT rcMonitor;
            internal RECT rcWork;
            internal uint dwFlags;
        }

        #endregion
    }
}