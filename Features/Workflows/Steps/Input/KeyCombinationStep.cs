// Input workflow step - key combination
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutomationCore.Core.Abstractions;
using AutomationCore.Core.Models;
using AutomationCore.Features.Workflows;

namespace AutomationCore.Features.Workflows.Steps.Input
{
    /// <summary>
    /// Шаг нажатия комбинации клавиш
    /// </summary>
    public sealed class KeyCombinationStep : IWorkflowStep
    {
        private readonly VirtualKey[] _keys;

        public string Name => $"PressKeys({string.Join("+", _keys.Select(k => k.ToString()))})";

        public KeyCombinationStep(params VirtualKey[] keys)
        {
            _keys = keys?.Length > 0 ? keys : throw new ArgumentException("At least one key must be specified", nameof(keys));
        }

        public async ValueTask ExecuteAsync(IWorkflowContext context, CancellationToken ct = default)
        {
            var input = context.GetService<IInputSimulator>();

            if (_keys.Length == 1)
            {
                // Одинарное нажатие клавиши
                var result = await input.Keyboard.PressKeyAsync(_keys[0], ct);
                if (!result.Success)
                {
                    throw new WorkflowStepException($"Failed to press key {_keys[0]}: {result.Error?.Message}");
                }
            }
            else
            {
                // Комбинация клавиш
                var result = await input.Keyboard.PressKeyCombinationAsync(_keys, ct);
                if (!result.Success)
                {
                    throw new WorkflowStepException($"Failed to press key combination {string.Join("+", _keys)}: {result.Error?.Message}");
                }
            }
        }
    }
}