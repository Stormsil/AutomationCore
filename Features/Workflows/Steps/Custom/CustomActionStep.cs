// Custom workflow step - arbitrary action execution
using System;
using System.Threading;
using System.Threading.Tasks;
using AutomationCore.Core.Abstractions;

namespace AutomationCore.Features.Workflows.Steps.Custom
{
    /// <summary>
    /// Шаг выполнения произвольного пользовательского действия
    /// </summary>
    public sealed class CustomActionStep : IWorkflowStep
    {
        private readonly Func<IWorkflowContext, ValueTask> _action;
        private readonly string _name;

        public string Name => _name;

        public CustomActionStep(Func<IWorkflowContext, ValueTask> action, string? name = null)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
            _name = name ?? "CustomAction";
        }

        public CustomActionStep(Action<IWorkflowContext> action, string? name = null)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            _action = context =>
            {
                action(context);
                return ValueTask.CompletedTask;
            };
            _name = name ?? "CustomAction";
        }

        public async ValueTask ExecuteAsync(IWorkflowContext context, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            await _action(context);
        }
    }

    /// <summary>
    /// Шаг выполнения произвольного пользовательского действия с возвращаемым значением
    /// </summary>
    public sealed class CustomActionStep<T> : IWorkflowStep
    {
        private readonly Func<IWorkflowContext, ValueTask<T>> _action;
        private readonly string _name;

        public string Name => _name;

        public CustomActionStep(Func<IWorkflowContext, ValueTask<T>> action, string? name = null)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
            _name = name ?? $"CustomAction<{typeof(T).Name}>";
        }

        public CustomActionStep(Func<IWorkflowContext, T> action, string? name = null)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            _action = context => ValueTask.FromResult(action(context));
            _name = name ?? $"CustomAction<{typeof(T).Name}>";
        }

        public async ValueTask ExecuteAsync(IWorkflowContext context, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var result = await _action(context);
            context.LastStepResult = result;
        }
    }
}