using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace AutomationCore.UI
{
    /// <summary>Простой оверлей: одна рамка + один текст.</summary>
    public sealed class SimpleOverlay : IDisposable
    {
        private Thread _uiThread;
        private OverlayForm _form;
        private readonly ManualResetEventSlim _ready = new(false);
        private bool _disposed;

        public SimpleOverlay()
        {
            _uiThread = new Thread(UIThread)
            {
                Name = "SimpleOverlay",
                IsBackground = true
            };
            _uiThread.SetApartmentState(ApartmentState.STA);
            _uiThread.Start();

            // ждём создания формы и handle
            _ready.Wait();
        }

        private void UIThread()
        {
            try { Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            _form = new OverlayForm();
            _form.CreateControl();         // гарантируем Handle
            _ready.Set();
            Application.Run(_form);
        }

        /// <summary>Показать оверлей на всех мониторах.</summary>
        public void Show()
        {
            if (_form == null) return;
            _form.BeginInvoke(new Action(() =>
            {
                _form.FitToVirtualScreen();
                _form.Show();
                _form.BringToTopMost();
            }));
        }

        /// <summary>Скрыть оверлей.</summary>
        public void Hide()
        {
            if (_form == null) return;
            _form.BeginInvoke(new Action(() => _form.Hide()));
        }

        /// <summary>Нарисовать рамку.</summary>
        public void DrawBox(Rectangle rect, Color color, int thickness = 3)
        {
            if (_form == null) return;
            _form.BeginInvoke(new Action(() => _form.SetBox(rect, color, thickness)));
        }

        /// <summary>Нарисовать текст.</summary>
        public void DrawText(Point at, string text, Color color, float fontSize = 12f)
        {
            if (_form == null) return;
            _form.BeginInvoke(new Action(() => _form.SetText(at, text, color, fontSize)));
        }

        /// <summary>Очистить всё.</summary>
        public void Clear()
        {
            if (_form == null) return;
            _form.BeginInvoke(new Action(() =>
            {
                _form.ClearBox();
                _form.ClearText();
            }));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                if (_form != null) _form.BeginInvoke(new Action(() => _form.Close()));
                _uiThread?.Join(1000);
            }
            catch { /* ignore */ }
            finally
            {
                _ready.Dispose();
                _form = null;
                _uiThread = null;
            }
        }

        /// <summary>Внутренняя форма оверлея.</summary>
        private sealed class OverlayForm : Form
        {
            // box
            private Rectangle _boxRect;
            private Color _boxColor = Color.Red;
            private int _boxThickness = 3;
            private bool _hasBox;

            // text
            private string _text = null;
            private Point _textPos;
            private Color _textColor = Color.Khaki;
            private float _textFontSize = 12f;

            public OverlayForm()
            {
                FormBorderStyle = FormBorderStyle.None;
                ShowInTaskbar = false;
                TopMost = true;

                BackColor = Color.Magenta;        // «прозрачный» цвет
                TransparencyKey = BackColor;

                SetStyle(ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.UserPaint |
                         ControlStyles.OptimizedDoubleBuffer, true);
            }

            protected override CreateParams CreateParams
            {
                get
                {
                    const int WS_EX_TOOLWINDOW = 0x00000080;
                    const int WS_EX_NOACTIVATE = 0x08000000;
                    const int WS_EX_TRANSPARENT = 0x00000020;

                    var cp = base.CreateParams;
                    cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT;
                    return cp;
                }
            }

            public void FitToVirtualScreen()
            {
                var vs = SystemInformation.VirtualScreen;
                Bounds = new Rectangle(vs.Left, vs.Top, vs.Width, vs.Height);
            }

            public void SetBox(Rectangle rect, Color color, int thickness)
            {
                _boxRect = rect;
                _boxColor = color;
                _boxThickness = Math.Max(1, thickness);
                _hasBox = rect.Width > 0 && rect.Height > 0;
                Invalidate();
            }

            public void ClearBox()
            {
                _hasBox = false;
                Invalidate();
            }

            public void SetText(Point pos, string text, Color color, float fontSize)
            {
                _textPos = pos;
                _text = text ?? string.Empty;
                _textColor = color;
                _textFontSize = Math.Max(6f, fontSize);
                Invalidate();
            }

            public void ClearText()
            {
                _text = null;
                Invalidate();
            }

            protected override void OnPaintBackground(PaintEventArgs e)
            {
                // полностью прозрачный фон
                e.Graphics.Clear(TransparencyKey);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // рамка
                if (_hasBox)
                {
                    using var pen = new Pen(_boxColor, _boxThickness) { LineJoin = LineJoin.Round };
                    g.DrawRectangle(pen, _boxRect);
                }

                // текст
                if (!string.IsNullOrEmpty(_text))
                {
                    using var font = new Font("Segoe UI", _textFontSize, FontStyle.Regular, GraphicsUnit.Point);
                    // лёгкая тень для читаемости
                    using var shadow = new SolidBrush(Color.FromArgb(160, 0, 0, 0));
                    using var brush = new SolidBrush(_textColor);
                    var p = _textPos;
                    g.DrawString(_text, font, shadow, p.X + 1, p.Y + 1);
                    g.DrawString(_text, font, brush, p.X, p.Y);
                }
            }

            public void BringToTopMost()
            {
                const uint SWP_NOMOVE = 0x0002, SWP_NOSIZE = 0x0001, SWP_NOACTIVATE = 0x0010, SWP_SHOWWINDOW = 0x0040;
                SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            }

            [DllImport("user32.dll")]
            private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
                int X, int Y, int cx, int cy, uint uFlags);
        }
    }
}
