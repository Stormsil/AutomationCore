// AutomationCore/Core/ClickOptions.cs
using AutomationCore.Core.Abstractions;
using AutomationCore.Core.Models;

namespace AutomationCore.Core
{
    /// <summary>Опции высокого уровня для клика по шаблону.</summary>
    public class ClickOptions
    {
        public static ClickOptions Default { get; } = new();

        /// <summary>Опции сопоставления шаблона (по умолчанию — универсальный пресет).</summary>
        public TemplateMatchOptions MatchOptions { get; set; } = TemplatePresets.Universal;

        /// <summary>Показать временный оверлей рамкой по найденному региону.</summary>
        public bool ShowOverlay { get; set; } = false;

        /// <summary>Опции перемещения мыши.</summary>
        public MouseMoveOptions MouseOptions { get; set; } = new();

        /// <summary>Пауза перед кликом (мс).</summary>
        public int DelayBeforeClick { get; set; } = 60;

        /// <summary>Кнопка мыши.</summary>
        public MouseButton Button { get; set; } = MouseButton.Left;
    }
}
