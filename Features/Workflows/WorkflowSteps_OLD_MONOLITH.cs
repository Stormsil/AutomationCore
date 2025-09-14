// Features/Workflows/WorkflowSteps.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutomationCore.Core.Abstractions;
using AutomationCore.Core.Models;
using AutomationCore.Features.ImageSearch;
using AutomationCore.Features.WindowAutomation;

namespace AutomationCore.Features.Workflows
{
    #region Basic Steps

    /// <summary>
    /// Шаг ожидания появления изображения
    /// </summary>
    internal sealed class WaitForImageStep : IWorkflowStep
    {
        private readonly string _templateKey;
        private readonly TimeSpan _timeout;

        public string Name => $"WaitForImage({_templateKey}, {_timeout.TotalSeconds:F1}s)";

        public WaitForImageStep(string templateKey, TimeSpan timeout)
        {
            _templateKey = templateKey ?? throw new ArgumentNullException(nameof(templateKey));
            _timeout = timeout;
        }

        public async ValueTask ExecuteAsync(IWorkflowContext context, CancellationToken ct = default)
        {
            var imageSearch = context.GetService<ImageSearchEngine>();
            var result = await imageSearch.WaitForAsync(_templateKey, _timeout, cancellationToken: ct);

            if (!result.Success)
            {
                throw new WorkflowStepException($"Image '{_templateKey}' did not appear within {_timeout}");
            }

            // Сохраняем результат в контекст
            if (result.Location != null)
            {
                context.SetVariable($"LastFoundImage_{_templateKey}", result.Location);
            }
        }
    }

    /// <summary>
    /// Шаг клика по изображению
    /// </summary>
    internal sealed class ClickOnImageStep : IWorkflowStep
    {
        private readonly string _templateKey;

        public string Name => $"ClickOnImage({_templateKey})";

        public ClickOnImageStep(string templateKey)
        {
            _templateKey = templateKey ?? throw new ArgumentNullException(nameof(templateKey));
        }

        public async ValueTask ExecuteAsync(IWorkflowContext context, CancellationToken ct = default)
        {
            var imageSearch = context.GetService<ImageSearchEngine>();
            var input = context.GetService<IInputSimulator>();

            // Сначала ищем изображение
            var searchResult = await imageSearch.FindAsync(_templateKey, cancellationToken: ct);

            if (!searchResult.Success || searchResult.Location == null)
            {
                throw new WorkflowStepException($"Image '{_templateKey}' not found on screen");
            }

            // Кликаем по центру найденного изображения
            var clickResult = await input.Mouse.ClickAsync(MouseButton.Left, ct);

            if (!clickResult.Success)
            {
                throw new WorkflowStepException($"Failed to click on image '{_templateKey}': {clickResult.Error?.Message}");
            }

            // Сохраняем координаты клика
            context.SetVariable("LastClickPosition", searchResult.Location.Center);
        }
    }

    /// <summary>
    /// Шаг ввода текста
    /// </summary>
    internal sealed class TypeTextStep : IWorkflowStep
    {
        private readonly string _text;

        public string Name => $"Type(\"{(_text.Length > 20 ? _text[..20] + "..." : _text)}\")";

        public TypeTextStep(string text)
        {
            _text = text ?? throw new ArgumentNullException(nameof(text));
        }

        public async ValueTask ExecuteAsync(IWorkflowContext context, CancellationToken ct = default)
        {
            var input = context.GetService<IInputSimulator>();
            var result = await input.Keyboard.TypeAsync(_text, cancellationToken: ct);

            if (!result.Success)
            {
                throw new WorkflowStepException($"Failed to type text: {result.Error?.Message}");
            }
        }
    }

    /// <summary>
    /// Шаг задержки
    /// </summary>
    internal sealed class DelayStep : IWorkflowStep
    {
        private readonly TimeSpan _delay;

        public string Name => $"Delay({_delay.TotalMilliseconds}ms)";

        public DelayStep(TimeSpan delay)
        {
            _delay = delay;
        }

        public async ValueTask ExecuteAsync(IWorkflowContext context, CancellationToken ct = default)
        {
            if (_delay > TimeSpan.Zero)
            {
                await Task.Delay(_delay, ct);
            }
        }
    }

    /// <summary>
    /// Шаг нажатия комбинации клавиш
    /// </summary>
    internal sealed class KeyCombinationStep : IWorkflowStep
    {
        private readonly VirtualKey[] _keys;

        public string Name => $"PressKeys({string.Join("+", _keys)})";

        public KeyCombinationStep(VirtualKey[] keys)
        {
            _keys = keys ?? throw new ArgumentNullException(nameof(keys));
        }

        public async ValueTask ExecuteAsync(IWorkflowContext context, CancellationToken ct = default)
        {
            var input = context.GetService<IInputSimulator>();
            var result = await input.Keyboard.KeyCombinationAsync(_keys, ct);

            if (!result.Success)
            {
                throw new WorkflowStepException($"Failed to press key combination: {result.Error?.Message}");
            }
        }
    }

