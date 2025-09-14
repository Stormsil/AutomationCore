// Windows Input Provider - композитный файл, использующий новые специализированные компоненты
using AutomationCore.Core.Abstractions;
using AutomationCore.Infrastructure.Input.Windows;

namespace AutomationCore.Infrastructure.Input
{
    /// <summary>
    /// Главный провайдер ввода для Windows, использующий специализированные компоненты
    /// </summary>
    public sealed class WindowsInputProvider : IInputSimulator
    {
        public IMouseSimulator Mouse { get; }
        public IKeyboardSimulator Keyboard { get; }
        public ICombinedInputSimulator Combined { get; }

        public WindowsInputProvider()
        {
            Mouse = new WindowsMouseSimulator();
            Keyboard = new WindowsKeyboardSimulator();
            Combined = new WindowsCombinedInputSimulator(Mouse, Keyboard);
        }

        public void Dispose()
        {
            Mouse?.Dispose();
            Keyboard?.Dispose();
            Combined?.Dispose();
        }
    }
}