// Input Options - параметры для различных типов ввода
using System;

namespace AutomationCore.Core.Models
{
    /// <summary>
    /// Опции для операций с клавиатурой
    /// </summary>
    public sealed record KeyboardOptions
    {
        public TimeSpan KeyHoldDuration { get; init; } = TimeSpan.FromMilliseconds(50);
        public TimeSpan TypeDelay { get; init; } = TimeSpan.FromMilliseconds(50);
        public bool UseUnicodeInput { get; init; } = true;
    }

    /// <summary>
    /// Опции для операций с мышью
    /// </summary>

    /// <summary>
    /// Опции для комбинированных операций ввода
    /// </summary>
    public sealed record CombinedInputOptions
    {
        public TimeSpan DelayBetweenOperations { get; init; } = TimeSpan.FromMilliseconds(100);
        public TimeSpan MenuDelay { get; init; } = TimeSpan.FromMilliseconds(200);
        public KeyboardOptions? KeyboardOptions { get; init; }
        public MouseMoveOptions? MouseOptions { get; init; }
    }

    /// <summary>
    /// Опции для операций ввода текста
    /// </summary>

    /// <summary>
    /// Опции для операций перетаскивания
    /// </summary>

    /// <summary>
    /// Общие опции для операций ввода
    /// </summary>
    public sealed record InputOptions
    {
        public KeyboardOptions Keyboard { get; init; } = new();
        public MouseMoveOptions Mouse { get; init; } = new();
        public TypingOptions Typing { get; init; } = new();
        public DragOptions Drag { get; init; } = new();
        public bool EnableDebugLogging { get; init; } = false;
    }
}