// Windows Combined Input Simulator (extracted from monolith)
using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using AutomationCore.Core.Abstractions;
using AutomationCore.Core.Models;

namespace AutomationCore.Infrastructure.Input.Windows
{
    /// <summary>
    /// Комбинированный симулятор для сложных операций ввода
    /// </summary>
    internal sealed class WindowsCombinedInputSimulator : ICombinedInputSimulator
    {
        private readonly IMouseSimulator _mouse;
        private readonly IKeyboardSimulator _keyboard;

        public WindowsCombinedInputSimulator(IMouseSimulator mouse, IKeyboardSimulator keyboard)
        {
            _mouse = mouse ?? throw new ArgumentNullException(nameof(mouse));
            _keyboard = keyboard ?? throw new ArgumentNullException(nameof(keyboard));
        }

        public async ValueTask<InputResult> ClickAndTypeAsync(Point position, string text, TypingOptions? options = null, CancellationToken ct = default)
        {
            var startTime = DateTime.UtcNow;
            try
            {
                // Перемещаемся к позиции и кликаем
                var moveResult = await _mouse.MoveToAsync(position, null, ct);
                if (!moveResult.Success) return moveResult;
                var clickResult = await _mouse.ClickAsync(MouseButton.Left, ct);
                if (!clickResult.Success)
                        return InputResult.Failed(clickResult.Error, "ClickAndType (Click failed)") with { Duration = DateTime.UtcNow - startTime };

                // Небольшая пауза перед вводом
                var delay = options?.BaseDelay ?? TimeSpan.FromMilliseconds(100);
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, ct);

                // Вводим текст
                var typeResult = await _keyboard.TypeAsync(text, options, ct);
                if (!typeResult.Success)
                    return InputResult.Failed(typeResult.Error, "ClickAndType (Type failed)") with { Duration = DateTime.UtcNow - startTime };

                return InputResult.Successful(DateTime.UtcNow - startTime, $"ClickAndType at {position} with '{text}'");
            }
            catch (Exception ex)
            {
                return InputResult.Failed(ex, "ClickAndType") with { Duration = DateTime.UtcNow - startTime };
            }
        }

        public async ValueTask<InputResult> DragWithKeysAsync(Point from, Point to, VirtualKey[] keys, DragOptions? options = null, CancellationToken ct = default)
        {
            var startTime = DateTime.UtcNow;
            try
            {
                // Нажимаем клавиши-модификаторы
                if (keys?.Length > 0)
                {
                    foreach (var key in keys)
                    {
                        await _keyboard.KeyDownAsync(key, ct);
                    }
                }

                // Выполняем перетаскивание
                var dragResult = await _mouse.DragAsync(from, to, options, ct);

                // Отпускаем клавиши-модификаторы
                if (keys?.Length > 0)
                {
                    for (int i = keys.Length - 1; i >= 0; i--)
                    {
                        await _keyboard.KeyUpAsync(keys[i], ct);
                    }
                }

                return dragResult.Success
                    ? InputResult.Successful(DateTime.UtcNow - startTime, $"DragWithKeys from {from} to {to}")
                    : InputResult.Failed(dragResult.Error, "DragWithKeys") with { Duration = DateTime.UtcNow - startTime };
            }
            catch (Exception ex)
            {
                return InputResult.Failed(ex, "DragWithKeys") with { Duration = DateTime.UtcNow - startTime };
            }
        }

        public async ValueTask<InputResult> SelectAllAsync(CancellationToken ct = default)
        {
            var startTime = DateTime.UtcNow;
            try
            {
                // Ctrl+A для выделения всего
                var selectResult = await _keyboard.KeyCombinationAsync(
                    new[] { VirtualKey.LeftControl, VirtualKey.A },
                    ct);

                return selectResult.Success
                    ? InputResult.Successful(DateTime.UtcNow - startTime, "SelectAll")
                    : InputResult.Failed(selectResult.Error, "SelectAll") with { Duration = DateTime.UtcNow - startTime };
            }
            catch (Exception ex)
            {
                return InputResult.Failed(ex, "SelectAll") with { Duration = DateTime.UtcNow - startTime };
            }
        }

        public async ValueTask<InputResult> ReplaceSelectedTextAsync(string newText, TypingOptions? options = null, CancellationToken ct = default)
        {
            var startTime = DateTime.UtcNow;
            try
            {
                // Вводим новый текст (это автоматически заменит выделенный)
                var typeResult = await _keyboard.TypeAsync(newText, options, ct);

                return typeResult.Success
                    ? InputResult.Successful(DateTime.UtcNow - startTime, $"ReplaceSelectedText '{newText}'")
                    : InputResult.Failed(typeResult.Error, "ReplaceSelectedText") with { Duration = DateTime.UtcNow - startTime };
            }
            catch (Exception ex)
            {
                return InputResult.Failed(ex, "ReplaceSelectedText") with { Duration = DateTime.UtcNow - startTime };
            }
        }

        public async ValueTask<InputResult> RightClickAndWaitAsync(Point location, TimeSpan timeout, CancellationToken ct = default)
        {
            var startTime = DateTime.UtcNow;
            try
            {
                // Правый клик для открытия контекстного меню
                var moveResult = await _mouse.MoveToAsync(location, null, ct);
                if (!moveResult.Success) return moveResult;
                var rightClickResult = await _mouse.ClickAsync(MouseButton.Right, ct);
                if (!rightClickResult.Success)
                    return InputResult.Failed(rightClickResult.Error, "RightClickAndWait (RightClick failed)") with { Duration = DateTime.UtcNow - startTime };

                // Ждем появления меню
                await Task.Delay(timeout, ct);

                return InputResult.Successful(DateTime.UtcNow - startTime, $"RightClickAndWait at {location}");
            }
            catch (Exception ex)
            {
                return InputResult.Failed(ex, "RightClickAndWait") with { Duration = DateTime.UtcNow - startTime };
            }
        }

    }
}