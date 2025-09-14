// AutomationCore/Core/IAutomationEngine.cs
using AutomationCore.Core.Abstractions;
using AutomationCore.Infrastructure.Input;
// using AutomationCore.Workflows; // <-- УДАЛИТЕ ЭТУ СТРОКУ, ЕСЛИ ОНА ВСЕ ЕЩЕ ЕСТЬ
using AutomationCore.Features.Workflows; // <-- ДОБАВЬТЕ ИЛИ УБЕДИТЕСЬ, ЧТО ЭТА СТРОКА ЕСТЬ
using System.Threading.Tasks;

namespace AutomationCore.Core
{
    /// <summary>Контракт движка автоматизации (минимум, нужный workflow'у).</summary>
    public interface IAutomationEngine
    {
        IInputSimulator Input { get; }

        /// <summary>Найти изображение и кликнуть по нему.</summary>
        Task<bool> ClickOnImageAsync(string templateKey, ClickOptions options = null);

        /// <summary>Создать fluent-workflow билдер.</summary>
        /// <remarks>
        /// Эта ссылка теперь однозначно указывает на полнофункциональный интерфейс
        /// из пространства имен AutomationCore.Features.Workflows.
        /// </remarks>
        IWorkflowBuilder CreateWorkflow(string name);
    }
}