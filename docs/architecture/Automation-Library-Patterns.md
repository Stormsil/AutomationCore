# Automation Library Patterns для AutomationCore

## Обзор

Этот документ определяет специфичные паттерны для библиотек автоматизации, адаптированные под потребности AutomationCore. В отличие от UI-паттернов вроде MVVM, здесь фокус на надежности, тестируемости и расширяемости бизнес-логики.

## 1. **Automation Command Pattern**

### Проблема
В автоматизации операции должны быть:
- Отменяемыми
- Повторяемыми
- Логируемыми
- Тестируемыми
- Композируемыми в сложные сценарии

### Решение
```csharp
public interface IAutomationCommand<TResult> : ICommand<TResult>
{
    string Name { get; }
    TimeSpan Timeout { get; set; }
    int RetryCount { get; set; }

    ValueTask<TResult> ExecuteAsync(CancellationToken ct = default);
    ValueTask<bool> CanExecuteAsync(CancellationToken ct = default);
    ValueTask UndoAsync(CancellationToken ct = default);
}

// Конкретная реализация
public sealed class ClickAtCommand : IAutomationCommand<InputResult>
{
    public string Name => $"ClickAt({_point.X}, {_point.Y})";
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);
    public int RetryCount { get; set; } = 3;

    private readonly Point _point;
    private readonly IInputSimulator _input;

    public async ValueTask<InputResult> ExecuteAsync(CancellationToken ct = default)
    {
        for (int i = 0; i < RetryCount; i++)
        {
            try
            {
                return await _input.Mouse.ClickAsync(_point, ct);
            }
            catch (Exception ex) when (i < RetryCount - 1)
            {
                await Task.Delay(100 * (i + 1), ct); // Exponential backoff
            }
        }

        return InputResult.Failure("Click failed after retries");
    }
}
```

### Применение
```csharp
var command = new ClickAtCommand(new Point(100, 200));
command.Timeout = TimeSpan.FromSeconds(10);
command.RetryCount = 5;

var result = await command.ExecuteAsync(cancellationToken);
if (!result.IsSuccess)
{
    await command.UndoAsync(); // Откат если необходимо
}
```

---

## 2. **Capture-Match-Act Pattern**

### Проблема
Большинство автоматизационных задач следуют циклу: захватить экран → найти элемент → выполнить действие.

### Решение
```csharp
public interface IAutomationSequence<TResult>
{
    ValueTask<TResult> ExecuteAsync(CancellationToken ct = default);
}

public sealed class CaptureMatchActSequence<TResult> : IAutomationSequence<TResult>
{
    private readonly ICaptureCommand _captureCommand;
    private readonly IMatchCommand _matchCommand;
    private readonly IAutomationCommand<TResult> _actionCommand;

    public async ValueTask<TResult> ExecuteAsync(CancellationToken ct = default)
    {
        // 1. Capture
        var frame = await _captureCommand.ExecuteAsync(ct);
        if (frame is null) return default(TResult);

        // 2. Match
        var match = await _matchCommand.ExecuteAsync(frame, ct);
        if (match is null) return default(TResult);

        // 3. Act
        return await _actionCommand.ExecuteAsync(ct);
    }
}

// Fluent API для создания
public static class AutomationSequenceBuilder
{
    public static CaptureStep Capture(CaptureTarget target) => new(target);
}

public class CaptureStep
{
    public MatchStep<T> Match<T>(string template) where T : IMatchCommand => new(template);
}
```

---

## 3. **Polling Observer Pattern**

### Проблема
Автоматизация часто требует ожидания появления/исчезновения элементов с таймаутом.

