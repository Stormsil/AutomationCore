using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace AutomationCore.Input
{
    /// <summary>
    /// Современная система ввода с имитацией человеческого поведения.
    /// Использует кривые Безье для движения мыши, случайные задержки,
    /// и другие техники для обхода систем защиты.
    /// </summary>
    public class HumanizedInput
    {
        private readonly Random _random = new Random();
        private readonly InputSettings _settings;

        public HumanizedInput(InputSettings settings = null)
        {
            _settings = settings ?? InputSettings.Default;
        }

        #region Мышь

        /// <summary>
        /// Перемещает мышь по человеческой траектории (кривая Безье)
        /// </summary>
        /// <param name="targetX">Целевая X координата</param>
        /// <param name="targetY">Целевая Y координата</param>
        /// <param name="duration">Длительность движения в миллисекундах</param>
        public async Task MoveMouseAsync(int targetX, int targetY, int duration = 0)
        {
            // Получаем текущую позицию
            GetCursorPos(out POINT currentPos);

            // Если duration не указан, вычисляем на основе расстояния
            if (duration == 0)
            {
                var distance = Math.Sqrt(Math.Pow(targetX - currentPos.X, 2) + Math.Pow(targetY - currentPos.Y, 2));
                duration = (int)(distance * _settings.MouseSpeedMultiplier);
                duration = Math.Max(duration, _settings.MinMouseMoveDuration);
                duration = Math.Min(duration, _settings.MaxMouseMoveDuration);
            }

            // Генерируем траекторию движения (кривая Безье)
            var trajectory = GenerateBezierTrajectory(
                new Point(currentPos.X, currentPos.Y),
                new Point(targetX, targetY),
                _settings.MouseCurveComplexity
            );

            // Двигаем мышь по траектории
            var startTime = DateTime.Now;
            var steps = trajectory.Count;

            for (int i = 0; i < steps; i++)
            {
                var point = trajectory[i];

                // Добавляем микро-дрожание (как у человека)
                if (_settings.EnableMicroMovements && i % 3 == 0)
                {
                    point.X += _random.Next(-1, 2);
                    point.Y += _random.Next(-1, 2);
                }

                SetCursorPos(point.X, point.Y);

                // Вычисляем задержку для плавности
                if (i < steps - 1)
                {
                    var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                    var progress = (double)i / steps;
                    var expectedTime = duration * progress;
                    var delay = (int)(expectedTime - elapsed);

                    if (delay > 0)
                    {
                        await Task.Delay(delay);
                    }
                }
            }

            // Финальная коррекция позиции (на случай дрожания)
            SetCursorPos(targetX, targetY);
        }

        /// <summary>
        /// Клик мышью с человеческими задержками
        /// </summary>
        public async Task ClickAsync(MouseButton button = MouseButton.Left)
        {
            // Случайная задержка перед нажатием (50-150ms)
            await Task.Delay(_random.Next(50, 150));

            // Определяем флаги для кнопки
            uint downFlag, upFlag;
            switch (button)
            {
                case MouseButton.Left:
                    downFlag = MOUSEEVENTF_LEFTDOWN;
                    upFlag = MOUSEEVENTF_LEFTUP;
                    break;
                case MouseButton.Right:
                    downFlag = MOUSEEVENTF_RIGHTDOWN;
                    upFlag = MOUSEEVENTF_RIGHTUP;
                    break;
                case MouseButton.Middle:
                    downFlag = MOUSEEVENTF_MIDDLEDOWN;
                    upFlag = MOUSEEVENTF_MIDDLEUP;
                    break;
                default:
                    throw new ArgumentException("Неподдерживаемая кнопка мыши");
            }

            // Нажимаем кнопку
            var inputs = new INPUT[1];
            inputs[0].type = INPUT_TYPE.MOUSE;
            inputs[0].U.mi.dwFlags = downFlag;
            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));

            // Человеческая задержка удержания (30-120ms)
            await Task.Delay(_random.Next(30, 120));

            // Отпускаем кнопку
            inputs[0].U.mi.dwFlags = upFlag;
            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));

            // Задержка после клика
            await Task.Delay(_random.Next(50, 100));
        }

        /// <summary>
        /// Двойной клик
        /// </summary>
        public async Task DoubleClickAsync(MouseButton button = MouseButton.Left)
        {
            await ClickAsync(button);
            await Task.Delay(_random.Next(100, 200)); // Задержка между кликами
            await ClickAsync(button);
        }

        /// <summary>
        /// Перетаскивание (drag & drop)
        /// </summary>
        public async Task DragAsync(int fromX, int fromY, int toX, int toY, int duration = 500)
        {
            // Перемещаемся к начальной точке
            await MoveMouseAsync(fromX, fromY, duration / 3);

            // Нажимаем кнопку мыши
            var inputs = new INPUT[1];
            inputs[0].type = INPUT_TYPE.MOUSE;
            inputs[0].U.mi.dwFlags = MOUSEEVENTF_LEFTDOWN;
            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));

            await Task.Delay(_random.Next(100, 200));

            // Перетаскиваем
            await MoveMouseAsync(toX, toY, duration);

            await Task.Delay(_random.Next(100, 200));

            // Отпускаем кнопку
            inputs[0].U.mi.dwFlags = MOUSEEVENTF_LEFTUP;
            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        /// <summary>
        /// Прокрутка колесика мыши
        /// </summary>
        public async Task ScrollAsync(int amount, ScrollDirection direction = ScrollDirection.Vertical)
        {
            var inputs = new INPUT[1];
            inputs[0].type = INPUT_TYPE.MOUSE;

            if (direction == ScrollDirection.Vertical)
            {
                inputs[0].U.mi.dwFlags = MOUSEEVENTF_WHEEL;
                inputs[0].U.mi.mouseData = (uint)(amount * 120); // 120 = одна "засечка" колеса
            }
            else
            {
                inputs[0].U.mi.dwFlags = MOUSEEVENTF_HWHEEL;
                inputs[0].U.mi.mouseData = (uint)(amount * 120);
            }

            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
            await Task.Delay(_random.Next(50, 100));
        }

        #endregion

        #region Клавиатура

        /// <summary>
        /// Печатает текст с человеческой скоростью и ошибками
        /// </summary>
        public async Task TypeTextAsync(string text, int baseDelay = 50)
        {
            foreach (char c in text)
            {
                // Иногда делаем опечатки (1% шанс)
                if (_settings.EnableTypos && _random.Next(100) < 1)
                {
                    // Печатаем неправильную букву
                    await TypeCharAsync(GetRandomNearbyChar(c));
                    await Task.Delay(_random.Next(200, 400));

                    // Стираем её
                    await KeyPressAsync(VirtualKey.Back);
                    await Task.Delay(_random.Next(100, 200));
                }

                // Печатаем правильный символ
                await TypeCharAsync(c);

                // Вариативная задержка между символами
                var delay = baseDelay + _random.Next(-20, 50);

                // Иногда делаем паузы (как будто думаем)
                if (_random.Next(20) == 0)
                {
                    delay += _random.Next(200, 500);
                }

                await Task.Delay(Math.Max(delay, 10));
            }
        }

        /// <summary>
        /// Печатает один символ
        /// </summary>
        private async Task TypeCharAsync(char c)
        {
            // Получаем виртуальный код клавиши
            short vkCode = VkKeyScan(c);
            var vk = (byte)(vkCode & 0xFF);
            var shift = (vkCode & 0x100) != 0;
            var ctrl = (vkCode & 0x200) != 0;
            var alt = (vkCode & 0x400) != 0;

            var inputs = new List<INPUT>();

            // Нажимаем модификаторы если нужно
            if (shift)
            {
                var input = new INPUT { type = INPUT_TYPE.KEYBOARD };
                input.U.ki.wVk = VK_SHIFT;
                inputs.Add(input);
            }

            // Нажимаем клавишу
            var keyDown = new INPUT { type = INPUT_TYPE.KEYBOARD };
            keyDown.U.ki.wVk = vk;
            inputs.Add(keyDown);

            // Отпускаем клавишу
            var keyUp = new INPUT { type = INPUT_TYPE.KEYBOARD };
            keyUp.U.ki.wVk = vk;
            keyUp.U.ki.dwFlags = KEYEVENTF_KEYUP;
            inputs.Add(keyUp);

            // Отпускаем модификаторы
            if (shift)
            {
                var input = new INPUT { type = INPUT_TYPE.KEYBOARD };
                input.U.ki.wVk = VK_SHIFT;
                input.U.ki.dwFlags = KEYEVENTF_KEYUP;
                inputs.Add(input);
            }

            SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf(typeof(INPUT)));
        }

        /// <summary>
        /// Нажатие клавиши
        /// </summary>
        public async Task KeyPressAsync(VirtualKey key)
        {
            var inputs = new INPUT[2];

            // Key down
            inputs[0].type = INPUT_TYPE.KEYBOARD;
            inputs[0].U.ki.wVk = (ushort)key;

            // Key up
            inputs[1].type = INPUT_TYPE.KEYBOARD;
            inputs[1].U.ki.wVk = (ushort)key;
            inputs[1].U.ki.dwFlags = KEYEVENTF_KEYUP;

            SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));

            await Task.Delay(_random.Next(50, 100));
        }

        /// <summary>
        /// Комбинация клавиш (например Ctrl+C)
        /// </summary>
        public async Task KeyCombinationAsync(params VirtualKey[] keys)
        {
            var inputs = new List<INPUT>();

            // Нажимаем все клавиши
            foreach (var key in keys)
            {
                var input = new INPUT { type = INPUT_TYPE.KEYBOARD };
                input.U.ki.wVk = (ushort)key;
                inputs.Add(input);
            }

            // Задержка
            await Task.Delay(_random.Next(50, 100));

            // Отпускаем все клавиши в обратном порядке
            for (int i = keys.Length - 1; i >= 0; i--)
            {
                var input = new INPUT { type = INPUT_TYPE.KEYBOARD };
                input.U.ki.wVk = (ushort)keys[i];
                input.U.ki.dwFlags = KEYEVENTF_KEYUP;
                inputs.Add(input);
            }

            SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf(typeof(INPUT)));

            await Task.Delay(_random.Next(50, 100));
        }

        #endregion

        #region Вспомогательные методы

        /// <summary>
        /// Генерирует траекторию движения по кривой Безье
        /// </summary>
        private List<Point> GenerateBezierTrajectory(Point start, Point end, int complexity)
        {
            var points = new List<Point>();
            var steps = 50; // Количество точек на траектории

            // Генерируем контрольные точки для кривой Безье
            var control1 = new Point(
                start.X + _random.Next(-complexity, complexity),
                start.Y + _random.Next(-complexity, complexity)
            );

            var control2 = new Point(
                end.X + _random.Next(-complexity, complexity),
                end.Y + _random.Next(-complexity, complexity)
            );

            // Вычисляем точки на кривой
            for (int i = 0; i <= steps; i++)
            {
                double t = (double)i / steps;
                double u = 1 - t;
                double tt = t * t;
                double uu = u * u;
                double uuu = uu * u;
                double ttt = tt * t;

                int x = (int)(uuu * start.X + 3 * uu * t * control1.X + 3 * u * tt * control2.X + ttt * end.X);
                int y = (int)(uuu * start.Y + 3 * uu * t * control1.Y + 3 * u * tt * control2.Y + ttt * end.Y);

                points.Add(new Point(x, y));
            }

            return points;
        }

        /// <summary>
        /// Получает случайную соседнюю клавишу (для опечаток)
        /// </summary>
        private char GetRandomNearbyChar(char c)
        {
            // Карта соседних клавиш на QWERTY клавиатуре
            var nearbyKeys = new Dictionary<char, string>
            {
                ['q'] = "wa",
                ['w'] = "qeas",
                ['e'] = "wsdr",
                ['r'] = "edft",
                ['t'] = "rfgy",
                ['y'] = "tghu",
                ['u'] = "yhji",
                ['i'] = "ujko",
                ['o'] = "iklp",
                ['p'] = "ol",
                ['a'] = "qwsz",
                ['s'] = "awedxz",
                ['d'] = "serfcx",
                ['f'] = "drtgvc",
                ['g'] = "ftyhbv",
                ['h'] = "gyujnb",
                ['j'] = "huikmn",
                ['k'] = "jiolm",
                ['l'] = "kop",
                ['z'] = "asx",
                ['x'] = "zsdc",
                ['c'] = "xdfv",
                ['v'] = "cfgb",
                ['b'] = "vghn",
                ['n'] = "bhjm",
                ['m'] = "njk"
            };

            var lower = char.ToLower(c);
            if (nearbyKeys.ContainsKey(lower))
            {
                var nearby = nearbyKeys[lower];
                var randomChar = nearby[_random.Next(nearby.Length)];
                return char.IsUpper(c) ? char.ToUpper(randomChar) : randomChar;
            }

            return c;
        }

        #endregion

        #region P/Invoke

        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        private static extern short VkKeyScan(char ch);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public INPUT_TYPE type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
            [FieldOffset(0)]
            public KEYBDINPUT ki;
            [FieldOffset(0)]
            public HARDWAREINPUT hi;
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
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        private enum INPUT_TYPE : uint
        {
            MOUSE = 0,
            KEYBOARD = 1,
            HARDWARE = 2
        }

        // Mouse event flags
        private const uint MOUSEEVENTF_MOVE = 0x0001;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
        private const uint MOUSEEVENTF_WHEEL = 0x0800;
        private const uint MOUSEEVENTF_HWHEEL = 0x1000;
        private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

        // Keyboard event flags
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_UNICODE = 0x0004;
        private const uint KEYEVENTF_SCANCODE = 0x0008;

        // Virtual keys
        private const ushort VK_SHIFT = 0x10;
        private const ushort VK_CONTROL = 0x11;
        private const ushort VK_MENU = 0x12; // Alt

        #endregion
    }

    /// <summary>
    /// Настройки для системы ввода
    /// </summary>
    public class InputSettings
    {
        /// <summary>
        /// Множитель скорости мыши (пикселей в миллисекунду)
        /// </summary>
        public double MouseSpeedMultiplier { get; set; } = 1.5;

        /// <summary>
        /// Минимальная длительность движения мыши
        /// </summary>
        public int MinMouseMoveDuration { get; set; } = 100;

        /// <summary>
        /// Максимальная длительность движения мыши
        /// </summary>
        public int MaxMouseMoveDuration { get; set; } = 2000;

        /// <summary>
        /// Сложность кривой Безье (больше = более изогнутая траектория)
        /// </summary>
        public int MouseCurveComplexity { get; set; } = 50;

        /// <summary>
        /// Включить микро-движения (дрожание мыши)
        /// </summary>
        public bool EnableMicroMovements { get; set; } = true;

        /// <summary>
        /// Включить случайные опечатки при печати
        /// </summary>
        public bool EnableTypos { get; set; } = false;

        /// <summary>
        /// Настройки по умолчанию
        /// </summary>
        public static InputSettings Default => new InputSettings();

        /// <summary>
        /// Настройки для максимальной скорости (без humanization)
        /// </summary>
        public static InputSettings Fast => new InputSettings
        {
            MouseSpeedMultiplier = 0.1,
            MinMouseMoveDuration = 0,
            MaxMouseMoveDuration = 100,
            MouseCurveComplexity = 0,
            EnableMicroMovements = false,
            EnableTypos = false
        };

        /// <summary>
        /// Настройки для максимальной человечности
        /// </summary>
        public static InputSettings VeryHuman => new InputSettings
        {
            MouseSpeedMultiplier = 2.5,
            MinMouseMoveDuration = 200,
            MaxMouseMoveDuration = 3000,
            MouseCurveComplexity = 100,
            EnableMicroMovements = true,
            EnableTypos = true
        };
    }

    /// <summary>
    /// Перечисления
    /// </summary>
    public enum MouseButton
    {
        Left,
        Right,
        Middle
    }

    public enum ScrollDirection
    {
        Vertical,
        Horizontal
    }

    public enum VirtualKey
    {
        // Буквы
        A = 0x41, B = 0x42, C = 0x43, D = 0x44, E = 0x45,
        F = 0x46, G = 0x47, H = 0x48, I = 0x49, J = 0x4A,
        K = 0x4B, L = 0x4C, M = 0x4D, N = 0x4E, O = 0x4F,
        P = 0x50, Q = 0x51, R = 0x52, S = 0x53, T = 0x54,
        U = 0x55, V = 0x56, W = 0x57, X = 0x58, Y = 0x59, Z = 0x5A,

        // Цифры
        D0 = 0x30, D1 = 0x31, D2 = 0x32, D3 = 0x33, D4 = 0x34,
        D5 = 0x35, D6 = 0x36, D7 = 0x37, D8 = 0x38, D9 = 0x39,

        // Функциональные клавиши
        Back = 0x08,
        Tab = 0x09,
        Return = 0x0D,
        Shift = 0x10,
        Control = 0x11,
        Alt = 0x12,
        Escape = 0x1B,
        Space = 0x20,
        End = 0x23,
        Home = 0x24,
        Left = 0x25,
        Up = 0x26,
        Right = 0x27,
        Down = 0x28,
        Delete = 0x2E,

        // F-клавиши
        F1 = 0x70, F2 = 0x71, F3 = 0x72, F4 = 0x73,
        F5 = 0x74, F6 = 0x75, F7 = 0x76, F8 = 0x77,
        F9 = 0x78, F10 = 0x79, F11 = 0x7A, F12 = 0x7B
    }
}