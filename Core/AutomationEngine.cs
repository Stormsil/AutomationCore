using AutomationCore.Core.Abstractions;
using AutomationCore.Core.Capture;
using AutomationCore.Core.Matching;
using AutomationCore.Core.Services;
using Microsoft.Extensions.DependencyInjection; 
using AutomationCore.Workflows;
using AutomationCore.Core.Matching;
using System.Windows.Media;



namespace AutomationCore.Core
{
    /// <summary>
    /// Главный фасад для автоматизации
    /// </summary>
    public class AutomationEngine : IAutomationEngine
    {
        private readonly IServiceProvider _services;

        public ICaptureService Capture { get; }
        public ITemplateMatcherService Matcher { get; }
        public IInputSimulator Input { get; }
        public IWindowService Windows { get; }
        public IOverlayService Overlay { get; }

        public AutomationEngine(IServiceProvider services)
        {
            _services = services;
            Capture = services.GetRequiredService<ICaptureService>();
            Matcher = services.GetRequiredService<ITemplateMatcherService>();
            Input = services.GetRequiredService<IInputSimulator>();
            Windows = services.GetRequiredService<IWindowService>();
            Overlay = services.GetRequiredService<IOverlayService>();
        }

        /// <summary>
        /// Высокоуровневый метод: найти и кликнуть
        /// </summary>
        public async Task<bool> ClickOnImageAsync(
            string templateKey,
            ClickOptions options = null)
        {
            options ??= ClickOptions.Default;

            // Ищем изображение
            var match = await Matcher.FindAsync(templateKey, options.MatchOptions);

            if (match == null || match.Score < options.MatchOptions.Threshold)
                return false;

            // Показываем оверлей если нужно
            if (options.ShowOverlay)
            {
                 Overlay.HighlightRegion(match.Bounds, Colors.Green, 2000);
            }

            // Кликаем
            await Input.Mouse.MoveToAsync(match.Center, options.MouseOptions);
            await Task.Delay(options.DelayBeforeClick);
            await Input.Mouse.ClickAsync(options.Button);

            return true;
        }

        /// <summary>
        /// Создание workflow
        /// </summary>
        public IWorkflowBuilder CreateWorkflow(string name)
        {
            return new WorkflowBuilder(name, this);
        }
    }
}