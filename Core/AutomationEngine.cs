using AutomationCore.Core.Abstractions;
using AutomationCore.Core.Capture;
using AutomationCore.Core.Matching;
using AutomationCore.Core.Services;
using Microsoft.Extensions.DependencyInjection;
// using AutomationCore.Workflows; // <-- УДАЛИТЕ ЭТУ СТРОКУ, ЕСЛИ ОНА ВСЕ ЕЩЕ ЕСТЬ
using System.Windows.Media;
using AutomationCore.Features.Workflows; // <-- ЭТОТ USING ДОЛЖЕН БЫТЬ
using Microsoft.Extensions.Logging;     // <-- ЭТОТ USING НУЖЕН ДЛЯ ILogger

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

            var match = await Matcher.FindAsync(templateKey, options.MatchOptions);

            if (match == null || match.Score < options.MatchOptions.Threshold)
                return false;

            if (options.ShowOverlay)
            {
                Overlay.HighlightRegion(match.Bounds, Colors.Green, 2000);
            }

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
            // Получаем логгер из DI-контейнера
            var logger = _services.GetRequiredService<ILogger<WorkflowBuilder>>();

            // Эта реализация теперь полностью соответствует исправленному интерфейсу IAutomationEngine
            return new WorkflowBuilder(name, _services, logger);
        }
    }
}