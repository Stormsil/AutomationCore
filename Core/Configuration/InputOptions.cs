using AutomationCore.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutomationCore.Core.Configuration
{
    /// <summary>Упрощённые опции, которые использует AutomationBuilder.</summary>
    public class InputOptions
    {
        public double MouseSpeed { get; set; } = 1.5;
        public int TypingSpeed { get; set; } = 50;
        public bool EnableHumanization { get; set; } = true;

        internal InputSettings ToSettings() => new InputSettings
        {
            MouseSpeedMultiplier = MouseSpeed,
            EnableMicroMovements = EnableHumanization
        };
    }

    /// <summary>
    /// Настройки ввода для внутреннего использования
    /// </summary>
    internal sealed class InputSettings
    {
        public double MouseSpeedMultiplier { get; set; } = 1.0;
        public bool EnableMicroMovements { get; set; } = true;
        public TimeSpan DefaultClickDelay { get; set; } = TimeSpan.FromMilliseconds(50);
        public TimeSpan DefaultKeyDelay { get; set; } = TimeSpan.FromMilliseconds(50);
    }
}

