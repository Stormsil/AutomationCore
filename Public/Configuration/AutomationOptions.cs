// Public/Configuration/AutomationOptions.cs
using System;
using System.IO;
using AutomationCore.Core.Models;

namespace AutomationCore.Public.Configuration
{
    /// <summary>
    /// Основные настройки системы автоматизации
    /// </summary>
    public sealed class AutomationOptions
    {
        /// <summary>Путь к папке с шаблонами изображений</summary>
        public string TemplatesPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "templates");

        /// <summary>Путь для временных файлов</summary>
        public string TempPath { get; set; } = Path.Combine(Path.GetTempPath(), "AutomationCore");

        /// <summary>Настройки захвата экрана</summary>
        public CaptureConfiguration Capture { get; set; } = new();

        /// <summary>Настройки симуляции ввода</summary>
        public InputConfiguration Input { get; set; } = new();

        /// <summary>Настройки сопоставления шаблонов</summary>
        public MatchingConfiguration Matching { get; set; } = new();

        /// <summary>Настройки кэширования</summary>
        public CacheConfiguration Cache { get; set; } = new();

        /// <summary>Настройки логирования</summary>
        public LoggingConfiguration Logging { get; set; } = new();

        /// <summary>Создает настройки по умолчанию</summary>
        public static AutomationOptions Default => new();

        /// <summary>Настройки для максимальной производительности</summary>
        public static AutomationOptions HighPerformance => new()
        {
            Capture = CaptureConfiguration.HighPerformance,
            Input = InputConfiguration.Fast,
            Matching = MatchingConfiguration.Fast,
            Cache = CacheConfiguration.Aggressive
        };

