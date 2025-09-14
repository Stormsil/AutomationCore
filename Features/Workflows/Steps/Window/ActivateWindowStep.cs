// Window workflow step - activate window
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutomationCore.Core.Abstractions;
using AutomationCore.Core.Models;
using AutomationCore.Features.Workflows;

namespace AutomationCore.Features.Workflows.Steps.Window
{
    /// <summary>
    /// Шаг активации окна
    /// </summary>
    public sealed class ActivateWindowStep : IWorkflowStep
    {
        private readonly string _windowTitle;

        public string Name => $"ActivateWindow({_windowTitle})";

        public ActivateWindowStep(string windowTitle)
        {
            _windowTitle = windowTitle ?? throw new ArgumentNullException(nameof(windowTitle));
        }

        public async ValueTask ExecuteAsync(IWorkflowContext context, CancellationToken ct = default)
        {
            var windowManager = context.GetService<IWindowManager>();

            // Ищем окно по заголовку
            var criteria = WindowSearchCriteria.WithTitle(_windowTitle);
            var windows = await windowManager.FindWindowsAsync(criteria);

            if (!windows.Any())
            {
                throw new WorkflowStepException($"Window with title '{_windowTitle}' not found");
            }

            var window = windows.First();
            var success = await windowManager.ActivateWindowAsync(window.Handle);

            if (!success)
            {
                throw new WorkflowStepException($"Failed to activate window '{_windowTitle}'");
            }
        }
    }
}