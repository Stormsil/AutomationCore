// AutomationCore/Workflows/WorkflowBuilder.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutomationCore.Core;

namespace AutomationCore.Workflows
{
    /// <summary>
    /// Fluent API для создания и выполнения workflow-сценариев.
    /// Поддерживает базовые шаги, условия и повторные попытки.
    /// </summary>
    public class WorkflowBuilder : IWorkflowBuilder
    {
        private readonly string _name;
        private readonly IAutomationEngine _engine;
        private readonly List<IWorkflowStep> _steps;

        public WorkflowBuilder(string name, IAutomationEngine engine)
        {
            _name = string.IsNullOrWhiteSpace(name) ? "Workflow" : name;
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _steps = new List<IWorkflowStep>(16);
        }

        public IWorkflowBuilder WaitForImage(string templateKey, TimeSpan timeout)
        {
            _steps.Add(new WaitForImageStep(templateKey, timeout));
            return this;
        }

        public IWorkflowBuilder ClickOn(string templateKey)
        {
            _steps.Add(new ClickImageStep(templateKey));
            return this;
        }

        public IWorkflowBuilder Type(string text)
        {
            _steps.Add(new TypeTextStep(text));
            return this;
        }

        public IWorkflowBuilder Delay(TimeSpan delay)
        {
            _steps.Add(new DelayStep(delay));
            return this;
        }

        public IWorkflowBuilder If(Func<Task<bool>> condition, Action<IWorkflowBuilder> then)
        {
            if (condition is null) throw new ArgumentNullException(nameof(condition));

            var cond = new ConditionalStep(condition);
            // наполняем ветку Then через вложенный билдер, который пишет в cond.ThenSteps
            var nested = new NestedBuilder(_engine, cond.ThenSteps);
            then?.Invoke(nested);

            _steps.Add(cond);
            return this;
        }

        public IWorkflowBuilder Retry(int times, Action<IWorkflowBuilder> steps)
        {
            var retry = new RetryStep(times);
            var nested = new NestedBuilder(_engine, retry.Steps);
            steps?.Invoke(nested);

            _steps.Add(retry);
            return this;
        }

        public async Task<WorkflowResult> ExecuteAsync(CancellationToken ct = default)
        {
            var result = new WorkflowResult { Name = _name, Success = true };
            var context = new WorkflowContext(_engine, ct);

            foreach (var step in _steps)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    await step.ExecuteAsync(context).ConfigureAwait(false);
                    result.CompletedSteps.Add(step.Name);
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Error = ex;
                    result.FailedStep = step.Name;
                    break;
                }
            }

            return result;
        }

        /// <summary>
        /// Внутренний билдер, который пишет не в корневой список, а в предоставленный список шагов.
        /// Используется для If/Retry.
        /// </summary>
        private sealed class NestedBuilder : IWorkflowBuilder
        {
            private readonly IAutomationEngine _engine;
            private readonly List<IWorkflowStep> _target;

            public NestedBuilder(IAutomationEngine engine, List<IWorkflowStep> target)
            {
                _engine = engine ?? throw new ArgumentNullException(nameof(engine));
                _target = target ?? throw new ArgumentNullException(nameof(target));
            }

            public IWorkflowBuilder WaitForImage(string templateKey, TimeSpan timeout)
            {
                _target.Add(new WaitForImageStep(templateKey, timeout));
                return this;
            }

            public IWorkflowBuilder ClickOn(string templateKey)
            {
                _target.Add(new ClickImageStep(templateKey));
                return this;
            }

            public IWorkflowBuilder Type(string text)
            {
                _target.Add(new TypeTextStep(text));
                return this;
            }

            public IWorkflowBuilder Delay(TimeSpan delay)
            {
                _target.Add(new DelayStep(delay));
                return this;
            }

            public IWorkflowBuilder If(Func<Task<bool>> condition, Action<IWorkflowBuilder> then)
            {
                var cond = new ConditionalStep(condition ?? throw new ArgumentNullException(nameof(condition)));
                var nested = new NestedBuilder(_engine, cond.ThenSteps);
                then?.Invoke(nested);
                _target.Add(cond);
                return this;
            }

            public IWorkflowBuilder Retry(int times, Action<IWorkflowBuilder> steps)
            {
                var retry = new RetryStep(times);
                var nested = new NestedBuilder(_engine, retry.Steps);
                steps?.Invoke(nested);
                _target.Add(retry);
                return this;
            }

            public Task<WorkflowResult> ExecuteAsync(CancellationToken ct = default)
            {
                // NestedBuilder не выполняет сам — он только конструирует список шагов.
                // Для единообразия можно собрать временный корневой билдер и выполнить его,
                // но текущие вызовы не требуют выполнения из вложенного контекста.
                throw new NotSupportedException("Nested builder cannot execute independently.");
            }
        }
    }
}
