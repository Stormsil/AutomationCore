// AutomationCore/Input/Options.cs
using System.Drawing;

namespace AutomationCore.Input
{
    public class MouseMoveOptions
    {
        /// <summary>Желаемая длительность перемещения (мс). Если 0 — вычисляется по расстоянию.</summary>
        public int DurationMs { get; set; } = 0;
        /// <summary>Делать ли «человеческую» траекторию (дрожание/кривая)</summary>
        public bool Humanize { get; set; } = true;
    }

    public class DragOptions
    {
        /// <summary>Длительность перетаскивания (мс).</summary>
        public int DurationMs { get; set; } = 500;
        /// <summary>Промежуточная пауза после зажатия (мс).</summary>
        public int HoldDelayBeforeMoveMs { get; set; } = 120;
        /// <summary>Пауза после отпускания (мс).</summary>
        public int ReleaseDelayMs { get; set; } = 80;
    }

    public class TypingOptions
    {
        /// <summary>Базовая задержка между символами (мс).</summary>
        public int BaseDelayMs { get; set; } = 50;
        /// <summary>Включить случайные «человеческие» паузы.</summary>
        public bool Humanize { get; set; } = true;
        /// <summary>Разрешить опечатки с автокоррекцией.</summary>
        public bool EnableTypos { get; set; } = false;
    }
}
