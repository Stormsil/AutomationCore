// AutomationCore/Core/IAutomationEngine.cs
using AutomationCore.Core.Abstractions;
using AutomationCore.Infrastructure.Input;
using AutomationCore.Workflows;
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
        IWorkflowBuilder CreateWorkflow(string name);
    }
}