    #endregion

    #region Window Steps

    /// <summary>
    /// Шаг активации окна
    /// </summary>
    internal sealed class ActivateWindowStep : IWorkflowStep
    {
        private readonly string _windowTitle;

        public string Name => $"ActivateWindow({_windowTitle})";

        public ActivateWindowStep(string windowTitle)
        {
            _windowTitle = windowTitle ?? throw new ArgumentNullException(nameof(windowTitle));
        }

        public async ValueTask ExecuteAsync(IWorkflowContext context, CancellationToken ct = default)
        {
            var windowManager = context.GetService<IWindowManager>();

            var criteria = WindowSearchCriteria.WithTitle(_windowTitle);
            var windows = await windowManager.FindWindowsAsync(criteria, ct);

            if (windows.Count == 0)
            {
                throw new WorkflowStepException($"Window '{_windowTitle}' not found");
            }

            var window = windows[0];
            var result = await windowManager.PerformWindowOperationAsync(window.Handle, WindowOperation.BringToFront, ct);

            if (!result)
            {
                throw new WorkflowStepException($"Failed to activate window '{_windowTitle}'");
            }

            // Сохраняем информацию об активном окне
            context.SetVariable("ActiveWindow", window);
        }
    }

    /// <summary>
    /// Шаг клика по изображению в окне
    /// </summary>
    internal sealed class ClickOnImageInWindowStep : IWorkflowStep
    {
        private readonly string _windowTitle;
        private readonly string _templateKey;

        public string Name => $"ClickOnImageInWindow({_windowTitle}, {_templateKey})";

        public ClickOnImageInWindowStep(string windowTitle, string templateKey)
        {
            _windowTitle = windowTitle ?? throw new ArgumentNullException(nameof(windowTitle));
            _templateKey = templateKey ?? throw new ArgumentNullException(nameof(templateKey));
        }

        public async ValueTask ExecuteAsync(IWorkflowContext context, CancellationToken ct = default)
        {
            var windowAutomator = context.GetService<WindowAutomator>();
            var result = await windowAutomator.ClickOnImageInWindowAsync(_windowTitle, _templateKey, cancellationToken: ct);

            if (!result.Success)
            {
                throw new WorkflowStepException($"Failed to click on image '{_templateKey}' in window '{_windowTitle}': {result.ErrorMessage}");
            }

            context.SetVariable("LastWindowOperation", result);
        }
    }

    /// <summary>
    /// Шаг ввода текста в окне
    /// </summary>
    internal sealed class TypeInWindowStep : IWorkflowStep
    {
        private readonly string _windowTitle;
        private readonly string _text;

        public string Name => $"TypeInWindow({_windowTitle}, \"{(_text.Length > 15 ? _text[..15] + "..." : _text)}\")";

        public TypeInWindowStep(string windowTitle, string text)
        {
            _windowTitle = windowTitle ?? throw new ArgumentNullException(nameof(windowTitle));
            _text = text ?? throw new ArgumentNullException(nameof(text));
        }

        public async ValueTask ExecuteAsync(IWorkflowContext context, CancellationToken ct = default)
        {
            var windowAutomator = context.GetService<WindowAutomator>();
            var result = await windowAutomator.TypeInWindowAsync(_windowTitle, _text, cancellationToken: ct);

            if (!result.Success)
            {
                throw new WorkflowStepException($"Failed to type text in window '{_windowTitle}': {result.ErrorMessage}");
            }
        }
    }

    #endregion

    #region Control Flow Steps

    /// <summary>
    /// Условный шаг
    /// </summary>
    internal sealed class ConditionalStep : IWorkflowStep
    {
        private readonly Func<IWorkflowContext, ValueTask<bool>> _condition;
        public List<IWorkflowStep> ThenSteps { get; } = new();

        public string Name => "If(condition)";

        public ConditionalStep(Func<IWorkflowContext, ValueTask<bool>> condition)
        {
            _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        }

        public async ValueTask ExecuteAsync(IWorkflowContext context, CancellationToken ct = default)
        {
            if (await _condition(context))
            {
                foreach (var step in ThenSteps)
                {
                    await step.ExecuteAsync(context, ct);
                }
            }
        }
    }

    /// <summary>
    /// Шаг повторных попыток
    /// </summary>
    internal sealed class RetryStep : IWorkflowStep
    {
        private readonly int _maxAttempts;
        public List<IWorkflowStep> Steps { get; } = new();

        public string Name => $"Retry(x{_maxAttempts})";

        public RetryStep(int maxAttempts)
        {
            _maxAttempts = Math.Max(1, maxAttempts);
        }

