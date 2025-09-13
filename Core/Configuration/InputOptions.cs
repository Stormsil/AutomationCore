using AutomationCore.Input;
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
}

