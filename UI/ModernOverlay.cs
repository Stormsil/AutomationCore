using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using WinFormsTimer = System.Windows.Forms.Timer;

namespace AutomationCore.UI
{
    /// <summary>
    /// Современное окно-оверлей с поддержкой анимаций и визуальных эффектов
    /// </summary>
    public sealed class ModernOverlay : Form
    {
        private readonly List<OverlayElement> _elements = new();
        private readonly object _lock = new();
        private readonly WinFormsTimer _animationTimer;
        private DateTime _lastFrameTime = DateTime.Now;

        // Цветовые схемы
        public static class ColorSchemes
        {
            public static readonly Color Success = Color.FromArgb(46, 204, 113);
            public static readonly Color Warning = Color.FromArgb(241, 196, 15);
            public static readonly Color Error = Color.FromArgb(231, 76, 60);
            public static readonly Color Info = Color.FromArgb(52, 152, 219);
            public static readonly Color Accent = Color.FromArgb(155, 89, 182);
            public static readonly Color Dark = Color.FromArgb(44, 62, 80);
        }

        public ModernOverlay()
        {
            InitializeWindow();

            // Таймер для анимаций (60 FPS)
            _animationTimer = new WinFormsTimer { Interval = 16 };
            _animationTimer.Tick += OnAnimationTick;
            _animationTimer.Start();
        }

        private void InitializeWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;

            // Прозрачность через TransparencyKey
            BackColor = Color.Magenta;
            TransparencyKey = BackColor;

