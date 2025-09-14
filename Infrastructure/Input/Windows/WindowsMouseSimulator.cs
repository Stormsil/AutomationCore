// Windows Mouse Simulator (extracted from monolith)
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AutomationCore.Core.Abstractions;
using AutomationCore.Core.Models;

namespace AutomationCore.Infrastructure.Input.Windows
{
    /// <summary>
    /// Реализация симуляции мыши для Windows через Win32 API
    /// </summary>
    internal sealed class WindowsMouseSimulator : IMouseSimulator
    {
        public Point CurrentPosition => GetCurrentCursorPos();

        public event EventHandler<InputEvent>? MouseMoved;
        public event EventHandler<InputEvent>? MouseClicked;

        public async ValueTask<InputResult> MoveToAsync(Point position, MouseMoveOptions? options = null, CancellationToken ct = default)
        {
            var startTime = DateTime.UtcNow;
            try
            {
                if (options?.Smooth == true)
                {
                    await SmoothMoveToAsync(position, options, ct);
                }
                else
                {
                    SetCursorPos(position.X, position.Y);
                    await Task.Delay(options?.Duration ?? TimeSpan.FromMilliseconds(10), ct);
                }

                MouseMoved?.Invoke(this, new InputEvent
                {
                    Type = InputEventType.MouseMove,
                    Position = position,
                    Timestamp = DateTime.UtcNow
                });

                return InputResult.Successful(DateTime.UtcNow - startTime, "MouseMove");
            }
            catch (Exception ex)
            {
                return InputResult.Failed(ex, "MouseMove") with { Duration = DateTime.UtcNow - startTime };
            }
        }

        public async ValueTask<InputResult> ClickAsync(MouseButton button = MouseButton.Left, CancellationToken ct = default)
        {
            var startTime = DateTime.UtcNow;
            try
            {
                var actualPosition = CurrentPosition;

                // Выполняем клик
                var (downFlag, upFlag) = GetMouseButtonFlags(button);

                mouse_event(downFlag, (uint)actualPosition.X, (uint)actualPosition.Y, 0, UIntPtr.Zero);
                await Task.Delay(50, ct); // Небольшая задержка между нажатием и отпусканием
                mouse_event(upFlag, (uint)actualPosition.X, (uint)actualPosition.Y, 0, UIntPtr.Zero);

                MouseClicked?.Invoke(this, new InputEvent
                {
                    Type = InputEventType.MouseClick,
                    Position = actualPosition,
                    Button = button,
                    Timestamp = DateTime.UtcNow
                });

                return InputResult.Successful(DateTime.UtcNow - startTime, $"Click {button}");
            }
            catch (Exception ex)
            {
                return InputResult.Failed(ex, $"Click {button}") with { Duration = DateTime.UtcNow - startTime };
            }
        }

        public async ValueTask<InputResult> DoubleClickAsync(MouseButton button = MouseButton.Left, CancellationToken ct = default)
        {
            var startTime = DateTime.UtcNow;
            try
            {
                var firstClick = await ClickAsync(button, ct);
                if (!firstClick.Success) return firstClick;

                await Task.Delay(50, ct); // Пауза между кликами

                var secondClick = await ClickAsync(button, ct);
                if (!secondClick.Success) return secondClick;

                return InputResult.Successful(DateTime.UtcNow - startTime, $"DoubleClick {button}");
            }
            catch (Exception ex)
            {
                return InputResult.Failed(ex, $"DoubleClick {button}") with { Duration = DateTime.UtcNow - startTime };
            }
        }

        public async ValueTask<InputResult> MouseDownAsync(MouseButton button = MouseButton.Left, CancellationToken ct = default)
        {
            var startTime = DateTime.UtcNow;
            try
            {
                var currentPos = CurrentPosition;
                var (downFlag, _) = GetMouseButtonFlags(button);
                mouse_event(downFlag, (uint)currentPos.X, (uint)currentPos.Y, 0, UIntPtr.Zero);
                return InputResult.Successful(DateTime.UtcNow - startTime, $"MouseDown {button}");
            }
            catch (Exception ex)
            {
                return InputResult.Failed(ex, $"MouseDown {button}") with { Duration = DateTime.UtcNow - startTime };
            }
        }

        public async ValueTask<InputResult> MouseUpAsync(MouseButton button = MouseButton.Left, CancellationToken ct = default)
        {
            var startTime = DateTime.UtcNow;
            try
            {
                var currentPos = CurrentPosition;
                var (_, upFlag) = GetMouseButtonFlags(button);
                mouse_event(upFlag, (uint)currentPos.X, (uint)currentPos.Y, 0, UIntPtr.Zero);
                return InputResult.Successful(DateTime.UtcNow - startTime, $"MouseUp {button}");
            }
            catch (Exception ex)
            {
                return InputResult.Failed(ex, $"MouseUp {button}") with { Duration = DateTime.UtcNow - startTime };
            }
        }

