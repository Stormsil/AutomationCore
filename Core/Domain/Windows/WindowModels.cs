// Core/Models/WindowModels.cs
using System;
using System.Drawing;

namespace AutomationCore.Core.Models
{
    /// <summary>
    /// Типизированная обертка для handle окна
    /// </summary>
    public readonly record struct WindowHandle(IntPtr Value)
    {
        public static WindowHandle Invalid => new(IntPtr.Zero);
        public bool IsValid => Value != IntPtr.Zero;

        public static implicit operator IntPtr(WindowHandle handle) => handle.Value;
        public static implicit operator WindowHandle(IntPtr handle) => new(handle);

        public override string ToString() => $"Window(0x{Value:X})";
    }

    /// <summary>
    /// Информация об окне
    /// </summary>
    public sealed record WindowInfo
    {
        public required WindowHandle Handle { get; init; }
        public required string Title { get; init; } = string.Empty;
        public required string ClassName { get; init; } = string.Empty;
        public required Rectangle Bounds { get; init; }
        public required int ProcessId { get; init; }
        public string ProcessName { get; init; } = string.Empty;
        public bool IsMinimized { get; init; }
        public bool IsMaximized { get; init; }
        public bool IsVisible { get; init; } = true;
        public bool IsTopmost { get; init; }

        public Point Center => new(Bounds.X + Bounds.Width / 2, Bounds.Y + Bounds.Height / 2);
        public Size Size => Bounds.Size;
        public bool IsEmpty => Bounds.IsEmpty;

        public override string ToString() =>
            $"{Title} [{ProcessName}] ({Bounds.Width}x{Bounds.Height})";
    }

    /// <summary>
    /// Критерии поиска окон
    /// </summary>
    public sealed record WindowSearchCriteria
    {
        public string? TitlePattern { get; init; }
        public string? ProcessName { get; init; }
        public string? ClassName { get; init; }
        public bool ExactMatch { get; init; }
        public bool VisibleOnly { get; init; } = true;
        public bool IncludeMinimized { get; init; }
        public Rectangle? BoundsFilter { get; init; }
        public int? ProcessIdFilter { get; init; }

        // Fluent API для создания критериев
        public static WindowSearchCriteria WithTitle(string pattern, bool exact = false)
            => new() { TitlePattern = pattern, ExactMatch = exact };

        public static WindowSearchCriteria WithProcess(string processName)
            => new() { ProcessName = processName };

        public static WindowSearchCriteria WithClassName(string className)
            => new() { ClassName = className };

        public WindowSearchCriteria IncludingMinimized()
            => this with { IncludeMinimized = true };

        public WindowSearchCriteria IncludingInvisible()
            => this with { VisibleOnly = false };

        public WindowSearchCriteria InBounds(Rectangle bounds)
            => this with { BoundsFilter = bounds };
    }

    /// <summary>
    /// Событие изменения окна
    /// </summary>
    public sealed record WindowChangedEvent
    {
        public required WindowHandle Handle { get; init; }
        public required WindowChangeType ChangeType { get; init; }
        public Rectangle? OldBounds { get; init; }
        public Rectangle? NewBounds { get; init; }
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Типы изменений окна
    /// </summary>
    public enum WindowChangeType
    {
        Moved,
        Resized,
        Minimized,
        Maximized,
        Restored,
        Closed,
        Created,
        FocusChanged
    }

    /// <summary>
    /// Состояние окна
    /// </summary>
    [Flags]
    public enum WindowState
    {
        Normal = 0,
        Minimized = 1,
        Maximized = 2,
        Hidden = 4,
        Topmost = 8,
        Disabled = 16
    }

    /// <summary>
    /// Операции с окнами
    /// </summary>
    public enum WindowOperation
    {
        Show,
        Hide,
        Minimize,
        Maximize,
        Restore,
        Close,
        SetTopmost,
        RemoveTopmost,
        BringToFront,
        SendToBack
    }
}