        /// <summary>Настройки для максимального качества</summary>
        public static AutomationOptions HighQuality => new()
        {
            Capture = CaptureConfiguration.HighQuality,
            Input = InputConfiguration.VeryHuman,
            Matching = MatchingConfiguration.Accurate,
            Cache = CacheConfiguration.Conservative
        };
    }

    /// <summary>
    /// Настройки захвата экрана
    /// </summary>
    public sealed class CaptureConfiguration
    {
        /// <summary>Использовать аппаратное ускорение</summary>
        public bool UseHardwareAcceleration { get; set; } = true;

        /// <summary>Целевой FPS</summary>
        public int TargetFps { get; set; } = 60;

        /// <summary>Размер пула кадров</summary>
        public int FramePoolSize { get; set; } = 2;

        /// <summary>Таймаут захвата</summary>
        public TimeSpan CaptureTimeout { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>Автоматически отслеживать изменения окон</summary>
        public bool AutoTrackWindows { get; set; } = true;

        /// <summary>Захватывать курсор мыши</summary>
        public bool CaptureCursor { get; set; } = false;

        public static CaptureConfiguration Default => new();

        public static CaptureConfiguration HighPerformance => new()
        {
            UseHardwareAcceleration = true,
            TargetFps = 144,
            FramePoolSize = 3,
            CaptureTimeout = TimeSpan.FromSeconds(1)
        };

        public static CaptureConfiguration HighQuality => new()
        {
            UseHardwareAcceleration = true,
            TargetFps = 30,
            FramePoolSize = 1,
            CaptureTimeout = TimeSpan.FromSeconds(10)
        };
    }

    /// <summary>
    /// Настройки симуляции ввода
    /// </summary>
    public sealed class InputConfiguration
    {
        /// <summary>Использовать человекоподобное поведение</summary>
        public bool UseHumanBehavior { get; set; } = true;

        /// <summary>Множитель скорости мыши</summary>
        public double MouseSpeedMultiplier { get; set; } = 1.0;

        /// <summary>Сложность траектории мыши (0-100)</summary>
        public int MouseTrajectoryComplexity { get; set; } = 50;

        /// <summary>Включить микро-движения мыши</summary>
        public bool EnableMicroMovements { get; set; } = true;

        /// <summary>Базовая скорость печати (мс между символами)</summary>
        public int TypingSpeed { get; set; } = 50;

        /// <summary>Разрешить опечатки</summary>
        public bool EnableTypos { get; set; } = false;

        /// <summary>Вероятность опечатки (0.0 - 1.0)</summary>
        public double TypoChance { get; set; } = 0.01;

        public static InputConfiguration Default => new();

        public static InputConfiguration Fast => new()
        {
            UseHumanBehavior = false,
            MouseSpeedMultiplier = 0.1,
            MouseTrajectoryComplexity = 0,
            EnableMicroMovements = false,
            TypingSpeed = 10,
            EnableTypos = false
        };

        public static InputConfiguration VeryHuman => new()
        {
            UseHumanBehavior = true,
            MouseSpeedMultiplier = 2.5,
            MouseTrajectoryComplexity = 100,
            EnableMicroMovements = true,
            TypingSpeed = 100,
            EnableTypos = true,
            TypoChance = 0.02
        };
    }

    /// <summary>
    /// Настройки сопоставления шаблонов
    /// </summary>
    public sealed class MatchingConfiguration
    {
        /// <summary>Пороговое значение по умолчанию</summary>
        public double DefaultThreshold { get; set; } = 0.9;

        /// <summary>Использовать мульти-масштабирование</summary>
        public bool UseMultiScale { get; set; } = true;

        /// <summary>Диапазон масштабирования</summary>
        public ScaleRange ScaleRange { get; set; } = new(0.95, 1.05, 0.01);

        /// <summary>Использовать предобработку изображений</summary>
        public bool UsePreprocessing { get; set; } = true;

        /// <summary>Конвертировать в градации серого</summary>
        public bool UseGrayscale { get; set; } = true;

        /// <summary>Применять размытие</summary>
        public bool UseBlur { get; set; } = true;

        /// <summary>Размер размытия</summary>
        public int BlurSize { get; set; } = 3;

        /// <summary>Максимальное количество результатов</summary>
        public int MaxResults { get; set; } = 10;

        /// <summary>Порог перекрытия для NMS</summary>
        public double NmsOverlapThreshold { get; set; } = 0.3;

        public static MatchingConfiguration Default => new();

        public static MatchingConfiguration Fast => new()
        {
            DefaultThreshold = 0.85,
            UseMultiScale = false,
            UsePreprocessing = false,
            UseGrayscale = true,
            UseBlur = false,
            MaxResults = 1
        };

        public static MatchingConfiguration Accurate => new()
        {
            DefaultThreshold = 0.95,
            UseMultiScale = true,
            ScaleRange = new(0.8, 1.2, 0.005),
            UsePreprocessing = true,
            UseGrayscale = true,
            UseBlur = true,
            BlurSize = 5,
            MaxResults = 50
        };
    }

    /// <summary>
    /// Настройки кэширования
    /// </summary>
    public sealed class CacheConfiguration
    {
        /// <summary>Включить кэширование результатов сопоставления</summary>
        public bool EnableMatchCache { get; set; } = true;

        /// <summary>Размер кэша сопоставлений</summary>
        public int MatchCacheSize { get; set; } = 256;

        /// <summary>TTL кэша сопоставлений</summary>
        public TimeSpan MatchCacheTtl { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>Включить кэширование информации о окнах</summary>
        public bool EnableWindowCache { get; set; } = true;

        /// <summary>TTL кэша окон</summary>
        public TimeSpan WindowCacheTtl { get; set; } = TimeSpan.FromSeconds(2);

        /// <summary>Включить кэширование шаблонов</summary>
        public bool EnableTemplateCache { get; set; } = true;

        /// <summary>Максимальный размер кэша шаблонов (MB)</summary>
        public int TemplateCacheMaxSizeMb { get; set; } = 100;

        public static CacheConfiguration Default => new();

        public static CacheConfiguration Aggressive => new()
        {
            EnableMatchCache = true,
            MatchCacheSize = 1024,
            MatchCacheTtl = TimeSpan.FromSeconds(10),
            EnableWindowCache = true,
            WindowCacheTtl = TimeSpan.FromSeconds(5),
            EnableTemplateCache = true,
            TemplateCacheMaxSizeMb = 500
        };

        public static CacheConfiguration Conservative => new()
        {
            EnableMatchCache = true,
            MatchCacheSize = 64,
            MatchCacheTtl = TimeSpan.FromSeconds(1),
            EnableWindowCache = false,
            EnableTemplateCache = true,
            TemplateCacheMaxSizeMb = 50
        };
    }

    /// <summary>
    /// Настройки логирования
    /// </summary>
    public sealed class LoggingConfiguration
    {
        /// <summary>Включить подробное логирование</summary>
        public bool EnableVerboseLogging { get; set; } = false;

        /// <summary>Логировать метрики производительности</summary>
        public bool LogPerformanceMetrics { get; set; } = false;

        /// <summary>Логировать события захвата</summary>
        public bool LogCaptureEvents { get; set; } = false;

        /// <summary>Логировать события ввода</summary>
        public bool LogInputEvents { get; set; } = false;

        /// <summary>Путь к лог-файлу (если null - только в консоль)</summary>
        public string? LogFilePath { get; set; }

        /// <summary>Максимальный размер лог-файла (MB)</summary>
        public int MaxLogFileSizeMb { get; set; } = 10;

        public static LoggingConfiguration Default => new();

        public static LoggingConfiguration Debug => new()
        {
            EnableVerboseLogging = true,
            LogPerformanceMetrics = true,
            LogCaptureEvents = true,
            LogInputEvents = true,
            LogFilePath = Path.Combine(Path.GetTempPath(), "AutomationCore", "debug.log")
        };
    }
}