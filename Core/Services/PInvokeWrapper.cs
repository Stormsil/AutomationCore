// AutomationCore/Core/Services/PInvokeWrapper.cs
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using static AutomationCore.Core.Services.PInvokeWrapper;

namespace AutomationCore.Core.Services
{
    public interface IPInvokeWrapper
    {
        bool EnumWindows(Func<IntPtr, IntPtr, bool> callback, IntPtr lParam = default);
        bool IsWindowVisible(IntPtr hWnd);
        int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);
        int GetWindowTextLength(IntPtr hWnd);
        int GetClassName(IntPtr hWnd, StringBuilder text, int maxCount);
        bool GetWindowRect(IntPtr hWnd, out Rectangle rect);
        bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT placement);
        uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    }

    public sealed class PInvokeWrapper : IPInvokeWrapper
    {
        public bool EnumWindows(Func<IntPtr, IntPtr, bool> callback, IntPtr lParam = default)
            => EnumWindows(new EnumWindowsProc((h, p) => callback(h, p)), lParam);

        public bool IsWindowVisible(IntPtr hWnd) => NativeIsWindowVisible(hWnd);
        public int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount) => NativeGetWindowText(hWnd, text, maxCount);
        public int GetWindowTextLength(IntPtr hWnd) => NativeGetWindowTextLength(hWnd);
        public int GetClassName(IntPtr hWnd, StringBuilder text, int maxCount) => NativeGetClassName(hWnd, text, maxCount);
        public bool GetWindowRect(IntPtr hWnd, out Rectangle rect) => NativeGetWindowRect(hWnd, out rect);
        public bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT placement) => NativeGetWindowPlacement(hWnd, ref placement);
        public uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid) => NativeGetWindowThreadProcessId(hWnd, out pid);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public Point ptMinPosition;
            public Point ptMaxPosition;
            public Rectangle rcNormalPosition;
        }

        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool NativeIsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int NativeGetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        [DllImport("user32.dll")] private static extern int NativeGetWindowTextLength(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int NativeGetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
        [DllImport("user32.dll")] private static extern bool NativeGetWindowRect(IntPtr hWnd, out Rectangle lpRect);
        [DllImport("user32.dll")] private static extern bool NativeGetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);
        [DllImport("user32.dll")] private static extern uint NativeGetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    }
}
