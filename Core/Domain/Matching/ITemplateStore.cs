// Core/Abstractions/ITemplateStore.cs
using System;
using OpenCvSharp;

namespace AutomationCore.Core.Abstractions
{
    /// <summary>
    /// Простой синхронный интерфейс хранилища шаблонов (для обратной совместимости)
    /// </summary>
    public interface ITemplateStore : IDisposable
    {
        /// <summary>Проверяет существование шаблона</summary>
        bool ContainsTemplate(string key);

        /// <summary>Получает шаблон</summary>
        Mat? GetTemplate(string key);

        /// <summary>Сохраняет шаблон</summary>
        void SaveTemplate(string key, Mat template);

        /// <summary>Событие изменения шаблона</summary>
        event EventHandler<string>? TemplateChanged;
    }

    /// <summary>
    /// Простая реализация ITemplateStore через FileTemplateStorage
    /// </summary>
    public sealed class TemplateStorageAdapter : ITemplateStore
    {
        private readonly ITemplateStorage _storage;

        public TemplateStorageAdapter(ITemplateStorage storage)
        {
            _storage = storage;
        }

        public event EventHandler<string>? TemplateChanged;

        public bool ContainsTemplate(string key)
        {
            return _storage.ContainsAsync(key).AsTask().Result;
        }

        public Mat? GetTemplate(string key)
        {
            try
            {
                var template = _storage.LoadAsync(key).AsTask().Result;
                // Конвертируем ReadOnlyMemory<byte> в Mat
                var data = template.Data.ToArray();
                var mat = Mat.FromArray<byte>(data).Reshape(template.Channels, template.Height);
                return mat;
            }
            catch
            {
                return null;
            }
        }

        public void SaveTemplate(string key, Mat template)
        {
            // Для простоты не реализуем сохранение
            throw new NotImplementedException("Save not implemented in adapter");
        }

        public void Dispose()
        {
            _storage?.Dispose();
        }
    }
}