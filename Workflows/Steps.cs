// AutomationCore/Workflows/Steps.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutomationCore.Core;
using AutomationCore.Core.Models;
using AutomationCore.Infrastructure.Input;

namespace AutomationCore.Workflows
{
    // ==== Базовые шаги ====

    internal sealed class WaitForImageStep : IWorkflowStep
    {
        private readonly string _key;
        private readonly TimeSpan _timeout;
        public string Name => $"WaitForImage({_key}, {_timeout.TotalMilliseconds}ms)";

        public WaitForImageStep(string key, TimeSpan timeout)
        {
            _key = key ?? throw new ArgumentNullException(nameof(key));
            _timeout = timeout <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(1) : timeout;
        }

        public async Task ExecuteAsync(WorkflowContext context)
        {
            // Лёгкая заглушка ожидания — функционал матчинга добавим позже
            await Task.Delay(_timeout, context.Cancellation);
        }
    }

    internal sealed class ClickImageStep : IWorkflowStep
    {
        private readonly string _key;
        public string Name => $"Click({_key})";
        public ClickImageStep(string key) => _key = key ?? throw new ArgumentNullException(nameof(key));

        public async Task ExecuteAsync(WorkflowContext context)
        {
            await context.Engine.ClickOnImageAsync(_key);
        }
    }

    internal sealed class TypeTextStep : IWorkflowStep
    {
        private readonly string _text;
        public string Name => $"Type(\"{_text}\")";
        public TypeTextStep(string text) => _text = text ?? string.Empty;

        public async Task ExecuteAsync(WorkflowContext context)
        {
            var kb = context.Engine?.Input?.Keyboard;
            if (kb != null)
            {
                await kb.TypeAsync(_text, new TypingOptions());
            }
        }
    }

    internal sealed class DelayStep : IWorkflowStep
    {
        private readonly TimeSpan _delay;
        public string Name => $"Delay({_delay.TotalMilliseconds}ms)";
        public DelayStep(TimeSpan delay) => _delay = delay;

        public async Task ExecuteAsync(WorkflowContext context)
        {
            if (_delay > TimeSpan.Zero)
                await Task.Delay(_delay, context.Cancellation);
        }
    }

    // ==== Составные шаги ====

    internal sealed class ConditionalStep : IWorkflowStep
    {
        private readonly Func<Task<bool>> _condition;
        public List<IWorkflowStep> ThenSteps { get; set; } = new();
        public string Name => "If(...)";

        public ConditionalStep(Func<Task<bool>> condition)
            => _condition = condition ?? throw new ArgumentNullException(nameof(condition));

        public async Task ExecuteAsync(WorkflowContext context)
        {
            if (await _condition().ConfigureAwait(false))
            {
                foreach (var step in ThenSteps)
                {
                    await step.ExecuteAsync(context).ConfigureAwait(false);
                }
            }
        }
    }

    internal sealed class RetryStep : IWorkflowStep
    {
        private readonly int _times;
        public List<IWorkflowStep> Steps { get; set; } = new();
        public string Name => $"Retry(x{_times})";

        public RetryStep(int times) => _times = Math.Max(1, times);

        public async Task ExecuteAsync(WorkflowContext context)
        {
            Exception last = null;

            for (int attempt = 1; attempt <= _times; attempt++)
            {
                try
                {
                    foreach (var step in Steps)
                        await step.ExecuteAsync(context).ConfigureAwait(false);
                    return; // успех
                }
                catch (Exception ex)
                {
                    last = ex;
                    // небольшая пауза между попытками
                    await Task.Delay(150, context.Cancellation).ConfigureAwait(false);
                }
            }

            // если все попытки неудачны — пробрасываем последнюю ошибку
            throw last ?? new InvalidOperationException("Retry failed without exception.");
        }
    }
}
