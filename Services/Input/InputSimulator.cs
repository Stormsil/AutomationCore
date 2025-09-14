// Services/Input/InputSimulator.cs
using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using AutomationCore.Core.Abstractions;
using AutomationCore.Core.Models;
using Microsoft.Extensions.Logging;

namespace AutomationCore.Services.Input
{
    /// <summary>
    /// Основная реализация симулятора ввода
    /// </summary>
    public sealed class InputSimulator : IInputSimulator
    {
        public IMouseSimulator Mouse { get; }
        public IKeyboardSimulator Keyboard { get; }
        public ICombinedInputSimulator Combined { get; }

        public InputSimulator(
            IMouseSimulator mouse,
            IKeyboardSimulator keyboard,
            ICombinedInputSimulator combined)
        {
            Mouse = mouse ?? throw new ArgumentNullException(nameof(mouse));
            Keyboard = keyboard ?? throw new ArgumentNullException(nameof(keyboard));
            Combined = combined ?? throw new ArgumentNullException(nameof(combined));
        }
    }

    /// <summary>
    /// Реализация симулятора мыши с человекоподобным поведением
    /// </summary>
    public sealed class HumanizedMouseSimulator : IMouseSimulator
    {
        private readonly IPlatformInputProvider _platform;
        private readonly IMouseTrajectoryBuilder _trajectoryBuilder;
        private readonly ILogger<HumanizedMouseSimulator> _logger;

        public Point CurrentPosition => _platform.GetCursorPosition();

        // События
        public event EventHandler<InputEvent>? MouseMoved;
        public event EventHandler<InputEvent>? MouseClicked;

        public HumanizedMouseSimulator(
            IPlatformInputProvider platform,
            IMouseTrajectoryBuilder trajectoryBuilder,
            ILogger<HumanizedMouseSimulator> logger)
        {
            _platform = platform ?? throw new ArgumentNullException(nameof(platform));
            _trajectoryBuilder = trajectoryBuilder ?? throw new ArgumentNullException(nameof(trajectoryBuilder));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async ValueTask<InputResult> MoveToAsync(Point target, MouseMoveOptions? options = null, CancellationToken ct = default)
        {
            options ??= MouseMoveOptions.Default;
            var startTime = DateTime.UtcNow;

            _logger.LogTrace("Moving mouse from {From} to {To}", CurrentPosition, target);

            try
            {
                var currentPos = CurrentPosition;
                if (currentPos == target)
                {
                    return InputResult.Successful(TimeSpan.Zero, "MouseMove");
                }

                // Строим траекторию движения
                var trajectory = _trajectoryBuilder.BuildTrajectory(currentPos, target, options);

                // Вычисляем время движения
                var duration = options.Duration ?? CalculateMoveDuration(currentPos, target, options);
                var stepDelay = duration.TotalMilliseconds / trajectory.Length;

                // Двигаем мышь по траектории
                foreach (var point in trajectory)
                {
                    ct.ThrowIfCancellationRequested();

                    _platform.SetCursorPosition(point);

                    MouseMoved?.Invoke(this, new InputEvent
                    {
                        Type = InputEventType.MouseMove,
                        Position = point,
                        Timestamp = DateTime.UtcNow
                    });

                    if (stepDelay > 1) // Пропускаем очень маленькие задержки
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(stepDelay), ct);
                    }
                }

                // Финальная коррекция позиции
                _platform.SetCursorPosition(target);

                var totalDuration = DateTime.UtcNow - startTime;
                _logger.LogTrace("Mouse moved in {Duration}ms", totalDuration.TotalMilliseconds);

                return InputResult.Successful(totalDuration, "MouseMove");
            }
            catch (OperationCanceledException)
            {
                _logger.LogTrace("Mouse movement was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                _logger.LogError(ex, "Failed to move mouse after {Duration}ms", duration.TotalMilliseconds);
                return InputResult.Failed(ex, "MouseMove");
            }
        }

        public async ValueTask<InputResult> ClickAsync(MouseButton button = MouseButton.Left, CancellationToken ct = default)
        {
            var startTime = DateTime.UtcNow;
            var position = CurrentPosition;

            _logger.LogTrace("Clicking {Button} button at {Position}", button, position);

            try
            {
                // Человеческая пауза перед кликом
                await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(50, 150)), ct);

                // Нажимаем кнопку
                _platform.MouseDown(button);

                // Человеческая задержка удержания
                var holdDuration = TimeSpan.FromMilliseconds(Random.Shared.Next(30, 120));
                await Task.Delay(holdDuration, ct);

                // Отпускаем кнопку
                _platform.MouseUp(button);