            // Оптимизация отрисовки
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                const int WS_EX_TOOLWINDOW = 0x00000080;
                const int WS_EX_NOACTIVATE = 0x08000000;
                const int WS_EX_TRANSPARENT = 0x00000020;
                const int WS_EX_LAYERED = 0x00080000;

                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT | WS_EX_LAYERED;
                return cp;
            }
        }

        /// <summary>
        /// Подогнать окно под все мониторы
        /// </summary>
        public void FitToVirtualScreen()
        {
            var vs = SystemInformation.VirtualScreen;
            Bounds = new Rectangle(vs.Left, vs.Top, vs.Width, vs.Height);
        }

        /// <summary>
        /// Очистить все элементы
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _elements.Clear();
            }
            Invalidate();
        }

        /// <summary>
        /// Добавить рамку с анимацией появления
        /// </summary>
        public void AddBox(Rectangle rect, Color color, BoxStyle style = BoxStyle.Modern)
        {
            lock (_lock)
            {
                _elements.Add(new BoxElement
                {
                    Bounds = rect,
                    Color = color,
                    Style = style,
                    CreatedAt = DateTime.Now,
                    AnimationProgress = 0f
                });
            }
            Invalidate();
        }

        /// <summary>
        /// Добавить текст с тенью
        /// </summary>
        public void AddText(Point position, string text, Color color, TextStyle style = TextStyle.Modern)
        {
            lock (_lock)
            {
                _elements.Add(new TextElement
                {
                    Position = position,
                    Text = text,
                    Color = color,
                    Style = style,
                    CreatedAt = DateTime.Now,
                    AnimationProgress = 0f
                });
            }
            Invalidate();
        }

        /// <summary>
        /// Добавить индикатор прогресса
        /// </summary>
        public void AddProgressBar(Rectangle rect, float progress, Color color)
        {
            lock (_lock)
            {
                _elements.Add(new ProgressElement
                {
                    Bounds = rect,
                    Progress = Math.Max(0, Math.Min(1, progress)),
                    Color = color,
                    CreatedAt = DateTime.Now,
                    AnimationProgress = 0f
                });
            }
            Invalidate();
        }

        /// <summary>
        /// Добавить пульсирующую точку
        /// </summary>
        public void AddPulse(Point center, int radius, Color color)
        {
            lock (_lock)
            {
                _elements.Add(new PulseElement
                {
                    Center = center,
                    Radius = radius,
                    Color = color,
                    CreatedAt = DateTime.Now,
                    AnimationProgress = 0f
                });
            }
            Invalidate();
        }

        /// <summary>
        /// Добавить стрелку
        /// </summary>
        public void AddArrow(Point from, Point to, Color color, ArrowStyle style = ArrowStyle.Modern)
        {
            lock (_lock)
            {
                _elements.Add(new ArrowElement
                {
                    From = from,
                    To = to,
                    Color = color,
                    Style = style,
                    CreatedAt = DateTime.Now,
                    AnimationProgress = 0f
                });
            }
            Invalidate();
        }

        /// <summary>
        /// Добавить всплывающую подсказку
        /// </summary>
        public void AddTooltip(Point anchor, string text, Color bgColor, TooltipPosition position = TooltipPosition.Top)
        {
            lock (_lock)
            {
                _elements.Add(new TooltipElement
                {
                    Anchor = anchor,
                    Text = text,
                    BackgroundColor = bgColor,
                    Position = position,
                    CreatedAt = DateTime.Now,
                    AnimationProgress = 0f
                });
            }
            Invalidate();
        }

        private void OnAnimationTick(object sender, EventArgs e)
        {
            var now = DateTime.Now;
            var deltaTime = (float)(now - _lastFrameTime).TotalSeconds;
            _lastFrameTime = now;

            bool needsRedraw = false;

            lock (_lock)
            {
                // Обновляем анимации
                foreach (var element in _elements)
                {
                    if (element.AnimationProgress < 1f)
                    {
                        element.AnimationProgress = Math.Min(1f, element.AnimationProgress + deltaTime * 3f);
                        needsRedraw = true;
                    }

                    // Специальная анимация для пульсирующих элементов
                    if (element is PulseElement pulse)
                    {
                        pulse.PulsePhase = (float)((now - pulse.CreatedAt).TotalSeconds * 2 % (Math.PI * 2));
                        needsRedraw = true;
                    }
                }

                // Удаляем старые элементы (опционально)
                if (_elements.Count > 100)
                {
                    _elements.RemoveAll(e => (now - e.CreatedAt).TotalSeconds > 30);
                    needsRedraw = true;
                }
            }

            if (needsRedraw)
                Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(TransparencyKey);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.CompositingQuality = CompositingQuality.HighQuality;

            List<OverlayElement> elementsCopy;
            lock (_lock)
            {
                elementsCopy = new List<OverlayElement>(_elements);
            }

            foreach (var element in elementsCopy.OrderBy(e => e.ZOrder))
            {
                DrawElement(g, element);
            }
        }

        private void DrawElement(Graphics g, OverlayElement element)
        {
            var alpha = (int)(element.AnimationProgress * 255);

            switch (element)
            {
                case BoxElement box:
                    DrawBox(g, box, alpha);
                    break;

                case TextElement text:
                    DrawText(g, text, alpha);
                    break;

                case ProgressElement progress:
                    DrawProgress(g, progress, alpha);
                    break;

                case PulseElement pulse:
                    DrawPulse(g, pulse, alpha);
                    break;

                case ArrowElement arrow:
                    DrawArrow(g, arrow, alpha);
                    break;

                case TooltipElement tooltip:
                    DrawTooltip(g, tooltip, alpha);
                    break;
            }
        }

        private void DrawBox(Graphics g, BoxElement box, int alpha)
        {
            var rect = box.Bounds;

            // Анимация масштабирования
            if (box.AnimationProgress < 1f)
            {
                var scale = 0.8f + 0.2f * box.AnimationProgress;
                var center = new PointF(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);
                rect = new Rectangle(
                    (int)(center.X - rect.Width * scale / 2),
                    (int)(center.Y - rect.Height * scale / 2),
                    (int)(rect.Width * scale),
                    (int)(rect.Height * scale)
                );
            }

            switch (box.Style)
            {
                case BoxStyle.Modern:
                    DrawModernBox(g, rect, box.Color, alpha);
                    break;

                case BoxStyle.Glow:
                    DrawGlowBox(g, rect, box.Color, alpha);
                    break;

                case BoxStyle.Dashed:
                    DrawDashedBox(g, rect, box.Color, alpha);
                    break;

                case BoxStyle.Gradient:
                    DrawGradientBox(g, rect, box.Color, alpha);
                    break;
            }
        }

        private void DrawModernBox(Graphics g, Rectangle rect, Color color, int alpha)
        {
            // Внешнее свечение
            using (var glowPath = CreateRoundedRectPath(rect, 8))
            using (var glowBrush = new PathGradientBrush(glowPath))
            {
                glowBrush.CenterColor = Color.FromArgb(alpha / 4, color);
                glowBrush.SurroundColors = new[] { Color.Transparent };

                var glowRect = Rectangle.Inflate(rect, 20, 20);
                g.FillRectangle(glowBrush, glowRect);
            }

            // Основная рамка
            using (var path = CreateRoundedRectPath(rect, 8))
            using (var pen = new Pen(Color.FromArgb(alpha, color), 3))
            {
                pen.LineJoin = LineJoin.Round;
                g.DrawPath(pen, path);
            }

            // Уголки
            DrawCorners(g, rect, Color.FromArgb(alpha, color));
        }

        private void DrawGlowBox(Graphics g, Rectangle rect, Color color, int alpha)
        {
            // Многослойное свечение
            for (int i = 5; i > 0; i--)
            {
                var inflatedRect = Rectangle.Inflate(rect, i * 3, i * 3);
                using (var path = CreateRoundedRectPath(inflatedRect, 8 + i))
                using (var pen = new Pen(Color.FromArgb(alpha / (i + 1), color), 1))
                {
                    g.DrawPath(pen, path);
                }
            }

            // Центральная рамка
            using (var path = CreateRoundedRectPath(rect, 8))
            using (var pen = new Pen(Color.FromArgb(alpha, color), 2))
            {
                g.DrawPath(pen, path);
            }
        }

        private void DrawDashedBox(Graphics g, Rectangle rect, Color color, int alpha)
        {
            using (var path = CreateRoundedRectPath(rect, 8))
            using (var pen = new Pen(Color.FromArgb(alpha, color), 2))
            {
                pen.DashStyle = DashStyle.Dash;
                pen.DashPattern = new[] { 5f, 3f };
                g.DrawPath(pen, path);
            }
        }

        private void DrawGradientBox(Graphics g, Rectangle rect, Color color, int alpha)
        {
            using (var path = CreateRoundedRectPath(rect, 8))
            {
                // Градиентная рамка
                var brush = new LinearGradientBrush(
                    rect,
                    Color.FromArgb(alpha, color),
                    Color.FromArgb(alpha / 2, Color.White),
                    45f
                );

                using (var pen = new Pen(brush, 3))
                {
                    g.DrawPath(pen, path);
                }

                brush.Dispose();
            }
        }

        private void DrawCorners(Graphics g, Rectangle rect, Color color)
        {
            var cornerSize = 20;
            using (var pen = new Pen(color, 3))
            {
                // Верхний левый
                g.DrawLine(pen, rect.Left, rect.Top + cornerSize, rect.Left, rect.Top);
                g.DrawLine(pen, rect.Left, rect.Top, rect.Left + cornerSize, rect.Top);

                // Верхний правый
                g.DrawLine(pen, rect.Right - cornerSize, rect.Top, rect.Right, rect.Top);
                g.DrawLine(pen, rect.Right, rect.Top, rect.Right, rect.Top + cornerSize);

                // Нижний левый
                g.DrawLine(pen, rect.Left, rect.Bottom - cornerSize, rect.Left, rect.Bottom);
                g.DrawLine(pen, rect.Left, rect.Bottom, rect.Left + cornerSize, rect.Bottom);

                // Нижний правый
                g.DrawLine(pen, rect.Right - cornerSize, rect.Bottom, rect.Right, rect.Bottom);
                g.DrawLine(pen, rect.Right, rect.Bottom - cornerSize, rect.Right, rect.Bottom);
            }
        }

        private void DrawText(Graphics g, TextElement text, int alpha)
        {
            using (var font = CreateFont(text.Style))
            {
                var pos = text.Position;

                // Анимация появления
                if (text.AnimationProgress < 1f)
                {
                    pos.Y -= (int)(10 * (1 - text.AnimationProgress));
                }

                // Тень
                if (text.Style != TextStyle.Simple)
                {
                    using (var shadowBrush = new SolidBrush(Color.FromArgb(alpha / 2, Color.Black)))
                    {
                        g.DrawString(text.Text, font, shadowBrush, pos.X + 2, pos.Y + 2);
                    }
                }

                // Основной текст
                using (var brush = new SolidBrush(Color.FromArgb(alpha, text.Color)))
                {
                    g.DrawString(text.Text, font, brush, pos);
                }

                // Подчеркивание для Bold стиля
                if (text.Style == TextStyle.Bold)
                {
                    var size = g.MeasureString(text.Text, font);
                    using (var pen = new Pen(Color.FromArgb(alpha / 2, text.Color), 2))
                    {
                        g.DrawLine(pen, pos.X, pos.Y + size.Height, pos.X + size.Width, pos.Y + size.Height);
                    }
                }
            }
        }

        private void DrawProgress(Graphics g, ProgressElement progress, int alpha)
        {
            var rect = progress.Bounds;
            if (rect.Width <= 0 || rect.Height <= 0) return;

            // фон
            using (var bgPath = CreateRoundedRectPath(rect, 4))
            using (var bgBrush = new SolidBrush(Color.FromArgb(Math.Max(0, alpha / 4), Color.Black)))
            {
                g.FillPath(bgBrush, bgPath);
            }

            // безопасная ширина заполнения
            var ratio = Math.Max(0f, Math.Min(1f, progress.Progress * progress.AnimationProgress));
            var fillW = (int)(rect.Width * ratio);

            if (fillW > 0) // ← если 0, пропускаем заливку, чтобы не падать
            {
                var progressRect = new Rectangle(rect.X, rect.Y, Math.Max(1, fillW), rect.Height);

                using (var path = CreateRoundedRectPath(progressRect, 4))
                using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                    progressRect,
                    Color.FromArgb(alpha, progress.Color),
                    Color.FromArgb(alpha, Color.FromArgb(
                        Math.Min(255, progress.Color.R + 50),
                        Math.Min(255, progress.Color.G + 50),
                        Math.Min(255, progress.Color.B + 50))),
                    0f))
                {
                    g.FillPath(brush, path);
                }
            }

            // рамка
            using (var path = CreateRoundedRectPath(rect, 4))
            using (var pen = new Pen(Color.FromArgb(alpha, progress.Color), 2))
            {
                g.DrawPath(pen, path);
            }

            // проценты
            var text = $"{(int)(progress.Progress * 100)}%";
            using (var font = new Font("Segoe UI", 10, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.FromArgb(alpha, Color.White)))
            {
                var textSize = g.MeasureString(text, font);
                var textPos = new PointF(
                    rect.X + (rect.Width - textSize.Width) / 2,
                    rect.Y + (rect.Height - textSize.Height) / 2
                );
                g.DrawString(text, font, brush, textPos);
            }
        }


        private void DrawPulse(Graphics g, PulseElement pulse, int alpha)
        {
            var maxRadius = pulse.Radius * 3;

            // Рисуем несколько колец с разной фазой
            for (int i = 0; i < 3; i++)
            {
                var phase = (pulse.PulsePhase + i * Math.PI * 2 / 3) % (Math.PI * 2);
                var scale = (float)Math.Sin(phase) * 0.5f + 0.5f;
                var ringAlpha = (int)(alpha * (1 - scale) * pulse.AnimationProgress);
                var ringRadius = pulse.Radius + (maxRadius - pulse.Radius) * scale;

                using (var pen = new Pen(Color.FromArgb(ringAlpha, pulse.Color), 2))
                {
                    g.DrawEllipse(pen,
                        pulse.Center.X - ringRadius,
                        pulse.Center.Y - ringRadius,
                        ringRadius * 2,
                        ringRadius * 2);
                }
            }

            // Центральная точка
            using (var brush = new SolidBrush(Color.FromArgb(alpha, pulse.Color)))
            {
                g.FillEllipse(brush,
                    pulse.Center.X - pulse.Radius,
                    pulse.Center.Y - pulse.Radius,
                    pulse.Radius * 2,
                    pulse.Radius * 2);
            }
        }

        private void DrawArrow(Graphics g, ArrowElement arrow, int alpha)
        {
            var from = arrow.From;
            var to = arrow.To;

            // Анимация рисования
            if (arrow.AnimationProgress < 1f)
            {
                var dx = to.X - from.X;
                var dy = to.Y - from.Y;
                to = new Point(
                    from.X + (int)(dx * arrow.AnimationProgress),
                    from.Y + (int)(dy * arrow.AnimationProgress)
                );
            }

            using (var pen = new Pen(Color.FromArgb(alpha, arrow.Color), 3))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Custom;
                pen.CustomEndCap = new AdjustableArrowCap(5, 5);

                if (arrow.Style == ArrowStyle.Curved)
                {
                    // Кривая стрелка
                    var midX = (from.X + to.X) / 2;
                    var midY = (from.Y + to.Y) / 2 - 50;

                    using (var path = new GraphicsPath())
                    {
                        path.AddBezier(from, new Point(midX, midY), new Point(midX, midY), to);
                        g.DrawPath(pen, path);
                    }
                }
                else
                {
                    // Прямая стрелка
                    g.DrawLine(pen, from, to);
                }
            }
        }

        private void DrawTooltip(Graphics g, TooltipElement tooltip, int alpha)
        {
            using (var font = new Font("Segoe UI", 11))
            {
                var textSize = g.MeasureString(tooltip.Text, font);
                var padding = 10;
                var tooltipSize = new Size(
                    (int)textSize.Width + padding * 2,
                    (int)textSize.Height + padding * 2
                );

                // Вычисляем позицию
                var tooltipRect = CalculateTooltipRect(tooltip.Anchor, tooltipSize, tooltip.Position);

                // Анимация появления
                if (tooltip.AnimationProgress < 1f)
                {
                    var scale = 0.7f + 0.3f * tooltip.AnimationProgress;
                    var center = new Point(
                        tooltipRect.X + tooltipRect.Width / 2,
                        tooltipRect.Y + tooltipRect.Height / 2
                    );
                    tooltipRect = new Rectangle(
                        (int)(center.X - tooltipRect.Width * scale / 2),
                        (int)(center.Y - tooltipRect.Height * scale / 2),
                        (int)(tooltipRect.Width * scale),
                        (int)(tooltipRect.Height * scale)
                    );
                }

                // Фон с тенью
                var shadowRect = new Rectangle(tooltipRect.X + 3, tooltipRect.Y + 3, tooltipRect.Width, tooltipRect.Height);
                using (var shadowPath = CreateRoundedRectPath(shadowRect, 6))
                using (var shadowBrush = new SolidBrush(Color.FromArgb(alpha / 3, Color.Black)))
                {
                    g.FillPath(shadowBrush, shadowPath);
                }

                // Основной фон
                using (var path = CreateRoundedRectPath(tooltipRect, 6))
                using (var brush = new SolidBrush(Color.FromArgb(alpha * 9 / 10, tooltip.BackgroundColor)))
                {
                    g.FillPath(brush, path);
                }

                // Рамка
                using (var path = CreateRoundedRectPath(tooltipRect, 6))
                using (var pen = new Pen(Color.FromArgb(alpha, Color.White), 1))
                {
                    g.DrawPath(pen, path);
                }

                // Текст
                using (var textBrush = new SolidBrush(Color.FromArgb(alpha, Color.White)))
                {
                    g.DrawString(tooltip.Text, font, textBrush,
                        tooltipRect.X + padding,
                        tooltipRect.Y + padding);
                }
            }
        }

        private Rectangle CalculateTooltipRect(Point anchor, Size size, TooltipPosition position)
        {
            var offset = 15;

            return position switch
            {
                TooltipPosition.Top => new Rectangle(
                    anchor.X - size.Width / 2,
                    anchor.Y - size.Height - offset,
                    size.Width,
                    size.Height
                ),
                TooltipPosition.Bottom => new Rectangle(
                    anchor.X - size.Width / 2,
                    anchor.Y + offset,
                    size.Width,
                    size.Height
                ),
                TooltipPosition.Left => new Rectangle(
                    anchor.X - size.Width - offset,
                    anchor.Y - size.Height / 2,
                    size.Width,
                    size.Height
                ),
                TooltipPosition.Right => new Rectangle(
                    anchor.X + offset,
                    anchor.Y - size.Height / 2,
                    size.Width,
                    size.Height
                ),
                _ => new Rectangle(anchor.X, anchor.Y, size.Width, size.Height)
            };
        }

        private Font CreateFont(TextStyle style)
        {
            return style switch
            {
                TextStyle.Simple => new Font("Segoe UI", 12, FontStyle.Regular),
                TextStyle.Modern => new Font("Segoe UI", 14, FontStyle.Regular),
                TextStyle.Bold => new Font("Segoe UI", 14, FontStyle.Bold),
                TextStyle.Large => new Font("Segoe UI", 18, FontStyle.Bold),
                _ => new Font("Segoe UI", 12, FontStyle.Regular)
            };
        }

        private GraphicsPath CreateRoundedRectPath(Rectangle rect, float radius)
        {
            var path = new GraphicsPath();

            if (radius <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }

            float diameter = radius * 2;
            var arc = new RectangleF(rect.Location, new SizeF(diameter, diameter));

            // Верхний левый угол
            path.AddArc(arc, 180, 90);

            // Верхний правый угол
            arc.X = rect.Right - diameter;
            path.AddArc(arc, 270, 90);

            // Нижний правый угол
            arc.Y = rect.Bottom - diameter;
            path.AddArc(arc, 0, 90);

            // Нижний левый угол
            arc.X = rect.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }

        /// <summary>
        /// Поднять окно на самый верх
        /// </summary>
        public void BringToTop()
        {
            const uint SWP_NOMOVE = 0x0002;
            const uint SWP_NOSIZE = 0x0001;
            const uint SWP_NOACTIVATE = 0x0010;
            const uint SWP_SHOWWINDOW = 0x0040;

            SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _animationTimer?.Stop();
                _animationTimer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    #region Классы элементов

    public abstract class OverlayElement
    {
        public DateTime CreatedAt { get; set; }
        public float AnimationProgress { get; set; }
        public int ZOrder { get; set; }
    }

    public class BoxElement : OverlayElement
    {
        public Rectangle Bounds { get; set; }
        public Color Color { get; set; }
        public BoxStyle Style { get; set; }
    }

    public class TextElement : OverlayElement
    {
        public Point Position { get; set; }
        public string Text { get; set; }
        public Color Color { get; set; }
        public TextStyle Style { get; set; }
    }

    public class ProgressElement : OverlayElement
    {
        public Rectangle Bounds { get; set; }
        public float Progress { get; set; }
        public Color Color { get; set; }
    }

    public class PulseElement : OverlayElement
    {
        public Point Center { get; set; }
        public float Radius { get; set; }
        public Color Color { get; set; }
        public float PulsePhase { get; set; }
    }

    public class ArrowElement : OverlayElement
    {
        public Point From { get; set; }
        public Point To { get; set; }
        public Color Color { get; set; }
        public ArrowStyle Style { get; set; }
    }

    public class TooltipElement : OverlayElement
    {
        public Point Anchor { get; set; }
        public string Text { get; set; }
        public Color BackgroundColor { get; set; }
        public TooltipPosition Position { get; set; }
    }

    #endregion

    #region Перечисления стилей

    public enum BoxStyle
    {
        Modern,
        Glow,
        Dashed,
        Gradient
    }

    public enum TextStyle
    {
        Simple,
        Modern,
        Bold,
        Large
    }

    public enum ArrowStyle
    {
        Modern,
        Curved
    }

    public enum TooltipPosition
    {
        Top,
        Bottom,
        Left,
        Right
    }

    #endregion
}