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
            if (context is WorkflowBuilder.WorkflowContext workflowContext)
            {
                return workflowContext.GetService<T>();
            }

            throw new InvalidOperationException("Context does not support service resolution");
        }
    }
}