using AutomationCore.Input;
using System.Drawing;

namespace AutomationCore.Core.Abstractions
{
    /// <summary>
    /// Интерфейс для симуляции ввода
    /// </summary>
    public interface IInputSimulator
    {
        IMouseSimulator Mouse { get; }
        IKeyboardSimulator Keyboard { get; }
    }

    public interface IMouseSimulator
    {
        Task MoveToAsync(Point target, MouseMoveOptions options = null);
        Task ClickAsync(MouseButton button = MouseButton.Left);
        Task DragAsync(Point from, Point to, DragOptions options = null);
        Task ScrollAsync(int amount, ScrollDirection direction = ScrollDirection.Vertical);
        Point CurrentPosition { get; }
    }

    public interface IKeyboardSimulator
    {
        Task TypeAsync(string text, TypingOptions options = null);
        Task PressKeyAsync(VirtualKey key);
        Task KeyCombinationAsync(params VirtualKey[] keys);
    }
}