// Features/Workflows/WorkflowBuilder.cs
using AutomationCore.Core.Abstractions;
using AutomationCore.Core.Models;
using AutomationCore.Features.ImageSearch;
using AutomationCore.Features.WindowAutomation;
using AutomationCore.Features.Workflows.Steps.Basic;
using AutomationCore.Features.Workflows.Steps.Input;
using AutomationCore.Features.Workflows.Steps.Control;
using AutomationCore.Features.Workflows.Steps.Custom;
using AutomationCore.Features.Workflows.Steps.Window;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AutomationCore.Features.Workflows
{
    /// <summary>
    /// Fluent API для создания сложных workflow автоматизации
    /// </summary>
    public sealed class WorkflowBuilder : IWorkflowBuilder
    {
        private readonly string _name;
        private readonly IServiceProvider _services;
        private readonly ILogger<WorkflowBuilder> _logger;
        private readonly List<AutomationCore.Features.Workflows.IWorkflowStep> _steps = new();

        public WorkflowBuilder(string name, IServiceProvider services, ILogger<WorkflowBuilder> logger)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Создает новый workflow
        /// </summary>
        public IWorkflowBuilder CreateNew(string name)
        {
            return new WorkflowBuilder(name, _services, _logger);
        }

        #region Basic Steps

        /// <summary>
        /// Ждет появления изображения на экране
        /// </summary>
        public IWorkflowBuilder WaitForImage(string templateKey, TimeSpan? timeout = null)
        {
            var step = new AutomationCore.Features.Workflows.Steps.Basic.WaitForImageStep(templateKey, timeout ?? TimeSpan.FromSeconds(10));
            _steps.Add(step);
            return this;
        }

        /// <summary>
        /// Кликает по изображению на экране
        /// </summary>
        public IWorkflowBuilder ClickOnImage(string templateKey)
        {
            var step = new AutomationCore.Features.Workflows.Steps.Input.ClickOnImageStep(templateKey);
            _steps.Add(step);
            return this;
        }

        /// <summary>
        /// Печатает текст
        /// </summary>
        public IWorkflowBuilder Type(string text)
        {
            var step = new AutomationCore.Features.Workflows.Steps.Input.TypeTextStep(text);
            _steps.Add(step);
            return this;
        }

        /// <summary>
        /// Добавляет задержку
        /// </summary>
        public IWorkflowBuilder Delay(TimeSpan delay)
        {
            var step = new AutomationCore.Features.Workflows.Steps.Basic.DelayStep(delay);
            _steps.Add(step);
            return this;
        }

        /// <summary>
        /// Нажимает комбинацию клавиш
        /// </summary>
        public IWorkflowBuilder PressKeys(params VirtualKey[] keys)
        {
            var step = new AutomationCore.Features.Workflows.Steps.Input.KeyCombinationStep(keys);
            _steps.Add(step);
            return this;
        }

        #endregion

        #region Window Steps

        /// <summary>
        /// Активирует окно
        /// </summary>
        public IWorkflowBuilder ActivateWindow(string windowTitle)
        {
            var step = new AutomationCore.Features.Workflows.Steps.Window.ActivateWindowStep(windowTitle);
            _steps.Add(step);
            return this;
        }

        /// <summary>
        /// Кликает по изображению в конкретном окне
        /// </summary>
        public IWorkflowBuilder ClickOnImageInWindow(string windowTitle, string templateKey)
        {
            var step = new AutomationCore.Features.Workflows.Steps.Window.ClickOnImageInWindowStep(windowTitle, templateKey);
            _steps.Add(step);
            return this;
        }

        /// <summary>
        /// Печатает текст в конкретном окне
        /// </summary>
        public IWorkflowBuilder TypeInWindow(string windowTitle, string text)
        {
            var step = new AutomationCore.Features.Workflows.Steps.Window.TypeInWindowStep(windowTitle, text);
            _steps.Add(step);
            return this;
        }

        #endregion

        #region Control Flow Steps

        /// <summary>
        /// Условное выполнение шагов
        /// </summary>
        public IWorkflowBuilder If(Func<IWorkflowContext, ValueTask<bool>> condition, Action<IWorkflowBuilder> thenSteps)
        {
            var conditionalStep = new AutomationCore.Features.Workflows.Steps.Control.ConditionalStep(condition);
            var nestedBuilder = new WorkflowBuilder($"{_name}_If", _services, _logger);
            thenSteps(nestedBuilder);
            conditionalStep.ThenSteps.AddRange(nestedBuilder._steps);
            _steps.Add(conditionalStep);
            return this;
        }

        /// <summary>
        /// Повторяет шаги с количеством попыток
        /// </summary>
        public IWorkflowBuilder Retry(int maxAttempts, Action<IWorkflowBuilder> steps)
        {
            var retryStep = new AutomationCore.Features.Workflows.Steps.Control.RetryStep(maxAttempts);
            var nestedBuilder = new WorkflowBuilder($"{_name}_Retry", _services, _logger);
            steps(nestedBuilder);
            retryStep.Steps.AddRange(nestedBuilder._steps);
            _steps.Add(retryStep);
            return this;
        }

        /// <summary>
        /// Выполняет шаги параллельно
        /// </summary>
        public IWorkflowBuilder Parallel(Action<IWorkflowBuilder> steps)
        {
            var parallelStep = new AutomationCore.Features.Workflows.Steps.Control.ParallelStep();
            var nestedBuilder = new WorkflowBuilder($"{_name}_Parallel", _services, _logger);
            steps(nestedBuilder);
            parallelStep.Steps.AddRange(nestedBuilder._steps);
            _steps.Add(parallelStep);
            return this;
        }

        /// <summary>
        /// Повторяет шаги пока условие истинно
        /// </summary>
        public IWorkflowBuilder While(Func<IWorkflowContext, ValueTask<bool>> condition, Action<IWorkflowBuilder> steps)
        {
            var whileStep = new AutomationCore.Features.Workflows.Steps.Control.WhileStep(condition);
            var nestedBuilder = new WorkflowBuilder($"{_name}_While", _services, _logger);
            steps(nestedBuilder);
            whileStep.Steps.AddRange(nestedBuilder._steps);
            _steps.Add(whileStep);
            return this;
        }

        #endregion

        #region Custom Steps

        /// <summary>
        /// Добавляет кастомный шаг
        /// </summary>
        public IWorkflowBuilder AddCustomStep(string name, Func<IWorkflowContext, ValueTask> action)
        {
            var step = new AutomationCore.Features.Workflows.Steps.Custom.CustomActionStep(action, name);
            _steps.Add(step);
            return this;
        }

        /// <summary>
        /// Добавляет шаг с возвратом значения
        /// </summary>
        public IWorkflowBuilder AddCustomStep<T>(string name, Func<IWorkflowContext, ValueTask<T>> action, string? storeKey = null)
        {
            var step = new AutomationCore.Features.Workflows.Steps.Custom.CustomActionStep<T>(action, name);
            _steps.Add(step);
            return this;
        }

        #endregion

        #region Execution

        /// <summary>
        /// Выполняет workflow
        /// </summary>
        public async ValueTask<WorkflowResult> ExecuteAsync(CancellationToken ct = default)
        {
            _logger.LogInformation("Starting execution of workflow '{WorkflowName}' with {StepCount} steps", _name, _steps.Count);

            var result = new WorkflowResult { Name = _name };
            var context = new WorkflowContext(_services);
            var startTime = DateTime.UtcNow;

            try
            {
                for (int i = 0; i < _steps.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    var step = _steps[i];
                    _logger.LogDebug("Executing step {StepIndex}/{TotalSteps}: {StepName}", i + 1, _steps.Count, step.Name);

                    var stepStartTime = DateTime.UtcNow;

                    try
                    {
                        await step.ExecuteAsync(context, ct);

                        var stepDuration = DateTime.UtcNow - stepStartTime;
                        result.CompletedSteps.Add(new StepResult
                        {
                            Name = step.Name,
                            Index = i,
                            Duration = stepDuration,
                            Success = true
                        });

                        _logger.LogTrace("Step '{StepName}' completed in {Duration}ms", step.Name, stepDuration.TotalMilliseconds);
                    }
                    catch (Exception stepEx)
                    {
                        var stepDuration = DateTime.UtcNow - stepStartTime;
                        result.CompletedSteps.Add(new StepResult
                        {
                            Name = step.Name,
                            Index = i,
                            Duration = stepDuration,
                            Success = false,
                            Error = stepEx
                        });

                        _logger.LogError(stepEx, "Step '{StepName}' failed after {Duration}ms", step.Name, stepDuration.TotalMilliseconds);

                        result.Success = false;
                        result.Error = stepEx;
                        result.FailedStep = step.Name;
                        break;
                    }
                }

                result.Duration = DateTime.UtcNow - startTime;

                if (result.Success)
                {
                    _logger.LogInformation("Workflow '{WorkflowName}' completed successfully in {Duration}ms",
                        _name, result.Duration.TotalMilliseconds);
                }
                else
                {
                    _logger.LogWarning("Workflow '{WorkflowName}' failed at step '{FailedStep}' after {Duration}ms",
                        _name, result.FailedStep, result.Duration.TotalMilliseconds);
                }

                return result;
            }
            catch (OperationCanceledException)
            {
                result.Duration = DateTime.UtcNow - startTime;
                result.Success = false;
                result.Error = new OperationCanceledException("Workflow was cancelled");

                _logger.LogInformation("Workflow '{WorkflowName}' was cancelled after {Duration}ms",
                    _name, result.Duration.TotalMilliseconds);

                return result;
            }
            catch (Exception ex)
            {
                result.Duration = DateTime.UtcNow - startTime;
                result.Success = false;
                result.Error = ex;

                _logger.LogError(ex, "Workflow '{WorkflowName}' failed with unexpected error after {Duration}ms",
                    _name, result.Duration.TotalMilliseconds);

                return result;
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Создает быстрый workflow для поиска и клика
        /// </summary>
        public static IWorkflowBuilder QuickClick(string name, string templateKey, IServiceProvider services, ILogger<WorkflowBuilder> logger)
        {
            return new WorkflowBuilder(name, services, logger)
                .WaitForImage(templateKey, TimeSpan.FromSeconds(5))
                .ClickOnImage(templateKey);
        }

        /// <summary>
        /// Создает workflow для заполнения формы
        /// </summary>
        public static IWorkflowBuilder FillForm(string name, string windowTitle, Dictionary<string, string> fields, IServiceProvider services, ILogger<WorkflowBuilder> logger)
        {
            var builder = new WorkflowBuilder(name, services, logger)
                .ActivateWindow(windowTitle);

            foreach (var field in fields)
            {
                builder
                    .ClickOnImageInWindow(windowTitle, field.Key)
                    .Delay(TimeSpan.FromMilliseconds(100))
                    .Type(field.Value)
                    .Delay(TimeSpan.FromMilliseconds(50));
            }

            return builder;
        }

        #endregion
    }

    #region Interfaces

    /// <summary>
    /// Интерфейс для построителя workflow
    /// </summary>
    public interface IWorkflowBuilder
    {
        IWorkflowBuilder CreateNew(string name);
        IWorkflowBuilder WaitForImage(string templateKey, TimeSpan? timeout = null);
        IWorkflowBuilder ClickOnImage(string templateKey);
        IWorkflowBuilder Type(string text);
        IWorkflowBuilder Delay(TimeSpan delay);
        IWorkflowBuilder PressKeys(params VirtualKey[] keys);
        IWorkflowBuilder ActivateWindow(string windowTitle);
        IWorkflowBuilder ClickOnImageInWindow(string windowTitle, string templateKey);
        IWorkflowBuilder TypeInWindow(string windowTitle, string text);
        IWorkflowBuilder If(Func<IWorkflowContext, ValueTask<bool>> condition, Action<IWorkflowBuilder> thenSteps);
        IWorkflowBuilder Retry(int maxAttempts, Action<IWorkflowBuilder> steps);
        IWorkflowBuilder Parallel(Action<IWorkflowBuilder> steps);
        IWorkflowBuilder While(Func<IWorkflowContext, ValueTask<bool>> condition, Action<IWorkflowBuilder> steps);
        IWorkflowBuilder AddCustomStep(string name, Func<IWorkflowContext, ValueTask> action);
        IWorkflowBuilder AddCustomStep<T>(string name, Func<IWorkflowContext, ValueTask<T>> action, string? storeKey = null);
        ValueTask<WorkflowResult> ExecuteAsync(CancellationToken ct = default);
    }


    /// <summary>
    /// Базовый интерфейс шага workflow
    /// </summary>
    public interface IWorkflowStep
    {
        string Name { get; }
        ValueTask ExecuteAsync(IWorkflowContext context, CancellationToken ct = default);
    }

    #endregion

    #region Context Implementation

    /// <summary>
    /// Реализация контекста выполнения workflow
    /// </summary>
    internal sealed class WorkflowContext : IWorkflowContext
    {
        public IServiceProvider Services { get; }
        public Dictionary<string, object> Variables { get; } = new();
        IDictionary<string, object> IWorkflowContext.Variables => Variables;
        public CancellationToken CancellationToken { get; set; }
        public object? LastStepResult { get; set; }
        public WorkflowExecutionState State { get; set; } = WorkflowExecutionState.NotStarted;

        public WorkflowContext(IServiceProvider services)
        {
            Services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public T GetService<T>() where T : notnull
        {
            return (T)Services.GetService(typeof(T))!;
        }


        public T? GetVariable<T>(string key)
        {
            if (Variables.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return default;
        }

        public bool HasVariable(string key) => Variables.ContainsKey(key);

        public void SetVariable(string key, object value)
        {
            if (value != null)
            {
                Variables[key] = value;
            }
            else
            {
                Variables.Remove(key);
            }
        }

        public void LogInfo(string message)
        {
            // TODO: Implement proper logging
            Console.WriteLine($"[INFO] {message}");
        }

        public void LogWarning(string message)
        {
            // TODO: Implement proper logging
            Console.WriteLine($"[WARNING] {message}");
        }

        public void LogError(string message, Exception? ex = null)
        {
            // TODO: Implement proper logging
            Console.WriteLine($"[ERROR] {message}");
            if (ex != null)
            {
                Console.WriteLine($"Exception: {ex}");
            }
        }
    }

    #endregion

    #region Results

    /// <summary>
    /// Результат выполнения workflow
    /// </summary>
    public sealed record WorkflowResult
    {
        public string Name { get; init; } = string.Empty;
        public bool Success { get; set; } = true;
        public TimeSpan Duration { get; set; }
        public Exception? Error { get; set; }
        public string? FailedStep { get; set; }
        public List<StepResult> CompletedSteps { get; init; } = new();

        public int TotalSteps => CompletedSteps.Count;
        public int SuccessfulSteps => CompletedSteps.Count(s => s.Success);
        public int FailedSteps => CompletedSteps.Count(s => !s.Success);
        public double SuccessRate => TotalSteps > 0 ? (double)SuccessfulSteps / TotalSteps : 0.0;
    }

    /// <summary>
    /// Результат выполнения отдельного шага
    /// </summary>
    public sealed record StepResult
    {
        public string Name { get; init; } = string.Empty;
        public int Index { get; init; }
        public TimeSpan Duration { get; init; }
        public bool Success { get; init; }
        public Exception? Error { get; init; }
        public Dictionary<string, object>? Data { get; init; }
    }

    #endregion
}