                // Пауза после клика
                await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(50, 100)), ct);

                MouseClicked?.Invoke(this, new InputEvent
                {
                    Type = InputEventType.MouseClick,
                    Position = position,
                    Button = button,
                    Timestamp = DateTime.UtcNow
                });

                var totalDuration = DateTime.UtcNow - startTime;
                _logger.LogTrace("Mouse clicked in {Duration}ms", totalDuration.TotalMilliseconds);

                return InputResult.Successful(totalDuration, $"MouseClick_{button}");
            }
            catch (OperationCanceledException)
            {
                _logger.LogTrace("Mouse click was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                _logger.LogError(ex, "Failed to click mouse after {Duration}ms", duration.TotalMilliseconds);
                return InputResult.Failed(ex, $"MouseClick_{button}");
            }
        }

        public async ValueTask<InputResult> DoubleClickAsync(MouseButton button = MouseButton.Left, CancellationToken ct = default)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                var firstClick = await ClickAsync(button, ct);
                if (!firstClick.Success)
                {
                    return firstClick;
                }

                // Пауза между кликами
                var betweenClicksDelay = TimeSpan.FromMilliseconds(Random.Shared.Next(100, 200));
                await Task.Delay(betweenClicksDelay, ct);

                var secondClick = await ClickAsync(button, ct);
                var totalDuration = DateTime.UtcNow - startTime;

                return InputResult.Successful(totalDuration, $"MouseDoubleClick_{button}");
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                return InputResult.Failed(ex, $"MouseDoubleClick_{button}");
            }
        }

        public async ValueTask<InputResult> MouseDownAsync(MouseButton button = MouseButton.Left, CancellationToken ct = default)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(20, 60)), ct);
                _platform.MouseDown(button);

                var duration = DateTime.UtcNow - startTime;
                return InputResult.Successful(duration, $"MouseDown_{button}");
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                return InputResult.Failed(ex, $"MouseDown_{button}");
            }
        }

        public async ValueTask<InputResult> MouseUpAsync(MouseButton button = MouseButton.Left, CancellationToken ct = default)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(20, 60)), ct);
                _platform.MouseUp(button);

                var duration = DateTime.UtcNow - startTime;
                return InputResult.Successful(duration, $"MouseUp_{button}");
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                return InputResult.Failed(ex, $"MouseUp_{button}");
            }
        }

        public async ValueTask<InputResult> DragAsync(Point from, Point to, DragOptions? options = null, CancellationToken ct = default)
        {
            options ??= DragOptions.Default;
            var startTime = DateTime.UtcNow;

            try
            {
                // Перемещаемся к начальной точке
                var moveResult = await MoveToAsync(from, options.MoveOptions, ct);
                if (!moveResult.Success)
                {
                    return moveResult;
                }

                // Нажимаем кнопку
                await MouseDownAsync(MouseButton.Left, ct);

                // Пауза после нажатия
                await Task.Delay(options.HoldDelayBeforeMove, ct);

                // Перетаскиваем
                var dragMoveOptions = options.MoveOptions with { Duration = options.Duration };
                var dragResult = await MoveToAsync(to, dragMoveOptions, ct);
                if (!dragResult.Success)
                {
                    return dragResult;
                }

                // Пауза перед отпусканием
                await Task.Delay(options.ReleaseDelay, ct);

                // Отпускаем кнопку
                await MouseUpAsync(MouseButton.Left, ct);

                var totalDuration = DateTime.UtcNow - startTime;
                return InputResult.Successful(totalDuration, "MouseDrag");
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                return InputResult.Failed(ex, "MouseDrag");
            }
        }

        public async ValueTask<InputResult> ScrollAsync(int amount, ScrollDirection direction = ScrollDirection.Vertical, CancellationToken ct = default)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(20, 60)), ct);
                _platform.Scroll(amount, direction);

                var duration = DateTime.UtcNow - startTime;
                return InputResult.Successful(duration, $"MouseScroll_{direction}");
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                return InputResult.Failed(ex, $"MouseScroll_{direction}");
            }
        }

        private static TimeSpan CalculateMoveDuration(Point from, Point to, MouseMoveOptions options)
        {
            var distance = Math.Sqrt(Math.Pow(to.X - from.X, 2) + Math.Pow(to.Y - from.Y, 2));
            var baseDuration = distance * options.SpeedMultiplier;

            return TimeSpan.FromMilliseconds(Math.Clamp(baseDuration, 100, 2000));
        }
    }

    /// <summary>
    /// Интерфейс платформенного провайдера ввода
    /// </summary>
    public interface IPlatformInputProvider
    {
        Point GetCursorPosition();
        void SetCursorPosition(Point position);
        void MouseDown(MouseButton button);
        void MouseUp(MouseButton button);
        void Scroll(int amount, ScrollDirection direction);
        void KeyDown(VirtualKey key);
        void KeyUp(VirtualKey key);
        void SendUnicodeChar(char character);
    }
}