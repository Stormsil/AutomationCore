// Workflow Context Extensions
using System;
using Microsoft.Extensions.DependencyInjection;
using AutomationCore.Core.Abstractions;

namespace AutomationCore.Features.Workflows
{
    /// <summary>
    /// Расширения для IWorkflowContext
    /// </summary>
    public static class WorkflowContextExtensions
    {
        /// <summary>
        /// Получает сервис из контейнера зависимостей через контекст
        /// </summary>
        public static T GetService<T>(this IWorkflowContext context) where T : notnull
        {
            // Temporary workaround - needs proper DI integration
            throw new NotImplementedException("Service resolution through workflow context needs proper implementation");
        }
    }
}