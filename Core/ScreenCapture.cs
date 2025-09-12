using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace AutomationCore.Core
{
    public class ScreenCapture : IDisposable
    {
        private AutomationCore.Capture.WindowsGraphicsCapture _wgc;

        // --- НОВОЕ: нормализация строки (дэши, NBSP, регистр, пробелы) ---
        private static string Normalize(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            ReadOnlySpan<char> span = s.Normalize(NormalizationForm.FormKC);
            var sb = new StringBuilder(span.Length);

            foreach (var ch in span)
            {
                // все типографские дефисы -> обычный '-'
                if (ch is '\u2012' or '\u2013' or '\u2014' or '\u2015' or '\u2212' or '-')
                    sb.Append('-');
                // неразрывные пробелы -> обычный пробел
                else if (ch is '\u00A0' or '\u2007' or '\u202F')
                    sb.Append(' ');
                else
                    sb.Append(char.ToLowerInvariant(ch));
            }
            // схлопываем подряд идущие пробелы
            return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
        }

        /// <summary>Захватывает окно по части заголовка.</summary>
        public async Task<Bitmap> CaptureWindowAsync(string windowTitle)
        {
            if (string.IsNullOrWhiteSpace(windowTitle))
                throw new ArgumentException("windowTitle пуст.", nameof(windowTitle));

            var hwnd = FindWindowByTitle(windowTitle);
            if (hwnd == IntPtr.Zero)
                throw new InvalidOperationException($"Окно с заголовком, содержащим \"{windowTitle}\", не найдено.");

            _wgc = new AutomationCore.Capture.WindowsGraphicsCapture();
            await _wgc.InitializeAsync(hwnd);
            return await _wgc.CaptureFrameAsync();
        }

        /// <summary>Захватывает весь экран.</summary>
        public async Task<Bitmap> CaptureScreenAsync()
        {
            _wgc = new AutomationCore.Capture.WindowsGraphicsCapture();
            await _wgc.InitializeAsync(IntPtr.Zero);
            return await _wgc.CaptureScreenAsync();
        }

        /// <summary>Захватывает область экрана.</summary>
        public async Task<Bitmap> CaptureRegionAsync(int x, int y, int width, int height)
        {
            using var full = await CaptureScreenAsync();
            var region = new Rectangle(x, y, width, height);
            var cropped = new Bitmap(width, height);
            using (var g = Graphics.FromImage(cropped))
                g.DrawImage(full, 0, 0, region, GraphicsUnit.Pixel);
            return cropped;
        }

        /// <summary>Находит окно по подстроке в заголовке (без учёта регистра/типографики).</summary>
        private static IntPtr FindWindowByTitle(string title)
        {
            var query = Normalize(title);
            IntPtr found = IntPtr.Zero;

            EnumWindows((hwnd, _) =>
            {
                if (!IsWindowVisible(hwnd)) return true;

                int len = GetWindowTextLength(hwnd);
                if (len <= 0) return true;

                var sb = new StringBuilder(len + 1);
                GetWindowText(hwnd, sb, sb.Capacity);

                var normalized = Normalize(sb.ToString());
                if (normalized.Contains(query))
                {
                    found = hwnd;
                    return false; // стоп
                }
                return true;
            }, IntPtr.Zero);

            return found;
        }

        public void Dispose() => _wgc?.Dispose();

        // P/Invoke
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
        [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    }
}
