// Control workflow step - retry logic
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutomationCore.Core.Abstractions;
using AutomationCore.Features.Workflows;

namespace AutomationCore.Features.Workflows.Steps.Control
{
    /// <summary>
    /// Шаг повторных попыток выполнения
    /// </summary>
    public sealed class RetryStep : IWorkflowStep
    {
        private readonly int _maxAttempts;
        private readonly TimeSpan _baseDelay;
        private readonly bool _useExponentialBackoff;

        public List<IWorkflowStep> Steps { get; } = new();
        public string Name => $"Retry(x{_maxAttempts})";

        public RetryStep(int maxAttempts, TimeSpan? baseDelay = null, bool useExponentialBackoff = true)
        {
            _maxAttempts = Math.Max(1, maxAttempts);
            _baseDelay = baseDelay ?? TimeSpan.FromMilliseconds(500);
            _useExponentialBackoff = useExponentialBackoff;
        }

        public async ValueTask ExecuteAsync(IWorkflowContext context, CancellationToken ct = default)
        {
            Exception? lastException = null;

            for (int attempt = 1; attempt <= _maxAttempts; attempt++)
            {
                try
                {
                    context.SetVariable("CurrentRetryAttempt", attempt);

                    foreach (var step in Steps)
                    {
                        ct.ThrowIfCancellationRequested();
                        await step.ExecuteAsync(context, ct);
                    }

                    // Все шаги успешно выполнены
                    context.SetVariable("RetryAttempt", attempt);
                    context.SetVariable("RetrySucceeded", true);
                    return;
                }
                catch (OperationCanceledException)
                {
                    throw; // Не перехватываем cancellation
                }
                catch (Exception ex)
                {
                    lastException = ex;

                    // Если это не последняя попытка, ждем перед повтором
                    if (attempt < _maxAttempts)
                    {
                        var delay = _useExponentialBackoff
                            ? TimeSpan.FromMilliseconds(_baseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1))
                            : _baseDelay;

                        await Task.Delay(delay, ct);
                    }
                }
            }

            // Все попытки исчерпаны
            context.SetVariable("RetrySucceeded", false);
            context.SetVariable("RetryLastError", lastException?.Message);

            throw new WorkflowStepException(
                $"Retry failed after {_maxAttempts} attempts. Last error: {lastException?.Message}",
                lastException);
        }

        /// <summary>
        /// Добавляет шаг для повторного выполнения
        /// </summary>
        public RetryStep AddStep(IWorkflowStep step)
        {
            Steps.Add(step ?? throw new ArgumentNullException(nameof(step)));
            return this;
        }
    }
}