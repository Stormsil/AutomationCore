// Control workflow step - conditional execution
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutomationCore.Core.Abstractions;

namespace AutomationCore.Features.Workflows.Steps.Control
{
    /// <summary>
    /// Условный шаг выполнения
    /// </summary>
    public sealed class ConditionalStep : IWorkflowStep
    {
        private readonly Func<IWorkflowContext, ValueTask<bool>> _condition;
        public List<IWorkflowStep> ThenSteps { get; } = new();
        public List<IWorkflowStep> ElseSteps { get; } = new();

        public string Name => "If(condition)";

        public ConditionalStep(Func<IWorkflowContext, ValueTask<bool>> condition)
        {
            _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        }

        public async ValueTask ExecuteAsync(IWorkflowContext context, CancellationToken ct = default)
        {
            var conditionResult = await _condition(context);
            context.SetVariable("LastConditionResult", conditionResult);

            var stepsToExecute = conditionResult ? ThenSteps : ElseSteps;

            foreach (var step in stepsToExecute)
            {
                ct.ThrowIfCancellationRequested();
                await step.ExecuteAsync(context, ct);
            }
        }

        /// <summary>
        /// Добавляет шаг для выполнения в случае истинного условия
        /// </summary>
        public ConditionalStep Then(IWorkflowStep step)
        {
            ThenSteps.Add(step ?? throw new ArgumentNullException(nameof(step)));
            return this;
        }

        /// <summary>
        /// Добавляет шаг для выполнения в случае ложного условия
        /// </summary>
        public ConditionalStep Else(IWorkflowStep step)
        {
            ElseSteps.Add(step ?? throw new ArgumentNullException(nameof(step)));
            return this;
        }
    }
}