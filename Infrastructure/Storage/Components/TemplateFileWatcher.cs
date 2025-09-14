// Template File Watcher - отвечает только за мониторинг изменений файлов
using System;
using System.IO;
using AutomationCore.Core.Abstractions;
using AbstractionsTemplateChangedEventArgs = AutomationCore.Core.Abstractions.TemplateChangedEventArgs;
using AbstractionsTemplateChangeType = AutomationCore.Core.Abstractions.TemplateChangeType;

namespace AutomationCore.Infrastructure.Storage.Components
{
    /// <summary>
    /// Компонент для отслеживания изменений файлов шаблонов
    /// </summary>
    internal sealed class TemplateFileWatcher : IDisposable
    {
        private readonly FileSystemWatcher _watcher;
        private readonly string _basePath;
        private bool _disposed;

        public event EventHandler<AbstractionsTemplateChangedEventArgs>? TemplateChanged;

        public TemplateFileWatcher(string basePath)
        {
            _basePath = basePath ?? throw new ArgumentNullException(nameof(basePath));

            try
            {
                _watcher = new FileSystemWatcher(_basePath)
                {
                    Filter = "*.*", // Будем фильтровать по расширениям в событии
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                    EnableRaisingEvents = false // Включаем позже
                };

                _watcher.Created += OnFileChanged;
                _watcher.Changed += OnFileChanged;
                _watcher.Deleted += OnFileChanged;
                _watcher.Renamed += OnFileRenamed;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create FileSystemWatcher for path: {_basePath}", ex);
            }
        }

        /// <summary>
        /// Запускает мониторинг файлов
        /// </summary>
        public void StartWatching()
        {
            if (_disposed)
                return;

            try
            {
                _watcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to start file watching: {ex.Message}");
            }
        }

        /// <summary>
        /// Останавливает мониторинг файлов
        /// </summary>
        public void StopWatching()
        {
            if (_disposed)
                return;

            try
            {
                _watcher.EnableRaisingEvents = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to stop file watching: {ex.Message}");
            }
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            if (_disposed || !IsTemplateFile(e.FullPath))
                return;

            try
            {
                var key = Path.GetFileNameWithoutExtension(e.Name);
                var changeType = e.ChangeType switch
                {
                    WatcherChangeTypes.Created => AbstractionsTemplateChangeType.Created,
                    WatcherChangeTypes.Changed => AbstractionsTemplateChangeType.Modified,
                    WatcherChangeTypes.Deleted => AbstractionsTemplateChangeType.Deleted,
                    _ => AbstractionsTemplateChangeType.Modified
                };

                var args = changeType switch
                {
                    AbstractionsTemplateChangeType.Created => AbstractionsTemplateChangedEventArgs.Created(key),
                    AbstractionsTemplateChangeType.Modified => AbstractionsTemplateChangedEventArgs.Modified(key),
                    AbstractionsTemplateChangeType.Deleted => AbstractionsTemplateChangedEventArgs.Deleted(key),
                    _ => AbstractionsTemplateChangedEventArgs.Modified(key)
                };

                TemplateChanged?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling file change event: {ex.Message}");
            }
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            if (_disposed)
                return;

            try
            {
                // Обрабатываем как удаление старого и добавление нового
                if (IsTemplateFile(e.OldFullPath))
                {
                    var oldKey = Path.GetFileNameWithoutExtension(e.OldName);
                    var oldArgs = AbstractionsTemplateChangedEventArgs.Deleted(oldKey);
                    TemplateChanged?.Invoke(this, oldArgs);
                }

                if (IsTemplateFile(e.FullPath))
                {
                    var newKey = Path.GetFileNameWithoutExtension(e.Name);
                    var newArgs = AbstractionsTemplateChangedEventArgs.Created(newKey);
                    TemplateChanged?.Invoke(this, newArgs);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling file rename event: {ex.Message}");
            }
        }

        private static bool IsTemplateFile(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension is ".png" or ".jpg" or ".jpeg" or ".bmp";
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            StopWatching();

            if (_watcher != null)
            {
                _watcher.Created -= OnFileChanged;
                _watcher.Changed -= OnFileChanged;
                _watcher.Deleted -= OnFileChanged;
                _watcher.Renamed -= OnFileRenamed;
                _watcher.Dispose();
            }

            _disposed = true;
        }
    }
}