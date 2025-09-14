// Core/Configuration/AutomationOptions.cs
using AutomationCore.Core.Models;
using System;

namespace AutomationCore.Core.Configuration
{
    /// <summary>
    /// Главные настройки автоматизации
    /// </summary>
    public sealed class AutomationOptions
    {
        public static AutomationOptions Default { get; } = new();

        /// <summary>Настройки ввода</summary>
        public InputOptions Input { get; set; } = new();

        /// <summary>Настройки захвата экрана</summary>
        public CaptureOptions Capture { get; set; } = new();

        /// <summary>Настройки сопоставления шаблонов</summary>
        public TemplateMatchingOptions TemplateMatching { get; set; } = new();

        /// <summary>Настройки хранения шаблонов</summary>
        public TemplateStoreOptions TemplateStorage { get; set; } = new();

        /// <summary>Включить расширенное логирование</summary>
        public bool EnableVerboseLogging { get; set; } = false;

        /// <summary>Использовать аппаратное ускорение где возможно</summary>
        public bool EnableHardwareAcceleration { get; set; } = true;
    }

    /// <summary>
    /// Настройки захвата экрана
    /// </summary>
    public sealed class CaptureOptions
    {
        /// <summary>Таймаут операций захвата</summary>
        public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>Максимальное количество повторов</summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>Использовать Windows Graphics Capture</summary>
        public bool UseWindowsGraphicsCapture { get; set; } = true;

        /// <summary>Использовать аппаратное ускорение</summary>
        public bool UseHardwareAcceleration { get; set; } = true;
    }

    /// <summary>
    /// Настройки сопоставления шаблонов
    /// </summary>
    public sealed class TemplateMatchingOptions
    {
        /// <summary>Порог совпадения по умолчанию</summary>
        public double DefaultThreshold { get; set; } = 0.9;

        /// <summary>Использовать мультимасштабирование по умолчанию</summary>
        public bool DefaultUseMultiScale { get; set; } = true;

        /// <summary>Диапазон масштабирования по умолчанию</summary>
        public ScaleRange DefaultScaleRange { get; set; } = new(0.97, 1.03, 0.01);

        /// <summary>Максимальное количество результатов</summary>
        public int MaxResults { get; set; } = 10;

        /// <summary>Включить кэширование результатов</summary>
        public bool EnableCaching { get; set; } = true;

        /// <summary>Время жизни кэша</summary>
        public TimeSpan CacheTtl { get; set; } = TimeSpan.FromSeconds(5);
    }
}