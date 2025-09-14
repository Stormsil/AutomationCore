// Template File Loader - отвечает только за загрузку файлов шаблонов
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AutomationCore.Core.Abstractions;
using AutomationCore.Core.Models;
using OpenCvSharp;
using AbstractionsTemplateData = AutomationCore.Core.Abstractions.TemplateData;

namespace AutomationCore.Infrastructure.Storage.Components
{
    /// <summary>
    /// Компонент для загрузки шаблонов из файлов
    /// </summary>
    internal sealed class TemplateFileLoader
    {
        private static readonly string[] SupportedExtensions = { ".png", ".jpg", ".jpeg", ".bmp" };

        /// <summary>
        /// Загружает шаблон из файла
        /// </summary>
        public async ValueTask<AbstractionsTemplateData> LoadFromFileAsync(string filePath, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Template file not found: {filePath}");

            if (!IsFileSupported(filePath))
                throw new NotSupportedException($"Unsupported file format: {Path.GetExtension(filePath)}");

            try
            {
                // Асинхронно читаем файл
                var fileData = await File.ReadAllBytesAsync(filePath, ct);

                // Загружаем изображение через OpenCV
                using var mat = Cv2.ImDecode(fileData, ImreadModes.Color);
                if (mat.Empty())
                    throw new InvalidOperationException($"Failed to decode image file: {filePath}");

                // Конвертируем в AbstractionsTemplateData
                var templateData = new AbstractionsTemplateData
                {
                    Data = fileData,
                    Width = mat.Width,
                    Height = mat.Height,
                    Channels = mat.Channels(),
                    CreatedAt = File.GetCreationTimeUtc(filePath),
                    ModifiedAt = File.GetLastWriteTimeUtc(filePath)
                };

                return templateData;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new InvalidOperationException($"Failed to load template from {filePath}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Проверяет существует ли файл шаблона
        /// </summary>
        public bool FileExists(string basePath, string key)
        {
            foreach (var extension in SupportedExtensions)
            {
                var filePath = Path.Combine(basePath, key + extension);
                if (File.Exists(filePath))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Находит файл шаблона по ключу
        /// </summary>
        public string? FindTemplateFile(string basePath, string key)
        {
            foreach (var extension in SupportedExtensions)
            {
                var filePath = Path.Combine(basePath, key + extension);
                if (File.Exists(filePath))
                    return filePath;
            }
            return null;
        }

        /// <summary>
        /// Получает все файлы шаблонов в папке
        /// </summary>
        public string[] GetAllTemplateFiles(string basePath)
        {
            if (!Directory.Exists(basePath))
                return Array.Empty<string>();

            var files = new System.Collections.Generic.List<string>();

            foreach (var extension in SupportedExtensions)
            {
                var pattern = "*" + extension;
                files.AddRange(Directory.GetFiles(basePath, pattern, SearchOption.TopDirectoryOnly));
            }

            return files.ToArray();
        }

        private static bool IsFileSupported(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return Array.IndexOf(SupportedExtensions, extension) >= 0;
        }

        private static ImageFormat GetImageFormat(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".png" => ImageFormat.PNG,
                ".jpg" or ".jpeg" => ImageFormat.JPEG,
                ".bmp" => ImageFormat.BMP,
                _ => ImageFormat.PNG // По умолчанию
            };
        }
    }
}