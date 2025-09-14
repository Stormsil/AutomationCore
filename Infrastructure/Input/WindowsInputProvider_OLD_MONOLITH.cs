// Infrastructure/Input/WindowsInputProvider.cs
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AutomationCore.Core.Abstractions;
using AutomationCore.Core.Models;

namespace AutomationCore.Infrastructure.Input
{
    /// <summary>
    /// Реализация Windows Input Provider через Win32 API
    /// </summary>
    public sealed class WindowsInputProvider : IInputSimulator
    {
        public IMouseSimulator Mouse { get; }
        public IKeyboardSimulator Keyboard { get; }
        public ICombinedInputSimulator Combined { get; }

        public WindowsInputProvider()
        {
            Mouse = new SimpleMouseSimulator();
            Keyboard = new SimpleKeyboardSimulator();
            Combined = new SimpleCombinedInputSimulator(Mouse, Keyboard);
        }

        private sealed class SimpleMouseSimulator : IMouseSimulator
        {
            public Point CurrentPosition => GetCurrentCursorPos();

            public event EventHandler<InputEvent>? MouseMoved;
            public event EventHandler<InputEvent>? MouseClicked;

            public async ValueTask<InputResult> MoveToAsync(Point position, MouseMoveOptions? options = null, CancellationToken ct = default)
            {
                var startTime = DateTime.UtcNow;
                try
                {
                    SetCursorPos(position.X, position.Y);
                    await Task.Delay(options?.Duration ?? TimeSpan.FromMilliseconds(50), ct);

                    MouseMoved?.Invoke(this, new InputEvent
                    {
                        Type = InputEventType.MouseMove,
                        Position = position
                    });

                    return InputResult.Successful(DateTime.UtcNow - startTime, "MouseMove");
                }
                catch (Exception ex)
                {
                    return InputResult.Failed(ex, "MouseMove");
                }
            }

            public async ValueTask<InputResult> ClickAsync(MouseButton button = MouseButton.Left, CancellationToken ct = default)
            {
                var startTime = DateTime.UtcNow;
                try
                {
                    SendMouseEvent(GetMouseDownFlag(button));
                    await Task.Delay(50, ct);
                    SendMouseEvent(GetMouseUpFlag(button));

                    MouseClicked?.Invoke(this, new InputEvent
                    {
                        Type = InputEventType.MouseClick,
                        Position = CurrentPosition,
                        MouseButton = button
                    });

                    return InputResult.Successful(DateTime.UtcNow - startTime, "MouseClick");
                }
                catch (Exception ex)
                {
                    return InputResult.Failed(ex, "MouseClick");
                }
            }

            public async ValueTask<InputResult> DoubleClickAsync(MouseButton button = MouseButton.Left, CancellationToken ct = default)
            {
                var startTime = DateTime.UtcNow;
                try
                {
                    await ClickAsync(button, ct);
                    await Task.Delay(50, ct);
                    await ClickAsync(button, ct);
                    return InputResult.Successful(DateTime.UtcNow - startTime, "MouseDoubleClick");
                }
                catch (Exception ex)
                {
                    return InputResult.Failed(ex, "MouseDoubleClick");
                }
            }

            public async ValueTask<InputResult> MouseDownAsync(MouseButton button = MouseButton.Left, CancellationToken ct = default)
            {
                var startTime = DateTime.UtcNow;
                try
                {
                    SendMouseEvent(GetMouseDownFlag(button));
                    return InputResult.Successful(DateTime.UtcNow - startTime, "MouseDown");
                }
                catch (Exception ex)
                {
                    return InputResult.Failed(ex, "MouseDown");
                }
            }

            public async ValueTask<InputResult> MouseUpAsync(MouseButton button = MouseButton.Left, CancellationToken ct = default)
            {
                var startTime = DateTime.UtcNow;
                try
                {
                    SendMouseEvent(GetMouseUpFlag(button));
                    return InputResult.Successful(DateTime.UtcNow - startTime, "MouseUp");
                }
                catch (Exception ex)
                {
                    return InputResult.Failed(ex, "MouseUp");
                }
            }

            public async ValueTask<InputResult> DragAsync(Point from, Point to, DragOptions? options = null, CancellationToken ct = default)
            {
                var startTime = DateTime.UtcNow;
                try
                {
                    await MoveToAsync(from, ct: ct);
                    SendMouseEvent(GetMouseDownFlag(MouseButton.Left));
                    await Task.Delay(100, ct);
                    await MoveToAsync(to, ct: ct);
                    await Task.Delay(50, ct);
                    SendMouseEvent(GetMouseUpFlag(MouseButton.Left));
                    return InputResult.Successful(DateTime.UtcNow - startTime, "MouseDrag");
                }
                catch (Exception ex)
                {
                    return InputResult.Failed(ex, "MouseDrag");
                }
            }

