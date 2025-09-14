// Control workflow step - while loop execution
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutomationCore.Core.Abstractions;
using AutomationCore.Features.Workflows;

namespace AutomationCore.Features.Workflows.Steps.Control
{
    /// <summary>
    /// Шаг циклического выполнения действий по условию (цикл while)
    /// </summary>
    public sealed class WhileStep : IWorkflowStep
    {
        private readonly Func<IWorkflowContext, ValueTask<bool>> _condition;
        private readonly int _maxIterations;
        public List<IWorkflowStep> Steps { get; } = new();

        public string Name => $"While(condition, {Steps.Count} steps, max {_maxIterations})";

        public WhileStep(Func<IWorkflowContext, ValueTask<bool>> condition, int maxIterations = 1000)
        {
            _condition = condition ?? throw new ArgumentNullException(nameof(condition));
            _maxIterations = maxIterations > 0 ? maxIterations : throw new ArgumentOutOfRangeException(nameof(maxIterations), "Max iterations must be positive");
        }

        public WhileStep(Func<IWorkflowContext, bool> condition, int maxIterations = 1000)
        {
            if (condition == null) throw new ArgumentNullException(nameof(condition));

            _condition = context => ValueTask.FromResult(condition(context));
            _maxIterations = maxIterations > 0 ? maxIterations : throw new ArgumentOutOfRangeException(nameof(maxIterations), "Max iterations must be positive");
        }

        public async ValueTask ExecuteAsync(IWorkflowContext context, CancellationToken ct = default)
        {
            int iterations = 0;

            try
            {
                while (await _condition(context))
                {
                    ct.ThrowIfCancellationRequested();

                    if (++iterations > _maxIterations)
                    {
                        throw new WorkflowStepException($"While loop exceeded maximum iterations limit ({_maxIterations})");
                    }

                    context.LogInfo($"While loop iteration {iterations}");

                    // Выполняем все шаги в цикле
                    foreach (var step in Steps)
                    {
                        ct.ThrowIfCancellationRequested();
                        await step.ExecuteAsync(context, ct);
                    }
                }

                // Сохраняем количество выполненных итераций
                context.SetVariable("WhileLoopIterations", iterations);
                context.LogInfo($"While loop completed after {iterations} iterations");
            }
            catch (Exception ex) when (!(ex is WorkflowStepException))
            {
                context.LogError($"While loop failed at iteration {iterations}", ex);
                throw new WorkflowStepException($"While loop failed at iteration {iterations}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Добавляет шаг для выполнения в цикле
        /// </summary>
        public WhileStep Do(IWorkflowStep step)
        {
            Steps.Add(step ?? throw new ArgumentNullException(nameof(step)));
            return this;
        }

        /// <summary>
        /// Добавляет несколько шагов для выполнения в цикле
        /// </summary>
        public WhileStep Do(params IWorkflowStep[] steps)
        {
            if (steps != null && steps.Length > 0)
            {
                Steps.AddRange(steps);
            }
            return this;
        }

        /// <summary>
        /// Добавляет шаги для выполнения в цикле
        /// </summary>
        public WhileStep Do(IEnumerable<IWorkflowStep> steps)
        {
            if (steps != null)
            {
                Steps.AddRange(steps);
            }
            return this;
        }
    }
}