        public async ValueTask ExecuteAsync(IWorkflowContext context, CancellationToken ct = default)
        {
            Exception? lastException = null;

            for (int attempt = 1; attempt <= _maxAttempts; attempt++)
            {
                try
                {
                    foreach (var step in Steps)
                    {
                        await step.ExecuteAsync(context, ct);
                    }

                    // Все шаги успешно выполнены
                    context.SetVariable("RetryAttempt", attempt);
                    return;
                }
                catch (Exception ex)
                {
                    lastException = ex;

                    // Если это не последняя попытка, ждем перед повтором
                    if (attempt < _maxAttempts)
                    {
                        var delay = TimeSpan.FromMilliseconds(500 * attempt); // Экспоненциальная задержка
                        await Task.Delay(delay, ct);
                    }
                }
            }

            // Все попытки исчерпаны
            throw new WorkflowStepException($"Retry failed after {_maxAttempts} attempts", lastException);
        }
    }

    /// <summary>
    /// Параллельное выполнение шагов
    /// </summary>
    internal sealed class ParallelStep : IWorkflowStep
    {
        public List<IWorkflowStep> Steps { get; } = new();

        public string Name => $"Parallel({Steps.Count} steps)";

        public async ValueTask ExecuteAsync(IWorkflowContext context, CancellationToken ct = default)
        {
            if (Steps.Count == 0) return;

            var tasks = Steps.Select(step => ExecuteStepSafely(step, context, ct)).ToArray();
            var results = await Task.WhenAll(tasks);

            // Проверяем наличие ошибок
            var exceptions = results.Where(r => r != null).ToArray();
            if (exceptions.Length > 0)
            {
                throw new AggregateException("One or more parallel steps failed", exceptions);
            }
        }

        private static async Task<Exception?> ExecuteStepSafely(IWorkflowStep step, IWorkflowContext context, CancellationToken ct)
        {
            try
            {
                await step.ExecuteAsync(context, ct);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }
    }

    /// <summary>
    /// Цикл while
    /// </summary>
    internal sealed class WhileStep : IWorkflowStep
    {
        private readonly Func<IWorkflowContext, ValueTask<bool>> _condition;
        public List<IWorkflowStep> Steps { get; } = new();

        public string Name => "While(condition)";

        public WhileStep(Func<IWorkflowContext, ValueTask<bool>> condition)
        {
            _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        }

        public async ValueTask ExecuteAsync(IWorkflowContext context, CancellationToken ct = default)
        {
            int iteration = 0;
            const int maxIterations = 1000; // Защита от бесконечного цикла

            while (await _condition(context) && iteration < maxIterations)
            {
                ct.ThrowIfCancellationRequested();

                foreach (var step in Steps)
                {
                    await step.ExecuteAsync(context, ct);
                }

                iteration++;
                context.SetVariable("WhileIteration", iteration);
            }

            if (iteration >= maxIterations)
            {
                throw new WorkflowStepException($"While loop exceeded maximum iterations ({maxIterations})");
            }
        }
    }

    #endregion

    #region Custom Steps

    /// <summary>
    /// Кастомный шаг действия
    /// </summary>
    internal sealed class CustomActionStep : IWorkflowStep
    {
        private readonly string _name;
        private readonly Func<IWorkflowContext, ValueTask> _action;

        public string Name => _name;

        public CustomActionStep(string name, Func<IWorkflowContext, ValueTask> action)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _action = action ?? throw new ArgumentNullException(nameof(action));
        }

        public async ValueTask ExecuteAsync(IWorkflowContext context, CancellationToken ct = default)
        {
            var originalCt = context.CancellationToken;
            try
            {
                context.CancellationToken = ct;
                await _action(context);
            }
            finally
            {
                context.CancellationToken = originalCt;
            }
        }
    }

    /// <summary>
    /// Кастомный шаг с возвратом значения
    /// </summary>
    internal sealed class CustomActionStep<T> : IWorkflowStep
    {
        private readonly string _name;
        private readonly Func<IWorkflowContext, ValueTask<T>> _action;
        private readonly string? _storeKey;

        public string Name => _name;

        public CustomActionStep(string name, Func<IWorkflowContext, ValueTask<T>> action, string? storeKey = null)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _action = action ?? throw new ArgumentNullException(nameof(action));
            _storeKey = storeKey;
        }

        public async ValueTask ExecuteAsync(IWorkflowContext context, CancellationToken ct = default)
        {
            var originalCt = context.CancellationToken;
            try
            {
                context.CancellationToken = ct;
                var result = await _action(context);

                // Сохраняем результат в контекст если указан ключ
                if (!string.IsNullOrEmpty(_storeKey) && result != null)
                {
                    context.SetVariable(_storeKey, result);
                }
            }
            finally
            {
                context.CancellationToken = originalCt;
            }
        }
    }

    #endregion

    #region Exception

    /// <summary>
    /// Исключение шага workflow
    /// </summary>
    public sealed class WorkflowStepException : Exception
    {
        public WorkflowStepException(string message) : base(message) { }
        public WorkflowStepException(string message, Exception? innerException) : base(message, innerException) { }
    }

    #endregion
}