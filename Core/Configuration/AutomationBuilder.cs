using AutomationCore.Assets;
using AutomationCore.Core.Abstractions;
using AutomationCore.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using AutomationCore.Core.Capture;
using AutomationCore.Input;
using AutomationCore.Core.Matching;
using Microsoft.Extensions.Logging;



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

            _services.AddSingleton<IWindowService, WindowService>();
            _services.AddSingleton<IPInvokeWrapper, PInvokeWrapper>();
            _services.AddSingleton<ICaptureFactory, WgcCaptureFactory>();

            // базовые зависимости для матчера (минимальные рабочие)
            _services.AddSingleton<IPreprocessor, BasicPreprocessor>();
            _services.AddSingleton<IMatchingEngine, BasicMatchingEngine>();
            _services.AddSingleton<IMatchCache>(sp => new MemoryMatchCache(new Core.Matching.CacheOptions()));
            _services.AddSingleton<ITemplateMatcherService, TemplateMatcherService>();
            _services.AddSingleton<IOverlayService, OverlayService>();
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

            _services.AddSingleton<IInputSimulator>(sp => new HumanizedInputSimulator(options));
            return this;
        }


        public AutomationBuilder WithCaching(Action<CacheOptions> configure = null)
        {
            var options = new CacheOptions();
            configure?.Invoke(options);

            _services.AddSingleton<IMatchCache>(sp => new MemoryMatchCache(options));


            return this;
        }

        public IAutomationEngine Build()
        {
            var provider = _services.BuildServiceProvider();
            return new AutomationEngine(provider);
        }
    }
}