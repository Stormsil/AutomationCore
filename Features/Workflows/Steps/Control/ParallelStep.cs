// Control workflow step - parallel execution
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutomationCore.Core.Abstractions;

namespace AutomationCore.Features.Workflows.Steps.Control
{
    /// <summary>
    /// Шаг параллельного выполнения нескольких действий
    /// </summary>
    public sealed class ParallelStep : IWorkflowStep
    {
        public List<IWorkflowStep> Steps { get; } = new();

        public string Name => $"Parallel({Steps.Count} steps)";

        public ParallelStep(params IWorkflowStep[] steps)
        {
            if (steps != null && steps.Length > 0)
            {
                Steps.AddRange(steps);
            }
        }

        public ParallelStep(IEnumerable<IWorkflowStep> steps)
        {
            if (steps != null)
            {
                Steps.AddRange(steps);
            }
        }

        public async ValueTask ExecuteAsync(IWorkflowContext context, CancellationToken ct = default)
        {
            if (Steps.Count == 0)
            {
                context.LogWarning("ParallelStep has no steps to execute");
                return;
            }

            // Создаем задачи для каждого шага
            var tasks = Steps.Select(step => ExecuteStepAsync(step, context, ct)).ToArray();

            try
            {
                // Ждем завершения всех задач
                await Task.WhenAll(tasks);

                // Сохраняем результаты всех параллельных шагов
                var results = new object?[tasks.Length];
                for (int i = 0; i < tasks.Length; i++)
                {
                    results[i] = context.LastStepResult; // Каждый шаг может установить свой результат
                }

                context.SetVariable("ParallelStepResults", results);
                context.LogInfo($"Successfully completed {Steps.Count} parallel steps");
            }
            catch (Exception ex)
            {
                context.LogError($"Parallel step execution failed", ex);
                throw;
            }
        }

        /// <summary>
        /// Добавляет шаг для параллельного выполнения
        /// </summary>
        public ParallelStep Add(IWorkflowStep step)
        {
            Steps.Add(step ?? throw new ArgumentNullException(nameof(step)));
            return this;
        }

        /// <summary>
        /// Добавляет несколько шагов для параллельного выполнения
        /// </summary>
        public ParallelStep AddRange(params IWorkflowStep[] steps)
        {
            if (steps != null && steps.Length > 0)
            {
                Steps.AddRange(steps);
            }
            return this;
        }

        private static async Task ExecuteStepAsync(IWorkflowStep step, IWorkflowContext context, CancellationToken ct)
        {
            try
            {
                await step.ExecuteAsync(context, ct);
            }
            catch (Exception ex)
            {
                context.LogError($"Step '{step.Name}' failed in parallel execution", ex);
                throw;
            }
        }
    }
}