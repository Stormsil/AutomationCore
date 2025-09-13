// Core/Models/InputModels.cs
using System;
using System.Drawing;

namespace AutomationCore.Core.Models
{
    /// <summary>
    /// Настройки движения мыши
    /// </summary>
    public sealed record MouseMoveOptions
    {
        public static MouseMoveOptions Default { get; } = new();

        /// <summary>Желаемая длительность перемещения (мс). Если 0 — вычисляется по расстоянию.</summary>
        public TimeSpan? Duration { get; init; }

        /// <summary>Делать ли «человеческую» траекторию (дрожание/кривая)</summary>
        public bool UseHumanTrajectory { get; init; } = true;

        /// <summary>Сложность кривой Безье (больше = более изогнутая траектория)</summary>
        public int CurveComplexity { get; init; } = 50;

        /// <summary>Включить микро-движения (дрожание мыши)</summary>
        public bool EnableMicroMovements { get; init; } = true;

        /// <summary>Множитель скорости мыши</summary>
        public double SpeedMultiplier { get; init; } = 1.0;

        public static MouseMoveOptions Fast => Default with
        {
            UseHumanTrajectory = false,
            EnableMicroMovements = false,
            SpeedMultiplier = 0.1
        };

        public static MouseMoveOptions VeryHuman => Default with
        {
            CurveComplexity = 100,
            SpeedMultiplier = 2.5
        };
    }

    /// <summary>
    /// Настройки перетаскивания
    /// </summary>
    public sealed record DragOptions
    {
        public static DragOptions Default { get; } = new();

        /// <summary>Длительность перетаскивания</summary>
        public TimeSpan Duration { get; init; } = TimeSpan.FromMilliseconds(500);

        /// <summary>Промежуточная пауза после зажатия</summary>
        public TimeSpan HoldDelayBeforeMove { get; init; } = TimeSpan.FromMilliseconds(120);

        /// <summary>Пауза после отпускания</summary>
        public TimeSpan ReleaseDelay { get; init; } = TimeSpan.FromMilliseconds(80);

        /// <summary>Настройки траектории движения</summary>
        public MouseMoveOptions MoveOptions { get; init; } = MouseMoveOptions.Default;
    }

    /// <summary>
    /// Настройки печати текста
    /// </summary>
    public sealed record TypingOptions
    {
        public static TypingOptions Default { get; } = new();

        /// <summary>Базовая задержка между символами</summary>
        public TimeSpan BaseDelay { get; init; } = TimeSpan.FromMilliseconds(50);

        /// <summary>Включить случайные «человеческие» паузы</summary>
        public bool UseHumanTiming { get; init; } = true;

        /// <summary>Разрешить опечатки с автокоррекцией</summary>
        public bool EnableTypos { get; init; } = false;

        /// <summary>Вероятность опечатки (0.0 - 1.0)</summary>
        public double TypoChance { get; init; } = 0.01;

        /// <summary>Диапазон случайной задержки</summary>
        public TimeSpan RandomDelayRange { get; init; } = TimeSpan.FromMilliseconds(20);

        public static TypingOptions Fast => Default with
        {
            BaseDelay = TimeSpan.FromMilliseconds(10),
            UseHumanTiming = false,
            EnableTypos = false
        };

        public static TypingOptions VeryHuman => Default with
        {
            BaseDelay = TimeSpan.FromMilliseconds(100),
            EnableTypos = true,
            TypoChance = 0.02
        };
    }

    /// <summary>
    /// Кнопки мыши
    /// </summary>
    public enum MouseButton
    {
        Left,
        Right,
        Middle,
        X1,
        X2
    }

    /// <summary>
    /// Направления прокрутки
    /// </summary>
    public enum ScrollDirection
    {
        Vertical,
        Horizontal
    }

    /// <summary>
    /// Виртуальные клавиши
    /// </summary>
    public enum VirtualKey : ushort
    {
        // Основные клавиши
        None = 0x00,
        Cancel = 0x03,
        Back = 0x08,
        Tab = 0x09,
        Clear = 0x0C,
        Return = 0x0D,
        Shift = 0x10,
        Control = 0x11,
        Alt = 0x12,
        Pause = 0x13,
        Capital = 0x14,
        Escape = 0x1B,
        Space = 0x20,

        // Навигация
        Prior = 0x21,    // Page Up
        Next = 0x22,     // Page Down
        End = 0x23,
        Home = 0x24,
        Left = 0x25,
        Up = 0x26,
        Right = 0x27,
        Down = 0x28,
        Select = 0x29,
        Print = 0x2A,
        Execute = 0x2B,
        Snapshot = 0x2C,
        Insert = 0x2D,
        Delete = 0x2E,
        Help = 0x2F,

        // Цифры
        D0 = 0x30, D1 = 0x31, D2 = 0x32, D3 = 0x33, D4 = 0x34,
        D5 = 0x35, D6 = 0x36, D7 = 0x37, D8 = 0x38, D9 = 0x39,

        // Буквы
        A = 0x41, B = 0x42, C = 0x43, D = 0x44, E = 0x45,
        F = 0x46, G = 0x47, H = 0x48, I = 0x49, J = 0x4A,
        K = 0x4B, L = 0x4C, M = 0x4D, N = 0x4E, O = 0x4F,
        P = 0x50, Q = 0x51, R = 0x52, S = 0x53, T = 0x54,
        U = 0x55, V = 0x56, W = 0x57, X = 0x58, Y = 0x59, Z = 0x5A,

        // Функциональные клавиши
        F1 = 0x70, F2 = 0x71, F3 = 0x72, F4 = 0x73,
        F5 = 0x74, F6 = 0x75, F7 = 0x76, F8 = 0x77,
        F9 = 0x78, F10 = 0x79, F11 = 0x7A, F12 = 0x7B,

        // Numpad
        Numpad0 = 0x60, Numpad1 = 0x61, Numpad2 = 0x62, Numpad3 = 0x63, Numpad4 = 0x64,
        Numpad5 = 0x65, Numpad6 = 0x66, Numpad7 = 0x67, Numpad8 = 0x68, Numpad9 = 0x69,
        Multiply = 0x6A, Add = 0x6B, Separator = 0x6C, Subtract = 0x6D, Decimal = 0x6E, Divide = 0x6F
    }

    /// <summary>
    /// Результат операции ввода
    /// </summary>
    public sealed record InputResult
    {
        public bool Success { get; init; } = true;
        public Exception? Error { get; init; }
        public TimeSpan Duration { get; init; }
        public string? Operation { get; init; }

        public static InputResult Successful(TimeSpan duration, string? operation = null)
            => new() { Duration = duration, Operation = operation };

        public static InputResult Failed(Exception error, string? operation = null)
            => new() { Success = false, Error = error, Operation = operation };
    }

    /// <summary>
    /// Событие ввода
    /// </summary>
    public sealed record InputEvent
    {
        public required InputEventType Type { get; init; }
        public Point? Position { get; init; }
        public MouseButton? MouseButton { get; init; }
        public VirtualKey? Key { get; init; }
        public string? Text { get; init; }
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Типы событий ввода
    /// </summary>
    public enum InputEventType
    {
        MouseMove,
        MouseDown,
        MouseUp,
        MouseClick,
        MouseScroll,
        KeyDown,
        KeyUp,
        KeyPress,
        TextInput
    }
}