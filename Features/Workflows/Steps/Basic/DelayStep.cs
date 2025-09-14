// Basic workflow step - delay/wait
using System;
using System.Threading;
using System.Threading.Tasks;
using AutomationCore.Core.Abstractions;

namespace AutomationCore.Features.Workflows.Steps.Basic
{
    /// <summary>
    /// Шаг задержки выполнения
    /// </summary>
    public sealed class DelayStep : IWorkflowStep
    {
        private readonly TimeSpan _delay;

        public string Name => $"Delay({_delay.TotalMilliseconds:F0}ms)";

        public DelayStep(TimeSpan delay)
        {
            _delay = delay > TimeSpan.Zero ? delay : throw new ArgumentOutOfRangeException(nameof(delay), "Delay must be positive");
        }

        public async ValueTask ExecuteAsync(IWorkflowContext context, CancellationToken ct = default)
        {
            await Task.Delay(_delay, ct);
        }
    }
}