### Решение
```csharp
public interface IConditionWaiter
{
    ValueTask<bool> WaitUntilAsync<T>(
        Func<ValueTask<T>> condition,
        Predicate<T> predicate,
        TimeSpan timeout,
        TimeSpan interval = default,
        CancellationToken ct = default);
}

public sealed class PollingConditionWaiter : IConditionWaiter
{
    public async ValueTask<bool> WaitUntilAsync<T>(
        Func<ValueTask<T>> condition,
        Predicate<T> predicate,
        TimeSpan timeout,
        TimeSpan interval = default,
        CancellationToken ct = default)
    {
        interval = interval == default ? TimeSpan.FromMilliseconds(100) : interval;
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            var result = await condition();
            if (predicate(result))
                return true;

            await Task.Delay(interval, ct);
        }

        return false;
    }
}

// Использование
var waiter = new PollingConditionWaiter();
var found = await waiter.WaitUntilAsync(
    condition: () => templateMatcher.FindAsync("button.png"),
    predicate: match => match is not null && match.Score > 0.8,
    timeout: TimeSpan.FromSeconds(30));
```

---

## 4. **Strategy Factory Pattern**

### Проблема
Разные ситуации требуют разных стратегий (быстрый захват vs качественный, разные алгоритмы матчинга).

### Решение
```csharp
public interface IStrategyFactory<TStrategy>
{
    TStrategy CreateBest(StrategyContext context);
    IEnumerable<TStrategy> CreateAll();
}

public sealed class CaptureStrategyFactory : IStrategyFactory<ICaptureStrategy>
{
    private readonly IServiceProvider _services;

    public ICaptureStrategy CreateBest(StrategyContext context)
    {
        return context.Priority switch
        {
            CapturePriority.Speed => _services.GetRequiredService<FastCaptureStrategy>(),
            CapturePriority.Quality => _services.GetRequiredService<HighQualityCaptureStrategy>(),
            CapturePriority.Compatibility => _services.GetRequiredService<GdiCompatibleCaptureStrategy>(),
            _ => _services.GetRequiredService<WindowsGraphicsCaptureStrategy>()
        };
    }
}

// Использование в команде
public sealed class SmartCaptureCommand : IAutomationCommand<CaptureFrame>
{
    private readonly IStrategyFactory<ICaptureStrategy> _strategyFactory;

    public async ValueTask<CaptureFrame> ExecuteAsync(CancellationToken ct = default)
    {
        var context = new StrategyContext { Priority = CapturePriority.Speed };
        var strategy = _strategyFactory.CreateBest(context);
        return await strategy.CaptureAsync(_target, ct);
    }
}
```

---

## 5. **Workflow Builder Pattern**

### Проблема
Сложные автоматизационные сценарии должны быть композируемыми и читаемыми.

### Решение
```csharp
public interface IWorkflowBuilder
{
    IWorkflowBuilder CaptureWindow(string windowTitle);
    IWorkflowBuilder WaitForTemplate(string template, TimeSpan timeout);
    IWorkflowBuilder ClickAt(Point point);
    IWorkflowBuilder Type(string text);
    IWorkflowBuilder Delay(TimeSpan delay);
    IWorkflowBuilder If(Func<WorkflowContext, bool> condition);
    IWorkflowBuilder Repeat(int count);

    IWorkflow Build();
}

// Fluent API использование
var workflow = workflowBuilder
    .CaptureWindow("Calculator")
    .WaitForTemplate("button_7.png", TimeSpan.FromSeconds(5))
    .ClickAt(Point.Empty) // Будет использован центр найденного шаблона
    .Type("*")
    .WaitForTemplate("button_8.png", TimeSpan.FromSeconds(5))
    .ClickAt(Point.Empty)
    .Delay(TimeSpan.FromMilliseconds(500))
    .Build();

var result = await workflow.ExecuteAsync();
```

---

## 6. **Resource Lifecycle Pattern**

### Проблема
Automation ресурсы (захватчики экрана, окна) требуют управления жизненным циклом.

