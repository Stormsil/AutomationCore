using AutomationCore.Core.Abstractions;
using AutomationCore.Core.Services;
using AutomationCore.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using AutomationCore.Core.Capture;
using AutomationCore.Infrastructure.Input;
using AutomationCore.Infrastructure.Storage;
using AutomationCore.Core.Domain.Matching;
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
            _services.AddSingleton<IMatchCache>(sp => new MemoryMatchCache(new Core.Matching.MatchCacheOptions()));
            _services.AddSingleton<ITemplateMatcherService, TemplateMatcherService>();
            _services.AddSingleton<IOverlayService, OverlayService>();
            _services.AddSingleton<WindowImageSearch>();
        }


        public AutomationBuilder WithTemplateStore(Action<TemplateStoreOptions> configure)
        {
            var options = new TemplateStoreOptions();
            configure(options);

            _services.AddSingleton<ITemplateStorage>(sp =>
                new FileTemplateStorage(options.BasePath));

            return this;
        }

        public AutomationBuilder WithInputSimulation(Action<InputOptions> configure = null)
        {
            var options = new InputOptions();
            configure?.Invoke(options);

            _services.AddSingleton<IInputSimulator, WindowsInputProvider>();
            return this;
        }


        public AutomationBuilder WithCaching(Action<AutomationCore.Core.Matching.MatchCacheOptions> configure = null)
        {
            var options = new AutomationCore.Core.Matching.MatchCacheOptions();
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