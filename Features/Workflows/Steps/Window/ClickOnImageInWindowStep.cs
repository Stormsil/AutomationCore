// Window workflow step - click on image in specific window
using System;
using System.Threading;
using System.Threading.Tasks;
using AutomationCore.Core.Abstractions;
using AutomationCore.Features.WindowAutomation;
using AutomationCore.Features.Workflows;

namespace AutomationCore.Features.Workflows.Steps.Window
{
    /// <summary>
    /// Шаг клика по изображению в конкретном окне
    /// </summary>
    public sealed class ClickOnImageInWindowStep : IWorkflowStep
    {
        private readonly string _windowTitle;
        private readonly string _templateKey;

        public string Name => $"ClickOnImageInWindow({_windowTitle}, {_templateKey})";

        public ClickOnImageInWindowStep(string windowTitle, string templateKey)
        {
            _windowTitle = windowTitle ?? throw new ArgumentNullException(nameof(windowTitle));
            _templateKey = templateKey ?? throw new ArgumentNullException(nameof(templateKey));
        }

        public async ValueTask ExecuteAsync(IWorkflowContext context, CancellationToken ct = default)
        {
            var windowAutomator = context.GetService<WindowAutomator>();
            var result = await windowAutomator.ClickOnImageInWindowAsync(_windowTitle, _templateKey, cancellationToken: ct);

            if (!result.Success)
            {
                throw new WorkflowStepException($"Failed to click on image '{_templateKey}' in window '{_windowTitle}': {result.Error?.Message}");
            }

            // Сохраняем результат операции с окном
            context.SetVariable("LastWindowOperationResult", result);
        }
    }
}