### Решение
```csharp
public interface IResourceManager<TResource> : IDisposable
{
    ValueTask<TResource> AcquireAsync(ResourceKey key, CancellationToken ct = default);
    ValueTask ReleaseAsync(ResourceKey key, CancellationToken ct = default);
    ValueTask<bool> IsAvailableAsync(ResourceKey key, CancellationToken ct = default);
}

public sealed class CaptureSessionManager : IResourceManager<ICaptureSession>
{
    private readonly ConcurrentDictionary<ResourceKey, ICaptureSession> _sessions = new();

    public async ValueTask<ICaptureSession> AcquireAsync(ResourceKey key, CancellationToken ct = default)
    {
        if (_sessions.TryGetValue(key, out var existing) && existing.IsActive)
            return existing;

        var session = await _captureFactory.CreateSessionAsync(key.Target, ct);
        _sessions[key] = session;
        return session;
    }

    public async ValueTask ReleaseAsync(ResourceKey key, CancellationToken ct = default)
    {
        if (_sessions.TryRemove(key, out var session))
        {
            await session.StopAsync(ct);
            session.Dispose();
        }
    }
}

// Использование с using
await using var sessionManager = new CaptureSessionManager();
var session = await sessionManager.AcquireAsync(new ResourceKey("Calculator"), ct);
// Автоматическое освобождение при выходе из scope
```

---

## 7. **Event Sourcing для Automation Logs**

### Проблема
Отладка автоматизации требует детального лога всех операций с возможностью воспроизведения.

### Решение
```csharp
public interface IAutomationEventStore
{
    ValueTask AppendAsync(AutomationEvent automationEvent, CancellationToken ct = default);
    IAsyncEnumerable<AutomationEvent> ReadAsync(string sessionId, CancellationToken ct = default);
}

public abstract record AutomationEvent
{
    public required string SessionId { get; init; }
    public required DateTime Timestamp { get; init; }
    public required string EventType { get; init; }
}

public record CommandExecutedEvent : AutomationEvent
{
    public required string CommandName { get; init; }
    public required object Parameters { get; init; }
    public required object Result { get; init; }
    public required TimeSpan Duration { get; init; }
}

public record TemplateMatchedEvent : AutomationEvent
{
    public required string TemplateName { get; init; }
    public required double Score { get; init; }
    public required Rectangle Bounds { get; init; }
    public required string ScreenshotPath { get; init; }
}

// Интеграция в команды
public abstract class EventedAutomationCommand<TResult> : IAutomationCommand<TResult>
{
    protected readonly IAutomationEventStore _eventStore;
    protected readonly string _sessionId;

    public async ValueTask<TResult> ExecuteAsync(CancellationToken ct = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            var result = await ExecuteInternalAsync(ct);

            await _eventStore.AppendAsync(new CommandExecutedEvent
            {
                SessionId = _sessionId,
                Timestamp = startTime,
                EventType = nameof(CommandExecutedEvent),
                CommandName = Name,
                Parameters = GetParameters(),
                Result = result,
                Duration = DateTime.UtcNow - startTime
            }, ct);

            return result;
        }
        catch (Exception ex)
        {
            await _eventStore.AppendAsync(new CommandFailedEvent
            {
                SessionId = _sessionId,
                Timestamp = startTime,
                EventType = nameof(CommandFailedEvent),
                CommandName = Name,
                Parameters = GetParameters(),
                Error = ex.Message,
                Duration = DateTime.UtcNow - startTime
            }, ct);

            throw;
        }
    }

    protected abstract ValueTask<TResult> ExecuteInternalAsync(CancellationToken ct);
    protected abstract object GetParameters();
}
```

---

## Заключение

Эти паттерны обеспечивают:

1. **Надежность** - через retry логику и правильное управление ресурсами
2. **Тестируемость** - через инверсию зависимостей и изоляцию
3. **Отладка** - через event sourcing и детальное логирование
4. **Расширяемость** - через композицию и стратегии
5. **Читаемость** - через fluent API и DSL подходы

Каждый паттерн решает конкретные проблемы автоматизации и может использоваться независимо или в комбинации с другими.