// Exception for workflow step execution failures
using System;

namespace AutomationCore.Features.Workflows
{
    /// <summary>
    /// Исключение при выполнении шага workflow
    /// </summary>
    public sealed class WorkflowStepException : Exception
    {
        public string? StepName { get; }
        public int? StepIndex { get; }

        public WorkflowStepException(string message) : base(message)
        {
        }

        public WorkflowStepException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public WorkflowStepException(string message, string stepName) : base(message)
        {
            StepName = stepName;
        }

        public WorkflowStepException(string message, string stepName, int stepIndex) : base(message)
        {
            StepName = stepName;
            StepIndex = stepIndex;
        }

        public WorkflowStepException(string message, Exception innerException, string stepName) : base(message, innerException)
        {
            StepName = stepName;
        }

        public override string ToString()
        {
            var result = base.ToString();

            if (!string.IsNullOrEmpty(StepName))
            {
                result = $"Step '{StepName}': {result}";
            }

            if (StepIndex.HasValue)
            {
                result = $"Step {StepIndex}: {result}";
            }

            return result;
        }
    }
}