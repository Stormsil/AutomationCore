// Window workflow step - type text in specific window
using System;
using System.Threading;
using System.Threading.Tasks;
using AutomationCore.Core.Abstractions;
using AutomationCore.Features.WindowAutomation;
using AutomationCore.Features.Workflows;

namespace AutomationCore.Features.Workflows.Steps.Window
{
    /// <summary>
    /// Шаг ввода текста в конкретном окне
    /// </summary>
    public sealed class TypeInWindowStep : IWorkflowStep
    {
        private readonly string _windowTitle;
        private readonly string _text;

        public string Name => $"TypeInWindow({_windowTitle}, '{_text.Substring(0, Math.Min(_text.Length, 20))}{(_text.Length > 20 ? "..." : "")}')";

        public TypeInWindowStep(string windowTitle, string text)
        {
            _windowTitle = windowTitle ?? throw new ArgumentNullException(nameof(windowTitle));
            _text = text ?? throw new ArgumentNullException(nameof(text));
        }

        public async ValueTask ExecuteAsync(IWorkflowContext context, CancellationToken ct = default)
        {
            var windowAutomator = context.GetService<WindowAutomator>();
            var result = await windowAutomator.TypeInWindowAsync(_windowTitle, _text, cancellationToken: ct);

            if (!result.Success)
            {
                throw new WorkflowStepException($"Failed to type text in window '{_windowTitle}': {result.Error?.Message}");
            }

            // Сохраняем результат операции с окном
            context.SetVariable("LastWindowOperationResult", result);
        }
    }
}