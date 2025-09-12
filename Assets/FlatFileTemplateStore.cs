using OpenCvSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace AutomationCore.Assets
{
    /// <summary>
    /// Файловый стор шаблонов.
    /// Ключ = имя файла без расширения (или с расширением) внутри &lt;base&gt;/assets/templates (по умолчанию).
    /// Поддерживаемые расширения: .png, .jpg, .jpeg, .bmp.
    ///
    /// Особенности:
    /// - Потокобезопасный кэш (по полному пути), возврат Clone() наружу.
    /// - Авто-инвалидция при изменении/удалении файлов через FileSystemWatcher.
    /// - Опциональный LRU-лимит на количество элементов в кэше.
    /// - Ретраи при чтении файла, чтобы не падать на «файле в записи».
    /// </summary>
    public sealed class FlatFileTemplateStore : ITemplateStore
    {
        private readonly string _basePath;
        private readonly ImreadModes _imreadMode;
        private readonly bool _watchForChanges;
        private readonly int _capacity; // 0 или меньше — без ограничения

        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);

        // Для LRU
        private readonly LinkedList<string> _lru = new();
        private readonly object _lruLock = new();

        // File watcher
        private FileSystemWatcher _fsw;

        private static readonly string[] _exts = { ".png", ".jpg", ".jpeg", ".bmp" };

        private sealed class CacheEntry : IDisposable
        {
            public Mat Mat;                       // Хранимый экземпляр (внутренний, не отдаём наружу)
            public DateTime LastWriteUtc;         // Время файла
            public long Length;                   // Размер файла
            public string Path;                   // Полный путь
            public LinkedListNode<string> LruNode; // Узел LRU (для O(1) перемещений)

            public bool IsValid(FileInfo fi) =>
                Mat != null && !Mat.Empty() &&
                fi.Exists &&
                fi.Length == Length &&
                fi.LastWriteTimeUtc == LastWriteUtc;

            public void Dispose()
            {
                try { Mat?.Dispose(); }
                catch { /* ignore */ }
                Mat = null;
                LruNode = null;
            }
        }

        /// <param name="basePath">Базовая папка с шаблонами (по умолчанию: &lt;AppContext.BaseDirectory&gt;/assets/templates)</param>
        /// <param name="imreadMode">Режим чтения изображений. По умолчанию Color (BGR 8UC3).</param>
        /// <param name="watchForChanges">Следить за изменениями на диске и инвалидировать кэш.</param>
        /// <param name="capacity">Ограничить количество элементов в кэше по LRU. 0/отрицательное — без лимита.</param>
        public FlatFileTemplateStore(
            string basePath = null,
            ImreadModes imreadMode = ImreadModes.Color,
            bool watchForChanges = true,
            int capacity = 0)
        {
            _basePath = basePath ?? Path.Combine(AppContext.BaseDirectory, "assets", "templates");
            _imreadMode = imreadMode;
            _watchForChanges = watchForChanges;
            _capacity = capacity;

            try
            {
                Directory.CreateDirectory(_basePath);
            }
            catch
            {
                // Если нет прав — всё равно будем пытаться читать, но watcher не запустится.
            }

            if (_watchForChanges && Directory.Exists(_basePath))
            {
                _fsw = new FileSystemWatcher(_basePath)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.CreationTime,
                    IncludeSubdirectories = true,
                    Filter = "*.*",
                    EnableRaisingEvents = true
                };

                _fsw.Changed += OnFsEvent;
                _fsw.Created += OnFsEvent;
                _fsw.Deleted += OnFsEvent;
                _fsw.Renamed += OnFsRenamed;
            }
        }

        public bool Contains(string key) => ResolvePath(key) != null;

        /// <summary>
        /// Возвращает BGR Mat (8UC3) как Clone() из кэша/диска.
        /// Бросает FileNotFoundException / InvalidOperationException при проблемах загрузки.
        /// </summary>
        public Mat GetTemplate(string key)
        {
            var full = ResolvePath(key);
            if (full == null)
                throw new FileNotFoundException($"Template '{key}' not found in '{_basePath}'");

            var fi = new FileInfo(full);

            // 1) Быстрый путь: есть валидный кэш и файл не изменился
            if (_cache.TryGetValue(full, out var entry) && entry.IsValid(fi))
            {
                TouchLru(entry);
                return entry.Mat.Clone();
            }

            // 2) Загрузка с диска (с ретраями)
            var mat = LoadMatWithRetry(full, _imreadMode);
            if (mat.Empty())
            {
                mat.Dispose();
                throw new InvalidOperationException($"Failed to load template '{key}' from '{full}'");
            }

            // Если требуется — приводим к BGR (на случай, если режим не Color)
            if (_imreadMode == ImreadModes.Unchanged && mat.Channels() == 4)
            {
                // Преобразуем BGRA -> BGR (внутреннее хранение BGR, как ожидает остальной код)
                using var tmp = new Mat();
                Cv2.CvtColor(mat, tmp, ColorConversionCodes.BGRA2BGR);
                mat.Dispose();
                mat = tmp.Clone();
            }
            else if (_imreadMode == ImreadModes.Grayscale && mat.Channels() == 1)
            {
                // Преобразуем Gray -> BGR для единообразия
                using var tmp = new Mat();
                Cv2.CvtColor(mat, tmp, ColorConversionCodes.GRAY2BGR);
                mat.Dispose();
                mat = tmp.Clone();
            }

            var newEntry = new CacheEntry
            {
                Mat = mat,
                LastWriteUtc = fi.Exists ? fi.LastWriteTimeUtc : DateTime.MinValue,
                Length = fi.Exists ? fi.Length : 0,
                Path = full
            };

            // 3) Обновляем кэш/LRU
            _cache.AddOrUpdate(full,
                addValueFactory: _ =>
                {
                    AddToLru(newEntry);
                    EnforceCapacityIfNeeded();
                    return newEntry;
                },
                updateValueFactory: (_, old) =>
                {
                    // Заменяем мат и метаданные, старый освобождаем
                    RemoveFromLru(old);
                    old.Dispose();

                    AddToLru(newEntry);
                    EnforceCapacityIfNeeded();
                    return newEntry;
                });

            return newEntry.Mat.Clone();
        }

        // ---------- Вспомогательные ----------

        private string ResolvePath(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            // Нормализуем относительный путь
            var rel = key.Replace('/', Path.DirectorySeparatorChar)
                         .Replace('\\', Path.DirectorySeparatorChar)
                         .TrimStart(Path.DirectorySeparatorChar);

            // Не позволяем выход за пределы базовой директории
            string CombineSafe(string baseDir, string relative)
            {
                var full = Path.GetFullPath(Path.Combine(baseDir, relative));
                return full.StartsWith(Path.GetFullPath(baseDir), StringComparison.OrdinalIgnoreCase)
                    ? full
                    : null;
            }

            // Если ключ уже с расширением — используем как есть
            if (Path.HasExtension(rel))
            {
                var withExt = CombineSafe(_basePath, rel);
                return (withExt != null && File.Exists(withExt)) ? withExt : null;
            }

            // Перебираем известные расширения
            foreach (var ext in _exts)
            {
                var candidate = CombineSafe(_basePath, rel + ext);
                if (candidate != null && File.Exists(candidate))
                    return candidate;
            }

            return null;
        }

        private static Mat LoadMatWithRetry(string path, ImreadModes mode, int maxAttempts = 3, int initialDelayMs = 15)
        {
            for (int i = 1; i <= maxAttempts; i++)
            {
                try
                {
                    var mat = Cv2.ImRead(path, mode);
                    if (!mat.Empty())
                        return mat;

                    // Пусто — возможно, файл ещё пишется
                    mat.Dispose();
                }
                catch
                {
                    // игнорируем и подождём
                }

                Thread.Sleep(initialDelayMs * i); // простой backoff
            }

            // Последняя попытка без try/catch для явной ошибки
            return Cv2.ImRead(path, mode);
        }

        private void AddToLru(CacheEntry entry)
        {
            if (_capacity <= 0) return;

            lock (_lruLock)
            {
                entry.LruNode = _lru.AddFirst(entry.Path);
            }
        }

        private void TouchLru(CacheEntry entry)
        {
            if (_capacity <= 0 || entry?.LruNode == null) return;

            lock (_lruLock)
            {
                // Переносим в голову
                if (entry.LruNode.List != null) // может быть уже удалён
                {
                    _lru.Remove(entry.LruNode);
                    _lru.AddFirst(entry.LruNode);
                }
            }
        }

        private void RemoveFromLru(CacheEntry entry)
        {
            if (_capacity <= 0 || entry?.LruNode == null) return;

            lock (_lruLock)
            {
                if (entry.LruNode.List != null)
                    _lru.Remove(entry.LruNode);
                entry.LruNode = null;
            }
        }

        private void EnforceCapacityIfNeeded()
        {
            if (_capacity <= 0) return;

            List<string> toEvict = null;

            lock (_lruLock)
            {
                while (_lru.Count > _capacity)
                {
                    var last = _lru.Last;
                    if (last == null) break;

                    toEvict ??= new List<string>();
                    toEvict.Add(last.Value);
                    _lru.RemoveLast();
                }
            }

            if (toEvict == null) return;

            foreach (var path in toEvict)
            {
                if (_cache.TryRemove(path, out var removed))
                {
                    removed.Dispose();
                }
            }
        }

        // ---------- FileSystemWatcher события ----------

        private void OnFsEvent(object sender, FileSystemEventArgs e)
        {
            if (string.IsNullOrEmpty(e.FullPath)) return;

            // Инвалидируем запись по этому пути (если есть). Следующая выдача обновит с диска.
            if (_cache.TryRemove(e.FullPath, out var removed))
            {
                RemoveFromLru(removed);
                removed.Dispose();
            }
        }

        private void OnFsRenamed(object sender, RenamedEventArgs e)
        {
            // Удаляем старый ключ
            if (!string.IsNullOrEmpty(e.OldFullPath) && _cache.TryRemove(e.OldFullPath, out var removed))
            {
                RemoveFromLru(removed);
                removed.Dispose();
            }

            // Новый путь пока не знаем, понадобится при следующем запросе
            // (если уже есть кэш по новому полному пути — он сам обновится через Changed/Created)
        }

        // ---------- IDispose ----------

        public void Dispose()
        {
            try
            {
                if (_fsw != null)
                {
                    _fsw.Changed -= OnFsEvent;
                    _fsw.Created -= OnFsEvent;
                    _fsw.Deleted -= OnFsEvent;
                    _fsw.Renamed -= OnFsRenamed;
                    _fsw.EnableRaisingEvents = false;
                    _fsw.Dispose();
                    _fsw = null;
                }
            }
            catch { /* ignore */ }

            foreach (var kv in _cache)
            {
                kv.Value.Dispose();
            }
            _cache.Clear();

            lock (_lruLock)
            {
                _lru.Clear();
            }
        }
    }
}
