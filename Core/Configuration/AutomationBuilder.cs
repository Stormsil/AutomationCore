using AutomationCore.Assets;
using AutomationCore.Core.Abstractions;
using AutomationCore.Core.Services;

namespace AutomationCore.Core.Configuration
{
    /// <summary>
    /// Builder для конфигурации автоматизации
    /// </summary>
    public class AutomationBuilder
    {
        private readonly IServiceCollection _services;

        public AutomationBuilder(IServiceCollection services)
        {
            _services = services;

            // Регистрируем базовые сервисы
            _services.AddSingleton<IWindowService, WindowService>();
            _services.AddSingleton<IPInvokeWrapper, PInvokeWrapper>();
            _services.AddSingleton<ICaptureFactory, WgcCaptureFactory>();
        }

        public AutomationBuilder WithTemplateStore(Action<TemplateStoreOptions> configure)
        {
            var options = new TemplateStoreOptions();
            configure(options);

            _services.AddSingleton<ITemplateStore>(sp =>
                new FileTemplateStore(options, sp.GetService<ILogger<FileTemplateStore>>()));

            return this;
        }

        public AutomationBuilder WithInputSimulation(Action<InputOptions> configure = null)
        {
            var options = new InputOptions();
            configure?.Invoke(options);

            _services.Configure<InputOptions>(o =>
            {
                o.MouseSpeed = options.MouseSpeed;
                o.TypingSpeed = options.TypingSpeed;
                o.EnableHumanization = options.EnableHumanization;
            });

            _services.AddSingleton<IInputSimulator, HumanizedInputSimulator>();

            return this;
        }

        public AutomationBuilder WithCaching(Action<CacheOptions> configure = null)
        {
            var options = new CacheOptions();
            configure?.Invoke(options);

            _services.AddSingleton<IMatchCache>(sp =>
                new MemoryMatchCache(options, sp.GetService<ILogger<MemoryMatchCache>>()));

            return this;
        }

        public IAutomationEngine Build()
        {
            var provider = _services.BuildServiceProvider();
            return new AutomationEngine(provider);
        }
    }
}