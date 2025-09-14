// Workflow Steps - композитный файл, использующий новые специализированные компоненты
using AutomationCore.Features.Workflows.Steps.Basic;
using AutomationCore.Features.Workflows.Steps.Input;
using AutomationCore.Features.Workflows.Steps.Control;
using AutomationCore.Features.Workflows.Steps.Custom;
using AutomationCore.Features.Workflows.Steps.Window;

namespace AutomationCore.Features.Workflows
{
    /// <summary>
    /// Фабрика для создания различных типов workflow шагов
    /// </summary>
    public static class WorkflowSteps
    {
        // Basic Steps
        public static WaitForImageStep WaitForImage(string templateKey, System.TimeSpan timeout)
            => new(templateKey, timeout);

        public static DelayStep Delay(System.TimeSpan delay)
            => new(delay);

        // Input Steps
        public static ClickOnImageStep ClickOnImage(string templateKey)
            => new(templateKey);

        public static TypeTextStep TypeText(string text)
            => new(text);

        public static KeyCombinationStep PressKeys(params AutomationCore.Core.Models.VirtualKey[] keys)
            => new(keys);

        // Control Steps
        public static ConditionalStep If(System.Func<AutomationCore.Core.Abstractions.IWorkflowContext, System.Threading.Tasks.ValueTask<bool>> condition)
            => new(condition);

        public static RetryStep Retry(int maxAttempts, System.TimeSpan? baseDelay = null, bool useExponentialBackoff = true)
            => new(maxAttempts, baseDelay, useExponentialBackoff);

        public static ParallelStep Parallel(params IWorkflowStep[] steps)
            => new(steps);

        public static WhileStep While(System.Func<AutomationCore.Core.Abstractions.IWorkflowContext, System.Threading.Tasks.ValueTask<bool>> condition, int maxIterations = 1000)
            => new(condition, maxIterations);

        public static WhileStep While(System.Func<AutomationCore.Core.Abstractions.IWorkflowContext, bool> condition, int maxIterations = 1000)
            => new(condition, maxIterations);

        // Custom Steps
        public static CustomActionStep CustomAction(System.Func<AutomationCore.Core.Abstractions.IWorkflowContext, System.Threading.Tasks.ValueTask> action, string? name = null)
            => new(action, name);

        public static CustomActionStep CustomAction(System.Action<AutomationCore.Core.Abstractions.IWorkflowContext> action, string? name = null)
            => new(action, name);

        public static CustomActionStep<T> CustomAction<T>(System.Func<AutomationCore.Core.Abstractions.IWorkflowContext, System.Threading.Tasks.ValueTask<T>> action, string? name = null)
            => new(action, name);

        public static CustomActionStep<T> CustomAction<T>(System.Func<AutomationCore.Core.Abstractions.IWorkflowContext, T> action, string? name = null)
            => new(action, name);

        // Window Steps
        public static ClickOnImageInWindowStep ClickOnImageInWindow(string windowTitle, string templateKey)
            => new(windowTitle, templateKey);

        public static TypeInWindowStep TypeInWindow(string windowTitle, string text)
            => new(windowTitle, text);
    }
}

// Re-export типов для обратной совместимости
namespace AutomationCore.Features.Workflows.Steps
{
    using Basic;
    using Input;
    using Control;
    using Custom;
    using Window;

    // Основные шаги доступны через пространство имен Steps
}