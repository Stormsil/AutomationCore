// Basic workflow step - waiting for image to appear
using System;
using System.Threading;
using System.Threading.Tasks;
using AutomationCore.Core.Abstractions;
using AutomationCore.Features.ImageSearch;
using AutomationCore.Features.Workflows;

namespace AutomationCore.Features.Workflows.Steps.Basic
{
    /// <summary>
    /// Шаг ожидания появления изображения
    /// </summary>
    public sealed class WaitForImageStep : IWorkflowStep
    {
        private readonly string _templateKey;
        private readonly TimeSpan _timeout;

        public string Name => $"WaitForImage({_templateKey}, {_timeout.TotalSeconds:F1}s)";

        public WaitForImageStep(string templateKey, TimeSpan timeout)
        {
            _templateKey = templateKey ?? throw new ArgumentNullException(nameof(templateKey));
            _timeout = timeout;
        }

        public async ValueTask ExecuteAsync(IWorkflowContext context, CancellationToken ct = default)
        {
            var imageSearch = context.GetService<ImageSearchEngine>();
            var result = await imageSearch.WaitForAsync(_templateKey, _timeout, cancellationToken: ct);

            if (!result.Success)
            {
                throw new WorkflowStepException($"Image '{_templateKey}' did not appear within {_timeout}");
            }

            // Сохраняем результат в контекст
            if (result.Location != null)
            {
                context.SetVariable($"LastFoundImage_{_templateKey}", result.Location);
            }
        }
    }
}