// Infrastructure/Matching/OpenCvMatchingEngine.cs
using System;
using System.Threading.Tasks;
using AutomationCore.Core.Domain.Matching;
using AutomationCore.Core.Models;
using OpenCvSharp;

namespace AutomationCore.Infrastructure.Matching
{
    /// <summary>
    /// Реализация поиска шаблонов через OpenCV
    /// </summary>
    public sealed class OpenCvMatchingEngine : IMatchingEngine
    {
        public Task<MatchResult> FindBestMatchAsync(Mat sourceProcessed, Mat templateProcessed, MatchOptions options)
        {
            if (sourceProcessed == null || templateProcessed == null || sourceProcessed.Empty() || templateProcessed.Empty())
            {
                return Task.FromResult(MatchResult.NotFound);
            }

            try
            {
                // Выполняем поиск шаблона
                using var result = new Mat();
                Cv2.MatchTemplate(sourceProcessed, templateProcessed, result, options.Algorithm);

                // Находим лучшее совпадение
                Cv2.MinMaxLoc(result, out double minVal, out double maxVal, out OpenCvSharp.Point minLoc, out OpenCvSharp.Point maxLoc);

                // Выбираем правильную точку в зависимости от алгоритма
                OpenCvSharp.Point matchLoc = options.HigherIsBetter ? maxLoc : minLoc;
                double score = options.HigherIsBetter ? maxVal : (1.0 - minVal);

                // Проверяем порог
                bool isMatch = score >= options.Threshold;

                // Создаем результат
                var bounds = new System.Drawing.Rectangle(
                    matchLoc.X,
                    matchLoc.Y,
                    templateProcessed.Width,
                    templateProcessed.Height);

                var center = new System.Drawing.Point(
                    bounds.X + bounds.Width / 2,
                    bounds.Y + bounds.Height / 2);

                return Task.FromResult(new MatchResult(bounds, center, score, 1.0, isMatch));
            }
            catch (Exception)
            {
                return Task.FromResult(MatchResult.NotFound);
            }
        }
    }
}