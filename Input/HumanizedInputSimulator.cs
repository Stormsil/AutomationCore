// AutomationCore/Input/HumanizedInputSimulator.cs
using System.Drawing;
using System.Threading.Tasks;
using AutomationCore.Core.Abstractions;
using AutomationCore.Core.Configuration;

namespace AutomationCore.Input
{
    /// <summary>Адаптер HumanizedInput к IInputSimulator.</summary>
    public sealed class HumanizedInputSimulator : IInputSimulator
    {
        private readonly HumanizedInput _impl;
        public IMouseSimulator Mouse { get; }
        public IKeyboardSimulator Keyboard { get; }

        public HumanizedInputSimulator(InputOptions options = null)
        {
            var settings = options?.ToSettings() ?? InputSettings.Default;
            _impl = new HumanizedInput(settings);
            Mouse = new MouseAdapter(_impl);
            Keyboard = new KeyboardAdapter(_impl, settings);
        }

        private sealed class MouseAdapter : IMouseSimulator
        {
            private readonly HumanizedInput _h;
            public MouseAdapter(HumanizedInput h) => _h = h;
            public Point CurrentPosition => System.Windows.Forms.Cursor.Position;

            public async Task MoveToAsync(Point target, MouseMoveOptions options = null)
                => await _h.MoveMouseAsync(target.X, target.Y, options?.DurationMs ?? 0);

            public async Task ClickAsync(MouseButton button = MouseButton.Left)
                => await _h.ClickAsync(button);

            public async Task DragAsync(Point from, Point to, DragOptions options = null)
                => await _h.DragAsync(from.X, from.Y, to.X, to.Y, options?.DurationMs ?? 500);

            public async Task ScrollAsync(int amount, ScrollDirection direction = ScrollDirection.Vertical)
                => await _h.ScrollAsync(amount, direction);
        }

        private sealed class KeyboardAdapter : IKeyboardSimulator
        {
            private readonly HumanizedInput _h;
            private readonly InputSettings _settings;
            public KeyboardAdapter(HumanizedInput h, InputSettings settings) { _h = h; _settings = settings; }

            public async Task TypeAsync(string text, TypingOptions options = null)
                => await _h.TypeTextAsync(text, (options?.BaseDelayMs ?? 50));

            public async Task PressKeyAsync(VirtualKey key)
                => await _h.KeyPressAsync(key);

            public async Task KeyCombinationAsync(params VirtualKey[] keys)
                => await _h.KeyCombinationAsync(keys);
        }
    }

}
