// Core/Models/TemplatePresets.cs
using AutomationCore.Core.Models;
using OpenCvSharp;

namespace AutomationCore.Core.Models
{
    /// <summary>
    /// Предустановленные настройки поиска шаблонов
    /// </summary>
    public static class TemplatePresets
    {
        /// <summary>
        /// Универсальные настройки - баланс скорости и точности
        /// </summary>
        public static TemplateMatchOptions Universal => new()
        {
            Threshold = 0.85,
            ScaleMin = 0.95,
            ScaleMax = 1.05,
            ScaleStep = 0.01,
            UseGray = true,
            UseCanny = false,
            Blur = new OpenCvSharp.Size(3, 3)
        };

        /// <summary>
        /// Быстрый поиск - минимальная точность, максимальная скорость
        /// </summary>
        public static TemplateMatchOptions Fast => new()
        {
            Threshold = 0.75,
            ScaleMin = 1.0,
            ScaleMax = 1.0,
            ScaleStep = 0.01,
            UseGray = true,
            UseCanny = false,
            Blur = new OpenCvSharp.Size(1, 1)
        };

        /// <summary>
        /// Точный поиск - максимальная точность, может быть медленнее
        /// </summary>
        public static TemplateMatchOptions Accurate => new()
        {
            Threshold = 0.95,
            ScaleMin = 0.9,
            ScaleMax = 1.1,
            ScaleStep = 0.005,
            UseGray = true,
            UseCanny = false,
            Blur = new OpenCvSharp.Size(5, 5)
        };

        /// <summary>
        /// Поиск элементов UI - оптимизировано для интерфейсов
        /// </summary>
        public static TemplateMatchOptions UI => new()
        {
            Threshold = 0.9,
            ScaleMin = 0.98,
            ScaleMax = 1.02,
            ScaleStep = 0.01,
            UseGray = true,
            UseCanny = false,
            Blur = new OpenCvSharp.Size(2, 2)
        };

        /// <summary>
        /// Поиск на разных масштабах - для приложений с изменяемым DPI
        /// </summary>
        public static TemplateMatchOptions MultiScale => new()
        {
            Threshold = 0.8,
            ScaleMin = 0.7,
            ScaleMax = 1.3,
            ScaleStep = 0.02,
            UseGray = true,
            UseCanny = false,
            Blur = new OpenCvSharp.Size(3, 3)
        };
    }
}