// Infrastructure/Matching/OpenCvPreprocessor.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using AutomationCore.Core.Abstractions;
using AutomationCore.Core.Models;
using OpenCvSharp;

namespace AutomationCore.Infrastructure.Matching
{
    /// <summary>
    /// Препроцессор изображений на основе OpenCV
    /// </summary>
    public sealed class OpenCvPreprocessor : IImagePreprocessor
    {
        public async ValueTask<ProcessedImage> ProcessAsync(
            ReadOnlyMemory<byte> imageData,
            int width, int height, int channels,
            PreprocessingOptions options,
            CancellationToken ct = default)
        {
            await Task.Yield();
            ct.ThrowIfCancellationRequested();

            try
            {
                // Конвертируем данные в Mat
                using var srcMat = new Mat(height, width, channels == 3 ? MatType.CV_8UC3 : MatType.CV_8UC4, imageData.ToArray());

                Mat processedMat = srcMat;
                bool needsDispose = false;

                // Конвертация в градации серого
                if (options.UseGray && channels > 1)
                {
                    var grayMat = new Mat();
                    Cv2.CvtColor(processedMat, grayMat, ColorConversionCodes.BGR2GRAY);

                    if (needsDispose)
                        processedMat.Dispose();

                    processedMat = grayMat;
                    needsDispose = true;
                    channels = 1;
                }

                // Размытие Гаусса
                if (options.BlurSize.HasValue && options.BlurSize.Value.Width > 0)
                {
                    var blurredMat = new Mat();
                    Cv2.GaussianBlur(processedMat, blurredMat, options.BlurSize.Value, options.GaussianSigma);

                    if (needsDispose)
                        processedMat.Dispose();

                    processedMat = blurredMat;
                    needsDispose = true;
                }

                // Детектор Canny
                if (options.UseCanny)
                {
                    var cannyMat = new Mat();
                    Cv2.Canny(processedMat, cannyMat, options.CannyThreshold1, options.CannyThreshold2);

                    if (needsDispose)
                        processedMat.Dispose();

                    processedMat = cannyMat;
                    needsDispose = true;
                }

                // Копируем данные в managed память
                var resultData = new byte[processedMat.Total() * processedMat.ElemSize()];
                processedMat.GetArray(out resultData);

                if (needsDispose)
                    processedMat.Dispose();

                return new ProcessedImage
                {
                    Data = resultData,
                    Width = processedMat.Width,
                    Height = processedMat.Height,
                    Channels = channels,
                    AppliedOptions = options
                };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to preprocess image: {ex.Message}", ex);
            }
        }
    }
}