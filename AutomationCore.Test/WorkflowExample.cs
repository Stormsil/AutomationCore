using AutomationCore.Features.Workflows;
using AutomationCore.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace AutomationCore.Test;

/// <summary>
/// Пример создания workflow с помощью WorkflowBuilder
/// </summary>
public static class WorkflowExample
{
    /// <summary>
    /// Создает простой workflow для автоматизации блокнота
    /// </summary>
    public static async Task RunNotepadWorkflowExample(IServiceProvider services, ILogger logger)
    {
        logger.LogInformation("🎭 Starting Workflow Builder example");

        try
        {
            var workflowBuilder = services.GetRequiredService<WorkflowBuilder>();

            // Создаем workflow для работы с блокнотом
            var workflow = workflowBuilder
                .StartWorkflow("Notepad Automation Demo")
                .AddDelay(1000) // Пауза 1 секунда
                .AddCustomAction(async (context) =>
                {
                    logger.LogInformation("🚀 Custom action: Starting Notepad process");

                    var process = System.Diagnostics.Process.Start("notepad.exe");
                    if (process != null)
                    {
                        logger.LogInformation("✅ Notepad started successfully");
                        context.SetVariable("NotepadProcess", process);
                    }
                    else
                    {
                        throw new Exception("Failed to start Notepad");
                    }
                }, "Start Notepad")
                .AddDelay(2000) // Ждем загрузку приложения
                .AddCustomAction(async (context) =>
                {
                    logger.LogInformation("⌨️ Custom action: Typing text in Notepad");

                    // Здесь можно было бы использовать WindowAutomator для ввода текста
                    // но для демонстрации используем простую имитацию
                    await Task.Delay(500);
                    logger.LogInformation("✅ Text typing simulated");
                }, "Type Text")
                .Build();

            // Выполняем workflow
            logger.LogInformation("▶️ Executing workflow: {Name}", workflow.Name);
            var result = await workflow.ExecuteAsync();

            if (result.Success)
            {
                logger.LogInformation("✅ Workflow completed successfully");
                logger.LogInformation("📊 Steps executed: {StepCount}", result.ExecutedSteps.Count);
                logger.LogInformation("⏱️ Total execution time: {Duration:F2}s", result.ExecutionTime.TotalSeconds);
            }
            else
            {
                logger.LogError("❌ Workflow failed: {Error}", result.Error);
                if (result.FailedStep != null)
                {
                    logger.LogError("💥 Failed at step: {StepName}", result.FailedStep.Name);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error during workflow example");
        }
    }

    /// <summary>
    /// Пример более сложного workflow с условиями и циклами
    /// </summary>
    public static async Task RunAdvancedWorkflowExample(IServiceProvider services, ILogger logger)
    {
        logger.LogInformation("🎪 Starting Advanced Workflow example");

        try
        {
            var workflowBuilder = services.GetRequiredService<WorkflowBuilder>();

            var workflow = workflowBuilder
                .StartWorkflow("Advanced Automation Demo")
                // Инициализация
                .AddCustomAction(async (context) =>
                {
                    context.SetVariable("Counter", 0);
                    context.SetVariable("MaxRetries", 3);
                    logger.LogInformation("🔧 Workflow variables initialized");
                }, "Initialize")

                // Условная логика
                .AddConditionalStep(
                    condition: (context) =>
                    {
                        var counter = (int)context.Variables.GetValueOrDefault("Counter", 0);
                        var maxRetries = (int)context.Variables.GetValueOrDefault("MaxRetries", 3);
                        return counter < maxRetries;
                    },
                    trueBranch: builder => builder
                        .AddCustomAction(async (context) =>
                        {
                            var counter = (int)context.Variables.GetValueOrDefault("Counter", 0);
                            counter++;
                            context.SetVariable("Counter", counter);
                            logger.LogInformation("🔄 Retry attempt: {Counter}", counter);

                            // Имитация операции, которая может не сработать
                            if (counter < 2)
                            {
                                throw new Exception($"Simulated failure on attempt {counter}");
                            }
                        }, "Retry Operation"),
                    falseBranch: builder => builder
                        .AddCustomAction(async (context) =>
                        {
                            logger.LogInformation("⏭️ Max retries exceeded, skipping");
                        }, "Skip Operation")
                )

                // Финализация
                .AddCustomAction(async (context) =>
                {
                    var finalCounter = (int)context.Variables.GetValueOrDefault("Counter", 0);
                    logger.LogInformation("🏁 Workflow completed with {Counter} attempts", finalCounter);
                }, "Finalize")

                .Build();

            // Выполняем workflow с retry логикой
            var result = await workflow.ExecuteAsync(retryCount: 2);

            logger.LogInformation("📈 Advanced workflow result: {Success}", result.Success);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error during advanced workflow example");
        }
    }
}