        public async ValueTask<InputResult> DragAsync(Point from, Point to, DragOptions? options = null, CancellationToken ct = default)
        {
            var startTime = DateTime.UtcNow;
            try
            {
                // Перемещаемся к начальной позиции
                await MoveToAsync(fromPosition, null, ct);

                // Нажимаем кнопку мыши
                var (downFlag, upFlag) = GetMouseButtonFlags(button);
                mouse_event(downFlag, (uint)fromPosition.X, (uint)fromPosition.Y, 0, UIntPtr.Zero);

                await Task.Delay(50, ct);

                // Плавно перемещаемся к конечной позиции
                await SmoothMoveToAsync(toPosition, new MouseMoveOptions { Smooth = true, Duration = TimeSpan.FromMilliseconds(200) }, ct);

                // Отпускаем кнопку мыши
                mouse_event(upFlag, (uint)toPosition.X, (uint)toPosition.Y, 0, UIntPtr.Zero);

                return InputResult.Successful(DateTime.UtcNow - startTime, $"Drag {button}");
            }
            catch (Exception ex)
            {
                return InputResult.Failed(ex, $"Drag") with { Duration = DateTime.UtcNow - startTime };
            }
        }

        public async ValueTask<InputResult> ScrollAsync(int amount, ScrollDirection direction = ScrollDirection.Vertical, CancellationToken ct = default)
        {
            var startTime = DateTime.UtcNow;
            try
            {
                var currentPos = CurrentPosition;
                uint scrollData = (uint)(amount * 120); // Windows scroll units

                if (direction == ScrollDirection.Vertical)
                {
                    mouse_event(MOUSEEVENTF_WHEEL, (uint)currentPos.X, (uint)currentPos.Y, scrollData, UIntPtr.Zero);
                }
                else // Horizontal
                {
                    mouse_event(MOUSEEVENTF_HWHEEL, (uint)currentPos.X, (uint)currentPos.Y, scrollData, UIntPtr.Zero);
                }

                return InputResult.Successful(DateTime.UtcNow - startTime, $"Scroll {direction} {amount}");
            }
            catch (Exception ex)
            {
                return InputResult.Failed(ex, $"Scroll {direction}") with { Duration = DateTime.UtcNow - startTime };
            }
        }

        private async Task SmoothMoveToAsync(Point targetPosition, MouseMoveOptions? options, CancellationToken ct)
        {
            var currentPos = CurrentPosition;
            var distance = Math.Sqrt(Math.Pow(targetPosition.X - currentPos.X, 2) + Math.Pow(targetPosition.Y - currentPos.Y, 2));

            if (distance < 5) // Если расстояние мало, просто перемещаемся
            {
                SetCursorPos(targetPosition.X, targetPosition.Y);
                return;
            }

            var duration = options?.Duration ?? TimeSpan.FromMilliseconds(Math.Min(500, distance * 2));
            var steps = Math.Max(5, (int)(distance / 10));
            var stepDelay = duration.TotalMilliseconds / steps;

            for (int i = 0; i <= steps; i++)
            {
                ct.ThrowIfCancellationRequested();

                var progress = (double)i / steps;
                var x = (int)(currentPos.X + (targetPosition.X - currentPos.X) * progress);
                var y = (int)(currentPos.Y + (targetPosition.Y - currentPos.Y) * progress);

                SetCursorPos(x, y);

                if (i < steps)
                    await Task.Delay((int)stepDelay, ct);
            }
        }

        private static Point GetCurrentCursorPos()
        {
            GetCursorPos(out var point);
            return new Point(point.X, point.Y);
        }

        private static (uint down, uint up) GetMouseButtonFlags(MouseButton button)
        {
            return button switch
            {
                MouseButton.Left => (MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP),
                MouseButton.Right => (MOUSEEVENTF_RIGHTDOWN, MOUSEEVENTF_RIGHTUP),
                MouseButton.Middle => (MOUSEEVENTF_MIDDLEDOWN, MOUSEEVENTF_MIDDLEUP),
                _ => (MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP)
            };
        }

        // Win32 API
        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        private const uint MOUSEEVENTF_LEFTDOWN = 0x02;
        private const uint MOUSEEVENTF_LEFTUP = 0x04;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x08;
        private const uint MOUSEEVENTF_RIGHTUP = 0x10;
        private const uint MOUSEEVENTF_MIDDLEDOWN = 0x20;
        private const uint MOUSEEVENTF_MIDDLEUP = 0x40;
        private const uint MOUSEEVENTF_WHEEL = 0x800;
        private const uint MOUSEEVENTF_HWHEEL = 0x01000;
    }
}