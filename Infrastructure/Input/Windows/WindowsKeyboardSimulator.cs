// Windows Keyboard Simulator (extracted from monolith)
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AutomationCore.Core.Abstractions;
using AutomationCore.Core.Models;

namespace AutomationCore.Infrastructure.Input.Windows
{
    /// <summary>
    /// Реализация симуляции клавиатуры для Windows через Win32 API
    /// </summary>
    internal sealed class WindowsKeyboardSimulator : IKeyboardSimulator
    {
        public event EventHandler<InputEvent>? KeyPressed;
        public event EventHandler<InputEvent>? TextTyped;

        public async ValueTask<InputResult> TypeAsync(string text, TypingOptions? options = null, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(text))
                return InputResult.Successful(TimeSpan.Zero, "Type (empty)");

            var startTime = DateTime.UtcNow;
            try
            {
                var delay = options?.BaseDelay ?? TimeSpan.FromMilliseconds(50);

                foreach (char c in text)
                {
                    ct.ThrowIfCancellationRequested();

                    if (char.IsControl(c))
                    {
                        // Обработка специальных символов
                        await HandleControlCharacter(c, ct);
                    }
                    else
                    {
                        // Обычные символы
                        await TypeCharacterAsync(c, ct);
                    }

                    if (delay > TimeSpan.Zero)
                        await Task.Delay(delay, ct);
                }

                return InputResult.Successful(DateTime.UtcNow - startTime, $"Type \"{text}\"");
            }
            catch (Exception ex)
            {
                return InputResult.Failed(ex, $"Type \"{text}\"") with { Duration = DateTime.UtcNow - startTime };
            }
        }

