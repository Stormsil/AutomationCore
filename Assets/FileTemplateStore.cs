// AutomationCore/Assets/FileTemplateStore.cs
using System;

using OpenCvSharp;
using Microsoft.Extensions.Logging;


namespace AutomationCore.Assets
{
    public class TemplateStoreOptions
    {
        public string BasePath { get; set; }
        public ImreadModes ImreadMode { get; set; } = ImreadModes.Color;
        public bool WatchForChanges { get; set; } = true;
        public int Capacity { get; set; } = 0;
    }

    /// <summary>
    /// Обёртка, ожидаемая билдерами, поверх FlatFileTemplateStore.
    /// </summary>
    public sealed class FileTemplateStore : ITemplateStore
    {
        private readonly FlatFileTemplateStore _inner;

        public FileTemplateStore(TemplateStoreOptions options, ILogger<FileTemplateStore> _)
        {
            options ??= new TemplateStoreOptions();
            _inner = new FlatFileTemplateStore(
                basePath: options.BasePath,
                imreadMode: options.ImreadMode,
                watchForChanges: options.WatchForChanges,
                capacity: options.Capacity);
        }

        public bool Contains(string key) => _inner.Contains(key);
        public OpenCvSharp.Mat GetTemplate(string key) => _inner.GetTemplate(key);
        public void Dispose() => _inner.Dispose();
    }
}
