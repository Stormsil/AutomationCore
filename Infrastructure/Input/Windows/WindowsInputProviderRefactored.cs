// Windows Input Provider (refactored from monolith)
using AutomationCore.Core.Abstractions;
using AutomationCore.Core.Models;

namespace AutomationCore.Infrastructure.Input.Windows
{
    /// <summary>
    /// Рефакторенная реализация Windows Input Provider с разделением на компоненты
    /// </summary>
    public sealed class WindowsInputProviderRefactored : IInputSimulator
    {
        public IMouseSimulator Mouse { get; }
        public IKeyboardSimulator Keyboard { get; }
        public ICombinedInputSimulator Combined { get; }

        public WindowsInputProviderRefactored()
        {
            Mouse = new WindowsMouseSimulator();
            Keyboard = new WindowsKeyboardSimulator();
            Combined = new WindowsCombinedInputSimulator(Mouse, Keyboard);
        }

        public void Dispose()
        {
            // Компоненты не требуют явного освобождения ресурсов в текущей реализации
        }
    }
}