using AutomationCore.Core.Abstractions;
using AutomationCore.Core.Models;
using OpenCvSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AutomationCore.Infrastructure.Storage
{
    /// <summary>
    /// Файловое хранилище шаблонов изображений.
    /// Реализует новый интерфейс ITemplateStorage с async/await паттернами.
    /// Поддерживает форматы: .png, .jpg, .jpeg, .bmp.
    ///
    /// Особенности:
    /// - Потокобезопасный кэш с автоматической инвалидацией
    /// - FileSystemWatcher для отслеживания изменений
    /// - Асинхронные операции загрузки
    /// - Конвертация данных в формат TemplateData
    /// </summary>
    public sealed class FileTemplateStorage : ITemplateStorage
    {
        private readonly string _basePath;
        private readonly ConcurrentDictionary<string, TemplateData> _cache = new(StringComparer.OrdinalIgnoreCase);
        private readonly FileSystemWatcher? _watcher;
        private readonly SemaphoreSlim _loadingSemaphore = new(10); // Ограничиваем параллельную загрузку
        private bool _disposed;

        private static readonly string[] SupportedExtensions = { ".png", ".jpg", ".jpeg", ".bmp" };

        public event EventHandler<TemplateChangedEventArgs>? TemplateChanged;

        /// <summary>
        /// Создает новое файловое хранилище шаблонов
        /// </summary>
        /// <param name="basePath">Путь к папке с шаблонами</param>
        /// <param name="watchForChanges">Отслеживать изменения файлов</param>
        public FileTemplateStorage(string basePath, bool watchForChanges = true)
        {
            _basePath = basePath ?? throw new ArgumentNullException(nameof(basePath));

            // Создаем директорию если не существует
            try
            {
                Directory.CreateDirectory(_basePath);
            }
            catch
            {
                // Игнорируем ошибки создания - будем пытаться читать из существующих путей
            }

            // Настраиваем FileSystemWatcher
            if (watchForChanges && Directory.Exists(_basePath))
            {
                try
                {
                    _watcher = new FileSystemWatcher(_basePath)
                    {
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                        IncludeSubdirectories = true,
                        EnableRaisingEvents = true
                    };

                    _watcher.Changed += OnFileChanged;
                    _watcher.Created += OnFileCreated;
                    _watcher.Deleted += OnFileDeleted;
                    _watcher.Renamed += OnFileRenamed;
                }
                catch
                {
                    // Если не удалось настроить watcher - продолжаем без него
                    _watcher?.Dispose();
                    _watcher = null;
                }
            }
        }

        /// <inheritdoc />
        public async ValueTask<bool> ContainsAsync(string key, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(key))
                return false;

            // Проверяем кэш
            if (_cache.ContainsKey(key))
                return true;

            // Проверяем файловую систему
            var fullPath = ResolvePath(key);
            return fullPath != null && File.Exists(fullPath);
        }

        /// <inheritdoc />
        public async ValueTask<TemplateData> LoadAsync(string key, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Template key cannot be null or empty", nameof(key));

            // Проверяем кэш
            if (_cache.TryGetValue(key, out var cached))
            {
                // Проверяем актуальность кэша
                var filePath = ResolvePath(key);
                if (filePath != null && File.Exists(filePath))
                {
                    var fileInfo = new FileInfo(filePath);
                    if (fileInfo.LastWriteTimeUtc == cached.ModifiedAt)
                    {
                        return cached;
                    }
                }
                else
                {
                    // Файл удален - удаляем из кэша
                    _cache.TryRemove(key, out _);
                    throw new FileNotFoundException($"Template '{key}' not found");
                }
            }

            // Загружаем с диска
            await _loadingSemaphore.WaitAsync(ct);
            try
            {
                return await LoadFromFileAsync(key, ct);
            }
            finally
            {
                _loadingSemaphore.Release();
            }
        }

        /// <inheritdoc />
        public async ValueTask SaveAsync(string key, TemplateData template, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Template key cannot be null or empty", nameof(key));
            if (template == null)
                throw new ArgumentNullException(nameof(template));

            var fullPath = GetSavePath(key);
            var directory = Path.GetDirectoryName(fullPath);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Конвертируем данные обратно в Mat для сохранения
            var mat = CreateMatFromTemplateData(template);
            try
            {
                // Сохраняем файл
                var success = await Task.Run(() => Cv2.ImWrite(fullPath, mat), ct);
                if (!success)
                    throw new InvalidOperationException($"Failed to save template '{key}' to '{fullPath}'");

                // Обновляем кэш
                var updatedTemplate = template with { ModifiedAt = DateTime.UtcNow };
                _cache.AddOrUpdate(key, updatedTemplate, (_, _) => updatedTemplate);

                // Уведомляем о создании/изменении
                var changeType = File.Exists(fullPath) ? TemplateChangeType.Modified : TemplateChangeType.Created;
                OnTemplateChanged(new TemplateChangedEventArgs { Key = key, ChangeType = changeType });
            }
            finally
            {
                mat.Dispose();
            }
        }

        /// <inheritdoc />
        public async ValueTask<bool> DeleteAsync(string key, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(key))
                return false;

            var fullPath = ResolvePath(key);
            if (fullPath == null || !File.Exists(fullPath))
                return false;

            try
            {
                await Task.Run(() => File.Delete(fullPath), ct);
                _cache.TryRemove(key, out _);

                OnTemplateChanged(TemplateChangedEventArgs.Deleted(key));
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <inheritdoc />
        public async ValueTask<IReadOnlyList<string>> GetKeysAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();

            if (!Directory.Exists(_basePath))
                return Array.Empty<string>();

            var keys = new List<string>();

            await Task.Run(() =>
            {
                foreach (var ext in SupportedExtensions)
                {
                    var pattern = "*" + ext;
                    var files = Directory.GetFiles(_basePath, pattern, SearchOption.AllDirectories);

                    foreach (var file in files)
                    {
                        var relativePath = Path.GetRelativePath(_basePath, file);
                        var key = Path.ChangeExtension(relativePath, null); // Убираем расширение
                        keys.Add(key.Replace(Path.DirectorySeparatorChar, '/'));
                    }
                }
            }, ct);

            return keys.Distinct().ToList();
        }

        private async Task<TemplateData> LoadFromFileAsync(string key, CancellationToken ct)
        {
            var fullPath = ResolvePath(key);
            if (fullPath == null)
                throw new FileNotFoundException($"Template '{key}' not found in '{_basePath}'");

            var fileInfo = new FileInfo(fullPath);
            if (!fileInfo.Exists)
                throw new FileNotFoundException($"Template file '{fullPath}' does not exist");

            // Загружаем с ретраями (для случая когда файл еще записывается)
            Mat mat = null;
            var attempts = 0;
            const int maxAttempts = 3;

            while (attempts < maxAttempts)
            {
                try
                {
                    mat = await Task.Run(() => Cv2.ImRead(fullPath, ImreadModes.Color), ct);
                    if (!mat.Empty())
                        break;

                    mat?.Dispose();
                    mat = null;
                }
                catch
                {
                    mat?.Dispose();
                    mat = null;
                }

                attempts++;
                if (attempts < maxAttempts)
                {
                    await Task.Delay(50 * attempts, ct); // Простой backoff
                }
            }

            if (mat == null || mat.Empty())
            {
                mat?.Dispose();
                throw new InvalidOperationException($"Failed to load image data from '{fullPath}'");
            }

            try
            {
                // Конвертируем Mat в TemplateData
                var templateData = CreateTemplateDataFromMat(key, mat, fileInfo, fullPath);

                // Кэшируем
                _cache.AddOrUpdate(key, templateData, (_, _) => templateData);

                return templateData;
            }
            finally
            {
                mat.Dispose();
            }
        }

        private TemplateData CreateTemplateDataFromMat(string key, Mat mat, FileInfo fileInfo, string sourcePath)
        {
            // Конвертируем Mat в byte array
            var success = Cv2.ImEncode(".png", mat, out var imageBytes);
            if (!success)
                throw new InvalidOperationException($"Failed to encode image data for template '{key}'");

            return new TemplateData
            {
                Data = imageBytes,
                Width = mat.Width,
                Height = mat.Height,
                Channels = mat.Channels(),
                CreatedAt = fileInfo.CreationTimeUtc,
                ModifiedAt = fileInfo.LastWriteTimeUtc,
                Description = $"Template loaded from {sourcePath}",
                Metadata = new Dictionary<string, object> { ["SourcePath"] = sourcePath }
            };
        }

        private Mat CreateMatFromTemplateData(TemplateData template)
        {
            var bytes = template.Data.ToArray();
            var mat = Cv2.ImDecode(bytes, ImreadModes.Color);
            if (mat.Empty())
                throw new InvalidOperationException($"Failed to decode image data for template");
            return mat;
        }

        private string? ResolvePath(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            // Нормализуем ключ (заменяем / на локальный разделитель)
            var normalizedKey = key.Replace('/', Path.DirectorySeparatorChar);

            // Если ключ уже содержит расширение
            if (Path.HasExtension(normalizedKey))
            {
                var candidate = Path.Combine(_basePath, normalizedKey);
                return File.Exists(candidate) ? candidate : null;
            }

            // Ищем файл с любым поддерживаемым расширением
            foreach (var ext in SupportedExtensions)
            {
                var candidate = Path.Combine(_basePath, normalizedKey + ext);
                if (File.Exists(candidate))
                    return candidate;
            }

            return null;
        }

        private string GetSavePath(string key)
        {
            var normalizedKey = key.Replace('/', Path.DirectorySeparatorChar);

            // Если расширение не указано - добавляем .png
            if (!Path.HasExtension(normalizedKey))
                normalizedKey += ".png";

            return Path.Combine(_basePath, normalizedKey);
        }

        #region FileSystemWatcher Events

        private void OnFileChanged(object? sender, FileSystemEventArgs e)
        {
            if (IsImageFile(e.FullPath))
            {
                var key = GetKeyFromPath(e.FullPath);
                if (key != null)
                {
                    _cache.TryRemove(key, out _);
                    OnTemplateChanged(TemplateChangedEventArgs.Modified(key));
                }
            }
        }

        private void OnFileCreated(object? sender, FileSystemEventArgs e)
        {
            if (IsImageFile(e.FullPath))
            {
                var key = GetKeyFromPath(e.FullPath);
                if (key != null)
                {
                    OnTemplateChanged(TemplateChangedEventArgs.Created(key));
                }
            }
        }

        private void OnFileDeleted(object? sender, FileSystemEventArgs e)
        {
            if (IsImageFile(e.FullPath))
            {
                var key = GetKeyFromPath(e.FullPath);
                if (key != null)
                {
                    _cache.TryRemove(key, out _);
                    OnTemplateChanged(TemplateChangedEventArgs.Deleted(key));
                }
            }
        }

        private void OnFileRenamed(object? sender, RenamedEventArgs e)
        {
            var oldKey = IsImageFile(e.OldFullPath) ? GetKeyFromPath(e.OldFullPath) : null;
            var newKey = IsImageFile(e.FullPath) ? GetKeyFromPath(e.FullPath) : null;

            if (oldKey != null)
            {
                _cache.TryRemove(oldKey, out _);
            }

            if (oldKey != null && newKey != null)
            {
                OnTemplateChanged(TemplateChangedEventArgs.Renamed(newKey, oldKey));
            }
            else if (oldKey != null)
            {
                OnTemplateChanged(TemplateChangedEventArgs.Deleted(oldKey));
            }
            else if (newKey != null)
            {
                OnTemplateChanged(TemplateChangedEventArgs.Created(newKey));
            }
        }

        private bool IsImageFile(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            var extension = Path.GetExtension(filePath);
            return SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
        }

        private string? GetKeyFromPath(string filePath)
        {
            try
            {
                if (!filePath.StartsWith(_basePath, StringComparison.OrdinalIgnoreCase))
                    return null;

                var relativePath = Path.GetRelativePath(_basePath, filePath);
                var key = Path.ChangeExtension(relativePath, null);
                return key?.Replace(Path.DirectorySeparatorChar, '/');
            }
            catch
            {
                return null;
            }
        }

        private void OnTemplateChanged(TemplateChangedEventArgs args)
        {
            TemplateChanged?.Invoke(this, args);
        }

        #endregion

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FileTemplateStorage));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                _watcher?.Dispose();
                _loadingSemaphore.Dispose();
                _cache.Clear();
            }
            catch
            {
                // Игнорируем ошибки при освобождении ресурсов
            }

            _disposed = true;
        }
    }
}