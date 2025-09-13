using AutomationCore.Core;

namespace AutomationCore.Workflows
{
    /// <summary>
    /// Fluent API для создания workflow
    /// </summary>
    public class WorkflowBuilder : IWorkflowBuilder
    {
        private readonly string _name;
        private readonly IAutomationEngine _engine;
        private readonly List<IWorkflowStep> _steps;

        public WorkflowBuilder(string name, IAutomationEngine engine)
        {
            _name = name;
            _engine = engine;
            _steps = new();
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
            var conditional = new ConditionalStep(condition);
            var subBuilder = new WorkflowBuilder($"{_name}_if", _engine);
            then(subBuilder);
            conditional.ThenSteps = subBuilder._steps;
            _steps.Add(conditional);
            return this;
        }

        public IWorkflowBuilder Retry(int times, Action<IWorkflowBuilder> steps)
        {
            var retry = new RetryStep(times);
            var subBuilder = new WorkflowBuilder($"{_name}_retry", _engine);
            steps(subBuilder);
            retry.Steps = subBuilder._steps;
            _steps.Add(retry);
            return this;
        }

        public async Task<WorkflowResult> ExecuteAsync(CancellationToken ct = default)
        {
            var result = new WorkflowResult { Name = _name };
            var context = new WorkflowContext(_engine, ct);

            foreach (var step in _steps)
            {
                try
                {
                    await step.ExecuteAsync(context);
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
    }
}