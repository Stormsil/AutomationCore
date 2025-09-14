// File Template Storage - композитный класс, использующий специализированные компоненты
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutomationCore.Core.Abstractions;
using AutomationCore.Infrastructure.Storage.Components;
using AbstractionsTemplateData = AutomationCore.Core.Abstractions.TemplateData;
using AbstractionsTemplateChangedEventArgs = AutomationCore.Core.Abstractions.TemplateChangedEventArgs;
using ModelsImageFormat = AutomationCore.Core.Models.ImageFormat;

namespace AutomationCore.Infrastructure.Storage
{
    /// <summary>
    /// Рефакторенное файловое хранилище шаблонов с разделением на компоненты
    /// </summary>
    public sealed class FileTemplateStorage : ITemplateStorage
    {
        private readonly string _basePath;
        private readonly TemplateFileLoader _fileLoader;
        private readonly TemplateCache _cache;
        private readonly TemplateFileWatcher? _watcher;
        private readonly SemaphoreSlim _loadingSemaphore = new(10);
        private bool _disposed;

        public event EventHandler<AbstractionsTemplateChangedEventArgs>? TemplateChanged;

        /// <summary>
        /// Создает новое файловое хранилище шаблонов
        /// </summary>
        public FileTemplateStorage(string basePath, bool watchForChanges = true, TimeSpan? cacheTtl = null)
        {
            _basePath = basePath ?? throw new ArgumentNullException(nameof(basePath));

            // Создаем директорию если не существует
            Directory.CreateDirectory(_basePath);

            // Инициализируем компоненты
            _fileLoader = new TemplateFileLoader();
            _cache = new TemplateCache(cacheTtl);

            // Настраиваем file watcher если нужно
            if (watchForChanges)
            {
                try
                {
                    _watcher = new TemplateFileWatcher(_basePath);
                    _watcher.TemplateChanged += OnTemplateFileChanged;
                    _watcher.StartWatching();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to setup file watcher: {ex.Message}");
                    // Продолжаем работу без file watcher
                }
            }
        }

        public async ValueTask<bool> ContainsAsync(string key, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;

            if (_disposed)
                return false;

            // Проверяем кэш
            if (_cache.Contains(key))
                return true;

            // Проверяем файловую систему
            return _fileLoader.FileExists(_basePath, key);
        }

        public async ValueTask<AbstractionsTemplateData> LoadAsync(string key, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Template key cannot be null or empty", nameof(key));

            if (_disposed)
                throw new ObjectDisposedException(nameof(FileTemplateStorage));

            // Сначала пробуем кэш
            var cached = _cache.Get(key);
            if (cached != null)
                return cached;

            // Загружаем из файла
            await _loadingSemaphore.WaitAsync(ct);
            try
            {
                // Двойная проверка после получения семафора
                cached = _cache.Get(key);
                if (cached != null)
                    return cached;

                var filePath = _fileLoader.FindTemplateFile(_basePath, key);
                if (filePath == null)
                    throw new FileNotFoundException($"Template '{key}' not found in {_basePath}");

                var templateData = await _fileLoader.LoadFromFileAsync(filePath, ct);

                // Сохраняем в кэш
                _cache.Set(key, templateData);

                return templateData;
            }
            finally
            {
                _loadingSemaphore.Release();
            }
        }

        public async ValueTask SaveAsync(string key, AbstractionsTemplateData template, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Template key cannot be null or empty", nameof(key));

            if (template.Data.IsEmpty)
                throw new ArgumentException("Template data cannot be null", nameof(template));

            if (_disposed)
                throw new ObjectDisposedException(nameof(FileTemplateStorage));

            // Default to PNG format since AbstractionsTemplateData doesn't have Format property
            var extension = ".png";

            var filePath = Path.Combine(_basePath, key + extension);

            try
            {
                await File.WriteAllBytesAsync(filePath, template.Data.ToArray(), ct);

                // Обновляем кэш
                var updatedTemplate = template with
                {
                    ModifiedAt = DateTime.UtcNow
                };

                _cache.Set(key, updatedTemplate);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to save template '{key}' to {filePath}: {ex.Message}", ex);
            }
        }

        public async ValueTask<bool> DeleteAsync(string key, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;

            if (_disposed)
                return false;

            var filePath = _fileLoader.FindTemplateFile(_basePath, key);
            if (filePath == null)
                return false;

            try
            {
                File.Delete(filePath);
                _cache.Remove(key);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async ValueTask<IReadOnlyList<string>> GetKeysAsync(CancellationToken ct = default)
        {
            if (_disposed)
                return Array.Empty<string>();

            var files = _fileLoader.GetAllTemplateFiles(_basePath);
            var keys = files.Select(Path.GetFileNameWithoutExtension)
                           .Where(key => !string.IsNullOrEmpty(key))
                           .ToList();

            return keys.AsReadOnly();
        }

        /// <summary>
        /// Получает статистику кэша
        /// </summary>
        public CacheStats GetCacheStats()
        {
            return _cache?.GetStats() ?? new CacheStats();
        }

        /// <summary>
        /// Очищает кэш шаблонов
        /// </summary>
        public void ClearCache()
        {
            _cache?.Clear();
        }

        private void OnTemplateFileChanged(object? sender, AbstractionsTemplateChangedEventArgs e)
        {
            if (_disposed)
                return;

            // Инвалидируем кэш
            _cache.Invalidate(e.Key);

            // Передаем событие наверх
            TemplateChanged?.Invoke(this, e);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _watcher?.Dispose();
            _cache?.Dispose();
            _loadingSemaphore?.Dispose();

            _disposed = true;
        }
    }
}