        public async ValueTask<InputResult> PressKeyAsync(VirtualKey key, CancellationToken ct = default)
        {
            var startTime = DateTime.UtcNow;
            try
            {
                var vkCode = (byte)key;
                var holdDuration = TimeSpan.FromMilliseconds(50);

                // Нажимаем клавишу
                keybd_event(vkCode, 0, 0, UIntPtr.Zero);

                if (holdDuration > TimeSpan.Zero)
                    await Task.Delay(holdDuration, ct);

                // Отпускаем клавишу
                keybd_event(vkCode, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

                KeyPressed?.Invoke(this, new InputEvent
                {
                    Type = InputEventType.KeyPress,
                    Key = key,
                    Timestamp = DateTime.UtcNow
                });

                return InputResult.Successful(DateTime.UtcNow - startTime, $"PressKey {key}");
            }
            catch (Exception ex)
            {
                return InputResult.Failed(ex, $"PressKey {key}") with { Duration = DateTime.UtcNow - startTime };
            }
        }

        public async ValueTask<InputResult> KeyCombinationAsync(VirtualKey[] keys, CancellationToken ct = default)
        {
            if (keys == null || keys.Length == 0)
                return InputResult.Successful(TimeSpan.Zero, "PressKeyCombination (empty)");

            var startTime = DateTime.UtcNow;
            try
            {
                var holdDuration = TimeSpan.FromMilliseconds(100);

                // Нажимаем все клавиши
                foreach (var key in keys)
                {
                    keybd_event((byte)key, 0, 0, UIntPtr.Zero);
                    await Task.Delay(10, ct); // Небольшая задержка между нажатиями
                }

                if (holdDuration > TimeSpan.Zero)
                    await Task.Delay(holdDuration, ct);

                // Отпускаем все клавиши в обратном порядке
                foreach (var key in keys.Reverse())
                {
                    keybd_event((byte)key, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                    await Task.Delay(10, ct);
                }

                var keyCombination = string.Join("+", keys.Select(k => k.ToString()));
                return InputResult.Successful(DateTime.UtcNow - startTime, $"PressKeyCombination {keyCombination}");
            }
            catch (Exception ex)
            {
                var keyCombination = string.Join("+", keys.Select(k => k.ToString()));
                return InputResult.Failed(ex, $"PressKeyCombination {keyCombination}") with { Duration = DateTime.UtcNow - startTime };
            }
        }

        public async ValueTask<InputResult> KeyDownAsync(VirtualKey key, CancellationToken ct = default)
        {
            var startTime = DateTime.UtcNow;
            try
            {
                var vkCode = (byte)key;
                keybd_event(vkCode, 0, 0, UIntPtr.Zero);
                return InputResult.Successful(DateTime.UtcNow - startTime, $"KeyDown {key}");
            }
            catch (Exception ex)
            {
                return InputResult.Failed(ex, $"KeyDown {key}") with { Duration = DateTime.UtcNow - startTime };
            }
        }

        public async ValueTask<InputResult> KeyUpAsync(VirtualKey key, CancellationToken ct = default)
        {
            var startTime = DateTime.UtcNow;
            try
            {
                var vkCode = (byte)key;
                keybd_event(vkCode, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                return InputResult.Successful(DateTime.UtcNow - startTime, $"KeyUp {key}");
            }
            catch (Exception ex)
            {
                return InputResult.Failed(ex, $"KeyUp {key}") with { Duration = DateTime.UtcNow - startTime };
            }
        }

        public async ValueTask<InputResult> CopyAsync(CancellationToken ct = default)
        {
            return await KeyCombinationAsync(new[] { VirtualKey.LeftControl, VirtualKey.C }, ct);
        }

        public async ValueTask<InputResult> PasteAsync(CancellationToken ct = default)
        {
            return await KeyCombinationAsync(new[] { VirtualKey.LeftControl, VirtualKey.V }, ct);
        }

        public async ValueTask<InputResult> CutAsync(CancellationToken ct = default)
        {
            return await KeyCombinationAsync(new[] { VirtualKey.LeftControl, VirtualKey.X }, ct);
        }

        public async ValueTask<InputResult> UndoAsync(CancellationToken ct = default)
        {
            return await KeyCombinationAsync(new[] { VirtualKey.LeftControl, VirtualKey.Z }, ct);
        }

        public async ValueTask<InputResult> RedoAsync(CancellationToken ct = default)
        {
            return await KeyCombinationAsync(new[] { VirtualKey.LeftControl, VirtualKey.Y }, ct);
        }

        private async Task TypeCharacterAsync(char character, CancellationToken ct)
        {
            // Конвертируем символ в виртуальные клавиши
            var vkCode = VkKeyScan(character);

            if (vkCode == -1)
            {
                // Символ не может быть введен напрямую, используем Unicode
                await SendUnicodeCharacterAsync(character, ct);
                return;
            }

            var key = (byte)(vkCode & 0xFF);
            var shift = (vkCode & 0x100) != 0;

            if (shift)
            {
                keybd_event(VK_SHIFT, 0, 0, UIntPtr.Zero);
                await Task.Delay(10, ct);
            }

            keybd_event(key, 0, 0, UIntPtr.Zero);
            await Task.Delay(10, ct);
            keybd_event(key, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

            if (shift)
            {
                await Task.Delay(10, ct);
                keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
        }

        private async Task SendUnicodeCharacterAsync(char character, CancellationToken ct)
        {
            // Отправляем Unicode символ напрямую
            var input = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = character,
                        dwFlags = KEYEVENTF_UNICODE,
                        dwExtraInfo = UIntPtr.Zero
                    }
                }
            };

            SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
            await Task.Delay(10, ct);
        }

        private async Task HandleControlCharacter(char c, CancellationToken ct)
        {
            switch (c)
            {
                case '\r':
                case '\n':
                    await PressKeyAsync(VirtualKey.Return, ct);
                    break;
                case '\t':
                    await PressKeyAsync(VirtualKey.Tab, ct);
                    break;
                case '\b':
                    await PressKeyAsync(VirtualKey.Backspace, ct);
                    break;
                default:
                    // Игнорируем другие управляющие символы
                    break;
            }
        }

        // Win32 API
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern short VkKeyScan(char ch);

        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
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
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_UNICODE = 0x0004;
        private const byte VK_SHIFT = 0x10;
    }
}