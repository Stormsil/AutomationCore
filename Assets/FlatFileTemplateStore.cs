using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;

namespace AutomationCore.Assets
{
    /// <summary>
    /// Простой файловый стор: ключ = имя файла без расширения в папке assets/templates рядом с EXE.
    /// Поиск расширений: .png, .jpg, .jpeg, .bmp.
    /// </summary>
    public sealed class FlatFileTemplateStore : ITemplateStore
    {
        private readonly string _basePath;
        private readonly Dictionary<string, Mat> _cache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly string[] _exts = { ".png", ".jpg", ".jpeg", ".bmp" };

        /// <param name="basePath">
        /// Путь к папке с шаблонами. По умолчанию: &lt;AppContext.BaseDirectory&gt;/assets/templates
        /// </param>
        public FlatFileTemplateStore(string basePath = null)
        {
            _basePath = basePath ?? Path.Combine(AppContext.BaseDirectory, "assets", "templates");
        }

        public bool Contains(string key) => File.Exists(ResolvePath(key));

        public Mat GetTemplate(string key)
        {
            var full = ResolvePath(key);
            if (full == null)
                throw new FileNotFoundException($"Template '{key}' not found in '{_basePath}'");

            if (_cache.TryGetValue(full, out var cached) && !cached.Empty())
                return cached.Clone();

            var mat = Cv2.ImRead(full, ImreadModes.Color);
            if (mat.Empty())
                throw new InvalidOperationException($"Failed to load template '{key}' from '{full}'");

            _cache[full] = mat;
            return mat.Clone();
        }

        private string ResolvePath(string key)
        {
            // key может быть "ok" или "ui/ok" -> ищем ok.png, ok.jpg...
            // Поддержим подпапки в пределах basePath.
            var rel = key.Replace('/', Path.DirectorySeparatorChar)
                         .Replace('\\', Path.DirectorySeparatorChar);

            // Если ключ уже с расширением — используем как есть
            if (Path.HasExtension(rel))
            {
                var fullExact = Path.GetFullPath(Path.Combine(_basePath, rel));
                return File.Exists(fullExact) ? fullExact : null;
            }

            foreach (var ext in _exts)
            {
                var full = Path.GetFullPath(Path.Combine(_basePath, rel + ext));
                if (File.Exists(full))
                    return full;
            }
            return null;
        }

        public void Dispose()
        {
            foreach (var kv in _cache) kv.Value?.Dispose();
            _cache.Clear();
        }
    }
}
