// Input workflow step - type text
using System;
using System.Threading;
using System.Threading.Tasks;
using AutomationCore.Core.Abstractions;
using AutomationCore.Features.Workflows;

namespace AutomationCore.Features.Workflows.Steps.Input
{
    /// <summary>
    /// Шаг ввода текста
    /// </summary>
    public sealed class TypeTextStep : IWorkflowStep
    {
        private readonly string _text;

        public string Name => $"Type(\"{(_text.Length > 20 ? _text[..20] + "..." : _text)}\")";

        public TypeTextStep(string text)
        {
            _text = text ?? throw new ArgumentNullException(nameof(text));
        }

        public async ValueTask ExecuteAsync(IWorkflowContext context, CancellationToken ct = default)
        {
            var input = context.GetService<IInputSimulator>();
            var result = await input.Keyboard.TypeAsync(_text, null, ct);

            if (!result.Success)
            {
                throw new WorkflowStepException($"Failed to type text: {result.Error?.Message}");
            }
        }
    }
}