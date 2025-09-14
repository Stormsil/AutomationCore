// Infrastructure/Matching/OpenCvPreprocessor.cs
using System;
using System.Threading.Tasks;
using AutomationCore.Core.Domain.Matching;
using AutomationCore.Core.Models;
using OpenCvSharp;

namespace AutomationCore.Infrastructure.Matching
{
    /// <summary>
    /// Препроцессор изображений на основе OpenCV
    /// </summary>
    public sealed class OpenCvPreprocessor : IPreprocessor
    {
        public Task<Mat> ProcessAsync(Mat src, PreprocessingOptions options)
        {
            if (src == null || src.Empty())
                return Task.FromResult(new Mat());

            Mat processedMat = src;
            bool needsDispose = false;

            try
            {
                // Конвертация в градации серого
                if (options.UseGray && src.Channels() > 1)
                {
                    var grayMat = new Mat();
                    Cv2.CvtColor(processedMat, grayMat, ColorConversionCodes.BGR2GRAY);

                    if (needsDispose)
                        processedMat.Dispose();

                    processedMat = grayMat;
                    needsDispose = true;
                }

                // Размытие Гаусса
                if (options.Blur.HasValue && options.Blur.Value.Width > 0)
                {
                    var blurredMat = new Mat();
                    Cv2.GaussianBlur(processedMat, blurredMat, options.Blur.Value, options.GaussianSigma);

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

                return Task.FromResult(processedMat);
            }
            catch (Exception)
            {
                if (needsDispose)
                    processedMat?.Dispose();
                return Task.FromResult(new Mat());
            }
        }
    }
}