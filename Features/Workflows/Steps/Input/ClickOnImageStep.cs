// Input workflow step - click on image
using System;
using System.Threading;
using System.Threading.Tasks;
using AutomationCore.Core.Abstractions;
using AutomationCore.Core.Models;
using AutomationCore.Features.ImageSearch;
using AutomationCore.Features.Workflows;

namespace AutomationCore.Features.Workflows.Steps.Input
{
    /// <summary>
    /// Шаг клика по изображению
    /// </summary>
    public sealed class ClickOnImageStep : IWorkflowStep
    {
        private readonly string _templateKey;

        public string Name => $"ClickOnImage({_templateKey})";

        public ClickOnImageStep(string templateKey)
        {
            _templateKey = templateKey ?? throw new ArgumentNullException(nameof(templateKey));
        }

        public async ValueTask ExecuteAsync(IWorkflowContext context, CancellationToken ct = default)
        {
            var imageSearch = context.GetService<ImageSearchEngine>();
            var input = context.GetService<IInputSimulator>();

            // Сначала ищем изображение
            var searchResult = await imageSearch.FindAsync(_templateKey, cancellationToken: ct);

            if (!searchResult.Success || searchResult.Location == null)
            {
                throw new WorkflowStepException($"Image '{_templateKey}' not found on screen");
            }

            // Кликаем по центру найденного изображения
            var clickResult = await input.Mouse.ClickAsync(MouseButton.Left, ct);

            if (!clickResult.Success)
            {
                throw new WorkflowStepException($"Failed to click on image '{_templateKey}': {clickResult.Error?.Message}");
            }

            // Сохраняем координаты клика
            context.SetVariable("LastClickPosition", searchResult.Location.Center);
        }
    }
}