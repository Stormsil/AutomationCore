// Core/Abstractions/ITemplateMatcher.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutomationCore.Core.Models;

namespace AutomationCore.Core.Abstractions
{
    /// <summary>
    /// Интерфейс для поиска изображений на экране
    /// </summary>
    public interface ITemplateMatcher
    {
        /// <summary>Находит лучшее совпадение для шаблона</summary>
        ValueTask<MatchingResult> FindBestMatchAsync(MatchRequest request, CancellationToken ct = default);

        /// <summary>Находит все совпадения для шаблона</summary>
        ValueTask<MatchingResult> FindAllMatchesAsync(MatchRequest request, CancellationToken ct = default);

        /// <summary>Ждет появления шаблона на экране</summary>
        ValueTask<MatchingResult> WaitForMatchAsync(WaitForMatchRequest request, CancellationToken ct = default);

        /// <summary>Поток совпадений в реальном времени</summary>
        IAsyncEnumerable<MatchingResult> WatchForMatchesAsync(MatchRequest request, CancellationToken ct = default);
    }

    /// <summary>
    /// Хранилище шаблонов
    /// </summary>
    public interface ITemplateStorage : IDisposable
    {
        /// <summary>Проверяет существование шаблона</summary>
        ValueTask<bool> ContainsAsync(string key, CancellationToken ct = default);

        /// <summary>Загружает шаблон</summary>
        ValueTask<TemplateData> LoadAsync(string key, CancellationToken ct = default);

        /// <summary>Сохраняет шаблон</summary>
        ValueTask SaveAsync(string key, TemplateData template, CancellationToken ct = default);

        /// <summary>Удаляет шаблон</summary>
        ValueTask<bool> DeleteAsync(string key, CancellationToken ct = default);

        /// <summary>Получает список всех шаблонов</summary>
        ValueTask<IReadOnlyList<string>> GetKeysAsync(CancellationToken ct = default);

        /// <summary>События изменения шаблонов</summary>
        event EventHandler<TemplateChangedEventArgs>? TemplateChanged;
    }

    /// <summary>
    /// Данные шаблона
    /// </summary>
    public sealed record TemplateData
    {
        public required ReadOnlyMemory<byte> Data { get; init; }
        public required int Width { get; init; }
        public required int Height { get; init; }
        public required int Channels { get; init; }
        public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
        public DateTime ModifiedAt { get; init; } = DateTime.UtcNow;
        public string? Description { get; init; }
        public Dictionary<string, object>? Metadata { get; init; }

        public bool IsEmpty => Data.IsEmpty || Width <= 0 || Height <= 0;
        public int BytesPerPixel => Channels;
        public long TotalBytes => Width * Height * Channels;
    }

    /// <summary>
    /// Событие изменения шаблона
    /// </summary>
    public sealed class TemplateChangedEventArgs : EventArgs
    {
        public required string Key { get; init; }
        public required TemplateChangeType ChangeType { get; init; }
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Типы изменений шаблонов
    /// </summary>
    public enum TemplateChangeType
    {
        Created,
        Modified,
        Deleted
    }

    /// <summary>
    /// Препроцессор изображений
    /// </summary>
    public interface IImagePreprocessor
    {
        /// <summary>Применяет предобработку к изображению</summary>
        ValueTask<ProcessedImage> ProcessAsync(
            ReadOnlyMemory<byte> imageData,
            int width, int height, int channels,
            PreprocessingOptions options,
            CancellationToken ct = default);
    }

    /// <summary>
    /// Обработанное изображение
    /// </summary>
    public sealed record ProcessedImage : IDisposable
    {
        public required ReadOnlyMemory<byte> Data { get; init; }
        public required int Width { get; init; }
        public required int Height { get; init; }
        public required int Channels { get; init; }
        public required PreprocessingOptions AppliedOptions { get; init; }

        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                // Освобождение ресурсов если нужно
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Кэш результатов сопоставления
    /// </summary>
    public interface IMatchCache
    {
        /// <summary>Получает результат из кэша</summary>
        ValueTask<MatchingResult?> GetAsync(string key, CancellationToken ct = default);

        /// <summary>Сохраняет результат в кэш</summary>
        ValueTask SetAsync(string key, MatchingResult result, TimeSpan? ttl = null, CancellationToken ct = default);

        /// <summary>Удаляет результат из кэша</summary>
        ValueTask<bool> RemoveAsync(string key, CancellationToken ct = default);

        /// <summary>Очищает кэш</summary>
        ValueTask ClearAsync(CancellationToken ct = default);
    }
}