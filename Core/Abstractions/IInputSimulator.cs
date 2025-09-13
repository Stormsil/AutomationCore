// Core/Abstractions/IInputSimulator.cs
using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using AutomationCore.Core.Models;

namespace AutomationCore.Core.Abstractions
{
    /// <summary>
    /// Главный интерфейс для симуляции ввода
    /// </summary>
    public interface IInputSimulator
    {
        /// <summary>Симулятор мыши</summary>
        IMouseSimulator Mouse { get; }

        /// <summary>Симулятор клавиатуры</summary>
        IKeyboardSimulator Keyboard { get; }

        /// <summary>Комбинированные операции</summary>
        ICombinedInputSimulator Combined { get; }
    }

    /// <summary>
    /// Симулятор мыши
    /// </summary>
    public interface IMouseSimulator
    {
        /// <summary>Текущая позиция курсора</summary>
        Point CurrentPosition { get; }

        /// <summary>Перемещает мышь в указанную точку</summary>
        ValueTask<InputResult> MoveToAsync(Point target, MouseMoveOptions? options = null, CancellationToken ct = default);

        /// <summary>Кликает указанной кнопкой мыши</summary>
        ValueTask<InputResult> ClickAsync(MouseButton button = MouseButton.Left, CancellationToken ct = default);

        /// <summary>Двойной клик</summary>
        ValueTask<InputResult> DoubleClickAsync(MouseButton button = MouseButton.Left, CancellationToken ct = default);

        /// <summary>Нажимает кнопку мыши (без отпускания)</summary>
        ValueTask<InputResult> MouseDownAsync(MouseButton button = MouseButton.Left, CancellationToken ct = default);

        /// <summary>Отпускает кнопку мыши</summary>
        ValueTask<InputResult> MouseUpAsync(MouseButton button = MouseButton.Left, CancellationToken ct = default);

        /// <summary>Перетаскивает объект</summary>
        ValueTask<InputResult> DragAsync(Point from, Point to, DragOptions? options = null, CancellationToken ct = default);

        /// <summary>Прокручивает колесико мыши</summary>
        ValueTask<InputResult> ScrollAsync(int amount, ScrollDirection direction = ScrollDirection.Vertical, CancellationToken ct = default);

        /// <summary>Событие движения мыши</summary>
        event EventHandler<InputEvent>? MouseMoved;

        /// <summary>Событие клика мыши</summary>
        event EventHandler<InputEvent>? MouseClicked;
    }

    /// <summary>
    /// Симулятор клавиатуры
    /// </summary>
    public interface IKeyboardSimulator
    {
        /// <summary>Печатает текст</summary>
        ValueTask<InputResult> TypeAsync(string text, TypingOptions? options = null, CancellationToken ct = default);

        /// <summary>Нажимает клавишу</summary>
        ValueTask<InputResult> PressKeyAsync(VirtualKey key, CancellationToken ct = default);

        /// <summary>Нажимает клавишу (без отпускания)</summary>
        ValueTask<InputResult> KeyDownAsync(VirtualKey key, CancellationToken ct = default);

        /// <summary>Отпускает клавишу</summary>
        ValueTask<InputResult> KeyUpAsync(VirtualKey key, CancellationToken ct = default);

        /// <summary>Выполняет комбинацию клавиш</summary>
        ValueTask<InputResult> KeyCombinationAsync(VirtualKey[] keys, CancellationToken ct = default);

        /// <summary>Копирует в буфер обмена</summary>
        ValueTask<InputResult> CopyAsync(CancellationToken ct = default);

        /// <summary>Вставляет из буфера обмена</summary>
        ValueTask<InputResult> PasteAsync(CancellationToken ct = default);

        /// <summary>Вырезает в буфер обмена</summary>
        ValueTask<InputResult> CutAsync(CancellationToken ct = default);

        /// <summary>Отменяет последнее действие</summary>
        ValueTask<InputResult> UndoAsync(CancellationToken ct = default);

        /// <summary>Повторяет отмененное действие</summary>
        ValueTask<InputResult> RedoAsync(CancellationToken ct = default);

        /// <summary>Событие нажатия клавиши</summary>
        event EventHandler<InputEvent>? KeyPressed;

        /// <summary>Событие ввода текста</summary>
        event EventHandler<InputEvent>? TextTyped;
    }

    /// <summary>
    /// Комбинированные операции ввода
    /// </summary>
    public interface ICombinedInputSimulator
    {
        /// <summary>Кликает и сразу печатает текст</summary>
        ValueTask<InputResult> ClickAndTypeAsync(Point location, string text, TypingOptions? typingOptions = null, CancellationToken ct = default);

        /// <summary>Перетаскивает с удержанием клавиш</summary>
        ValueTask<InputResult> DragWithKeysAsync(Point from, Point to, VirtualKey[] keys, DragOptions? options = null, CancellationToken ct = default);

        /// <summary>Выделяет весь текст</summary>
        ValueTask<InputResult> SelectAllAsync(CancellationToken ct = default);

        /// <summary>Заменяет выделенный текст</summary>
        ValueTask<InputResult> ReplaceSelectedTextAsync(string newText, TypingOptions? options = null, CancellationToken ct = default);

        /// <summary>Кликает правой кнопкой и ждет контекстное меню</summary>
        ValueTask<InputResult> RightClickAndWaitAsync(Point location, TimeSpan timeout, CancellationToken ct = default);
    }

    /// <summary>
    /// Построитель траекторий движения мыши
    /// </summary>
    public interface IMouseTrajectoryBuilder
    {
        /// <summary>Строит траекторию от точки к точке</summary>
        Point[] BuildTrajectory(Point from, Point to, MouseMoveOptions options);

        /// <summary>Строит сглаженную траекторию через множество точек</summary>
        Point[] BuildSmoothTrajectory(Point[] waypoints, MouseMoveOptions options);

        /// <summary>Добавляет человеческие отклонения к траектории</summary>
        Point[] AddHumanVariations(Point[] trajectory, MouseMoveOptions options);
    }

    /// <summary>
    /// Генератор человеческих паттернов ввода
    /// </summary>
    public interface IHumanInputPatternGenerator
    {
        /// <summary>Генерирует случайные задержки для печати</summary>
        TimeSpan[] GenerateTypingDelays(string text, TypingOptions options);

        /// <summary>Генерирует потенциальные опечатки</summary>
        string GenerateTypoVersion(string text, TypingOptions options);

        /// <summary>Генерирует вариации времени удержания клавиш</summary>
        TimeSpan GenerateKeyHoldDuration(VirtualKey key, TypingOptions options);

        /// <summary>Генерирует паузы "размышления"</summary>
        TimeSpan GenerateThinkingPause(string contextBefore, string contextAfter, TypingOptions options);
    }
}