// AutomationCore/Workflows/Contracts.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AutomationCore.Core
{
    /// <summary>Контекст выполнения workflow.</summary>
    public class WorkflowContext
    {
        public IAutomationEngine Engine { get; }
        public CancellationToken Cancellation { get; }

        public WorkflowContext(IAutomationEngine engine, CancellationToken cancellation)
        {
            Engine = engine ?? throw new ArgumentNullException(nameof(engine));
            Cancellation = cancellation;
        }
    }

    /// <summary>Результат выполнения workflow.</summary>
    public class WorkflowResult
    {
        public string Name { get; set; } = "";
        public bool Success { get; set; } = true;
        public Exception Error { get; set; }
        public string FailedStep { get; set; }
        public List<string> CompletedSteps { get; } = new();
    }

    /// <summary>Шаг workflow.</summary>
    public interface IWorkflowStep
    {
        string Name { get; }
        Task ExecuteAsync(WorkflowContext context);
    }

    /// <summary>Fluent API для сборки сценариев.</summary>
    public interface IWorkflowBuilder
    {
        IWorkflowBuilder WaitForImage(string templateKey, TimeSpan timeout);
        IWorkflowBuilder ClickOn(string templateKey);
        IWorkflowBuilder Type(string text);
        IWorkflowBuilder Delay(TimeSpan delay);

        IWorkflowBuilder If(Func<Task<bool>> condition, Action<IWorkflowBuilder> then);
        IWorkflowBuilder Retry(int times, Action<IWorkflowBuilder> steps);

        Task<WorkflowResult> ExecuteAsync(CancellationToken ct = default);
    }
}