            public async ValueTask<InputResult> ScrollAsync(int delta, ScrollDirection direction = ScrollDirection.Vertical, CancellationToken ct = default)
            {
                var startTime = DateTime.UtcNow;
                try
                {
                    uint flags = direction == ScrollDirection.Vertical ? 0x0800 : 0x01000;
                    SendMouseEvent(flags, delta * 120);
                    return InputResult.Successful(DateTime.UtcNow - startTime, "MouseScroll");
                }
                catch (Exception ex)
                {
                    return InputResult.Failed(ex, "MouseScroll");
                }
            }

            private static Point GetCurrentCursorPos()
            {
                GetCursorPos(out var point);
                return new Point(point.X, point.Y);
            }

            private static uint GetMouseDownFlag(MouseButton button) => button switch
            {
                MouseButton.Left => 0x0002,
                MouseButton.Right => 0x0008,
                MouseButton.Middle => 0x0020,
                _ => 0x0002
            };

            private static uint GetMouseUpFlag(MouseButton button) => button switch
            {
                MouseButton.Left => 0x0004,
                MouseButton.Right => 0x0010,
                MouseButton.Middle => 0x0040,
                _ => 0x0004
            };

            private static void SendMouseEvent(uint flags, int data = 0)
            {
                var input = new INPUT
                {
                    type = 0,
                    U = new InputUnion
                    {
                        mi = new MOUSEINPUT
                        {
                            dx = 0,
                            dy = 0,
                            mouseData = data,
                            dwFlags = flags,
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                };

                SendInput(1, ref input, Marshal.SizeOf<INPUT>());
            }
        }

        private sealed class SimpleKeyboardSimulator : IKeyboardSimulator
        {
            public event EventHandler<InputEvent>? KeyPressed;
            public event EventHandler<InputEvent>? TextTyped;

            public async ValueTask<InputResult> TypeAsync(string text, TypingOptions? options = null, CancellationToken ct = default)
            {
                var startTime = DateTime.UtcNow;
                options ??= TypingOptions.Default;

                try
                {
                    foreach (char c in text)
                    {
                        var scan = VkKeyScan(c);
                        if (scan != -1)
                        {
                            var vk = (VirtualKey)(scan & 0xFF);
                            await PressKeyAsync(vk, ct);
                            await Task.Delay(options.BaseDelay, ct);
                        }
                    }

                    TextTyped?.Invoke(this, new InputEvent
                    {
                        Type = InputEventType.TextInput,
                        Text = text
                    });

                    return InputResult.Successful(DateTime.UtcNow - startTime, "TypeText");
                }
                catch (Exception ex)
                {
                    return InputResult.Failed(ex, "TypeText");
                }
            }

            public async ValueTask<InputResult> PressKeyAsync(VirtualKey key, CancellationToken ct = default)
            {
                var startTime = DateTime.UtcNow;
                try
                {
                    await KeyDownAsync(key, ct);
                    await Task.Delay(50, ct);
                    await KeyUpAsync(key, ct);
                    return InputResult.Successful(DateTime.UtcNow - startTime, "PressKey");
                }
                catch (Exception ex)
                {
                    return InputResult.Failed(ex, "PressKey");
                }
            }

            public async ValueTask<InputResult> KeyDownAsync(VirtualKey key, CancellationToken ct = default)
            {
                var startTime = DateTime.UtcNow;
                try
                {
                    SendKeyEvent((ushort)key, false);
                    return InputResult.Successful(DateTime.UtcNow - startTime, "KeyDown");
                }
                catch (Exception ex)
                {
                    return InputResult.Failed(ex, "KeyDown");
                }
            }

            public async ValueTask<InputResult> KeyUpAsync(VirtualKey key, CancellationToken ct = default)
            {
                var startTime = DateTime.UtcNow;
                try
                {
                    SendKeyEvent((ushort)key, true);
                    return InputResult.Successful(DateTime.UtcNow - startTime, "KeyUp");
                }
                catch (Exception ex)
                {
                    return InputResult.Failed(ex, "KeyUp");
                }
            }

            public async ValueTask<InputResult> KeyCombinationAsync(VirtualKey[] keys, CancellationToken ct = default)
            {
                var startTime = DateTime.UtcNow;
                try
                {
                    // Нажимаем все клавиши
                    foreach (var key in keys)
                        await KeyDownAsync(key, ct);

                    await Task.Delay(50, ct);

                    // Отпускаем в обратном порядке
                    for (int i = keys.Length - 1; i >= 0; i--)
                        await KeyUpAsync(keys[i], ct);

                    return InputResult.Successful(DateTime.UtcNow - startTime, "KeyCombination");
                }
                catch (Exception ex)
                {
                    return InputResult.Failed(ex, "KeyCombination");
                }
            }

            public async ValueTask<InputResult> CopyAsync(CancellationToken ct = default)
            {
                return await KeyCombinationAsync(new[] { VirtualKey.Control, VirtualKey.C }, ct);
            }

            public async ValueTask<InputResult> PasteAsync(CancellationToken ct = default)
            {
                return await KeyCombinationAsync(new[] { VirtualKey.Control, VirtualKey.V }, ct);
            }

            public async ValueTask<InputResult> CutAsync(CancellationToken ct = default)
            {
                return await KeyCombinationAsync(new[] { VirtualKey.Control, VirtualKey.X }, ct);
            }

            public async ValueTask<InputResult> UndoAsync(CancellationToken ct = default)
            {
                return await KeyCombinationAsync(new[] { VirtualKey.Control, VirtualKey.Z }, ct);
            }

            public async ValueTask<InputResult> RedoAsync(CancellationToken ct = default)
            {
                return await KeyCombinationAsync(new[] { VirtualKey.Control, VirtualKey.Y }, ct);
            }

            private static void SendKeyEvent(ushort keyCode, bool keyUp)
            {
                var input = new INPUT
                {
                    type = 1,
                    U = new InputUnion
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = keyCode,
                            wScan = 0,
                            dwFlags = keyUp ? 0x0002u : 0u,
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                };

                SendInput(1, ref input, Marshal.SizeOf<INPUT>());
            }
        }

        private sealed class SimpleCombinedInputSimulator : ICombinedInputSimulator
        {
            private readonly IMouseSimulator _mouse;
            private readonly IKeyboardSimulator _keyboard;

            public SimpleCombinedInputSimulator(IMouseSimulator mouse, IKeyboardSimulator keyboard)
            {
                _mouse = mouse;
                _keyboard = keyboard;
            }

            public async ValueTask<InputResult> ClickAndTypeAsync(Point location, string text, TypingOptions? typingOptions = null, CancellationToken ct = default)
            {
                var startTime = DateTime.UtcNow;
                try
                {
                    await _mouse.MoveToAsync(location, ct: ct);
                    await _mouse.ClickAsync(ct: ct);
                    await Task.Delay(100, ct);
                    await _keyboard.TypeAsync(text, typingOptions, ct);
                    return InputResult.Successful(DateTime.UtcNow - startTime, "ClickAndType");
                }
                catch (Exception ex)
                {
                    return InputResult.Failed(ex, "ClickAndType");
                }
            }

            public async ValueTask<InputResult> DragWithKeysAsync(Point from, Point to, VirtualKey[] keys, DragOptions? options = null, CancellationToken ct = default)
            {
                var startTime = DateTime.UtcNow;
                try
                {
                    // Нажимаем клавиши
                    foreach (var key in keys)
                        await _keyboard.KeyDownAsync(key, ct);

                    // Перетаскиваем
                    await _mouse.DragAsync(from, to, options, ct);

                    // Отпускаем клавиши
                    for (int i = keys.Length - 1; i >= 0; i--)
                        await _keyboard.KeyUpAsync(keys[i], ct);

                    return InputResult.Successful(DateTime.UtcNow - startTime, "DragWithKeys");
                }
                catch (Exception ex)
                {
                    return InputResult.Failed(ex, "DragWithKeys");
                }
            }

            public async ValueTask<InputResult> SelectAllAsync(CancellationToken ct = default)
            {
                return await _keyboard.KeyCombinationAsync(new[] { VirtualKey.Control, VirtualKey.A }, ct);
            }

            public async ValueTask<InputResult> ReplaceSelectedTextAsync(string newText, TypingOptions? options = null, CancellationToken ct = default)
            {
                var startTime = DateTime.UtcNow;
                try
                {
                    await _keyboard.TypeAsync(newText, options, ct);
                    return InputResult.Successful(DateTime.UtcNow - startTime, "ReplaceSelectedText");
                }
                catch (Exception ex)
                {
                    return InputResult.Failed(ex, "ReplaceSelectedText");
                }
            }

            public async ValueTask<InputResult> RightClickAndWaitAsync(Point location, TimeSpan timeout, CancellationToken ct = default)
            {
                var startTime = DateTime.UtcNow;
                try
                {
                    await _mouse.MoveToAsync(location, ct: ct);
                    await _mouse.ClickAsync(MouseButton.Right, ct);
                    await Task.Delay(timeout, ct);
                    return InputResult.Successful(DateTime.UtcNow - startTime, "RightClickAndWait");
                }
                catch (Exception ex)
                {
                    return InputResult.Failed(ex, "RightClickAndWait");
                }
            }
        }

        // Win32 API structures and imports
        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public int mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, ref INPUT pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern short VkKeyScan(char ch);
    }
}