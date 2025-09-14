// Enhanced ScreenCapture (refactored from monolith)
// Высокоуровневая обертка для захвата экрана и поиска шаблонов
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using AutomationCore.Core.Abstractions;
using AutomationCore.Core.Models;
using AutomationCore.Core.Abstractions;
using AutomationCore.Infrastructure.Storage;
using AutomationCore.Infrastructure.Capture;
using AutomationCore.Core.Domain.Matching;
using OpenCvSharp;

using SDPoint = System.Drawing.Point;

namespace AutomationCore.Core.Domain.Capture
{
    /// <summary>
    /// Высокоуровневый класс для захвата экрана с расширенными возможностями
    /// </summary>
    public class EnhancedScreenCapture : IDisposable
    {
        // ===== Public result type (единый) =====
        public sealed record MatchResult(System.Drawing.Rectangle Bounds,
                                          SDPoint Center,
                                          double Score,
                                          double Scale,
                                          bool IsHardPass);

        private readonly Dictionary<IntPtr, EnhancedWindowsGraphicsCapture> _captureInstances = new();
        private readonly object _lock = new();

        private readonly ITemplateStore _templates;
        private CaptureSettings _defaultSettings;
        private readonly TemplateMatchOptions _defaultMatchOptions;

        // Кэш препроцессинга шаблонов (после Gray/Blur/Canny, до масштабирования)
        private readonly AutomationCore.Core.Domain.Matching.TemplatePreprocessCache _prepCache = new();

        // Последние удачные координаты по ключу (для локального ROI)
        private readonly ConcurrentDictionary<string, SDPoint> _lastHits = new();

        public EnhancedScreenCapture(
            CaptureSettings defaultSettings = null,
            ITemplateStore templateStore = null,
            TemplateMatchOptions defaultMatchOptions = null)
        {
            _defaultSettings = defaultSettings ?? CaptureSettings.Default;
            _templates = templateStore ?? new TemplateStorageAdapter(new FileTemplateStorage("templates"));
            _defaultMatchOptions = defaultMatchOptions ?? TemplatePresets.Universal;
        }

        // TODO: Остальные методы будут перенесены в отдельные сервисы
        // Этот класс пока оставлен для обратной совместимости

        public void Dispose()
        {
            lock (_lock)
            {
                foreach (var capture in _captureInstances.Values)
                {
                    capture?.Dispose();
                }
                _captureInstances.Clear();
            }

            _prepCache?.Dispose();
        }

        // Статические методы конвертации для обратной совместимости
        public static Mat ConvertToMat(CaptureFrame frame)
        {
            // TODO: Реализовать конвертацию из CaptureFrame в OpenCV Mat
            throw new NotImplementedException("ConvertToMat needs implementation");
        }
    }
}