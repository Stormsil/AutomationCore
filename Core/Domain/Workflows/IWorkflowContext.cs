// Core/Domain/Workflows/IWorkflowContext.cs
using System;
using System.Collections.Generic;
using System.Threading;
using AutomationCore.Core.Models;

namespace AutomationCore.Core.Abstractions
{
    /// <summary>
    /// Контекст выполнения workflow
    /// </summary>
    public interface IWorkflowContext
    {
        /// <summary>Токен отмены для workflow</summary>
        CancellationToken CancellationToken { get; }

        /// <summary>Переменные workflow</summary>
        IDictionary<string, object> Variables { get; }

        /// <summary>Результат последнего шага</summary>
        object? LastStepResult { get; set; }

        /// <summary>Состояние выполнения</summary>
        WorkflowExecutionState State { get; set; }

        /// <summary>Логирование</summary>
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message, Exception? ex = null);

        /// <summary>Установить переменную</summary>
        void SetVariable(string key, object value);

        /// <summary>Получить переменную</summary>
        T? GetVariable<T>(string key);

        /// <summary>Проверить наличие переменной</summary>
        bool HasVariable(string key);
    }

    /// <summary>
    /// Состояние выполнения workflow
    /// </summary>
    public enum WorkflowExecutionState
    {
        NotStarted,
        Running,
        Completed,
        Failed,
        Cancelled
    }
}