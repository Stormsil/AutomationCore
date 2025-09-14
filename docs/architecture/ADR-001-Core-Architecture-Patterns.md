# ADR-001: Core Architecture Patterns for AutomationCore Library

## Status
Accepted

## Context
AutomationCore is a Windows automation library that needs to support:
- Image-based template matching and screen capture
- Input simulation (mouse, keyboard)
- Window management operations
- Extensible workflow execution
- Future features: application launching, script execution, command processing

The library was previously monolithic with poor separation of concerns. We need architectural patterns that:
- Prevent code degradation over time
- Enable easy testing and mocking
- Support platform-specific implementations
- Allow extensible functionality without breaking existing code
- Maintain clean separation between domains

## Decision

### 1. **Domain-Driven Design (DDD) Structure**
```
Core/
├── Domain/           # Business logic domains
│   ├── Capture/     # Screen capture domain
│   ├── Input/       # Input simulation domain
│   ├── Matching/    # Template matching domain
│   ├── Windows/     # Window management domain
│   └── Workflows/   # Automation workflows domain
├── Abstractions/    # Cross-domain interfaces
├── Models/          # Shared data models
└── Configuration/   # DI container setup
```

### 2. **Command Pattern for All Operations**
Every automation operation implements `ICommand<TResult>`:
```csharp
public interface ICommand<TResult>
{
    ValueTask<TResult> ExecuteAsync(CancellationToken ct = default);
}

// Examples:
public class ClickCommand : ICommand<InputResult>
public class CaptureWindowCommand : ICommand<CaptureFrame>
public class FindTemplateCommand : ICommand<MatchResult?>
```

**Benefits:**
- Undo/Redo capability
- Command queuing and batching
- Easy testing through mocking
- Consistent async patterns

### 3. **Strategy Pattern for Platform Implementations**
Abstract platform-specific logic behind strategy interfaces:
```csharp
public interface ICaptureStrategy
{
    ValueTask<CaptureFrame> CaptureAsync(CaptureTarget target, CancellationToken ct);
}

// Implementations:
public class WindowsGraphicsCaptureStrategy : ICaptureStrategy
public class GdiPlusCaptureStrategy : ICaptureStrategy
```

### 4. **Factory Pattern for Cross-Platform Support**
```csharp
public interface ICaptureFactory
{
    ICaptureStrategy CreateBestStrategy(CaptureTarget target);
    ICaptureSession CreateSession(CaptureTarget target, CaptureOptions options);
}
```

### 5. **Repository Pattern for Data Access**
All external data access goes through repositories:
```csharp
public interface ITemplateRepository
{
    ValueTask<Template?> GetAsync(string key, CancellationToken ct);
    ValueTask SaveAsync(string key, Template template, CancellationToken ct);
}

public interface IConfigurationRepository
{
    ValueTask<T?> GetConfigAsync<T>(string section, CancellationToken ct);
}
```

### 6. **Observer Pattern for Event Handling**
Consistent event handling across all domains:
```csharp
public interface IEventPublisher
{
    void Publish<TEvent>(TEvent eventArgs) where TEvent : class;
}

// Events:
public record FrameCapturedEvent(CaptureFrame Frame, DateTime Timestamp);
public record TemplateMatchedEvent(MatchResult Result, string TemplateKey);
public record WorkflowCompletedEvent(string WorkflowId, bool Success);
```

### 7. **Chain of Responsibility for Workflows**
Complex automation sequences as processing chains:
```csharp
public interface IWorkflowStep
{
    ValueTask<WorkflowResult> ProcessAsync(WorkflowContext context, CancellationToken ct);
    IWorkflowStep? Next { get; set; }
}

// Usage:
var workflow = new CaptureStep()
    .Then(new FindTemplateStep("button.png"))
    .Then(new ClickStep())
    .Then(new DelayStep(TimeSpan.FromMilliseconds(500)));
```

### 8. **Result Pattern for Error Handling**
No exceptions for business logic failures:
```csharp
public readonly record struct Result<T>
{
    public bool IsSuccess { get; init; }
    public T? Value { get; init; }
    public string? Error { get; init; }

    public static Result<T> Success(T value) => new() { IsSuccess = true, Value = value };
    public static Result<T> Failure(string error) => new() { IsSuccess = false, Error = error };
}
```

### 9. **Dependency Injection Container Structure**
```csharp
// Core services registration
services.AddScoped<IWindowManager, WindowManager>();
services.AddScoped<ICaptureService, CaptureService>();

// Strategy registration
services.AddScoped<ICaptureStrategy, WindowsGraphicsCaptureStrategy>();
services.AddScoped<IInputStrategy, WindowsInputStrategy>();

// Factory registration
services.AddScoped<ICaptureFactory, CaptureFactory>();
services.AddScoped<ICommandFactory, CommandFactory>();
```

### 10. **Interface Segregation Principle**
Small, focused interfaces instead of large ones:
```csharp
// Bad:
public interface IWindowService
{
    Task CaptureAsync(...);
    Task FindAsync(...);
    Task ClickAsync(...);
    Task MoveAsync(...);
    // 20+ methods
}

// Good:
public interface IWindowCapture
{
    ValueTask<CaptureFrame> CaptureAsync(WindowHandle handle, CancellationToken ct);
}

public interface IWindowSearch
{
    ValueTask<MatchResult?> FindTemplateAsync(WindowHandle handle, string template, CancellationToken ct);
}
```

## Consequences

### Positive:
- **Testability**: Each component can be unit tested in isolation
- **Extensibility**: New automation features follow established patterns
- **Maintainability**: Clear separation prevents code degradation
- **Platform Support**: Easy to add Linux/macOS implementations
- **Performance**: Async-first design with minimal allocations

### Negative:
- **Initial Complexity**: More interfaces and classes than simple approach
- **Learning Curve**: New developers need to understand the patterns
- **Over-engineering Risk**: Simple operations become multi-class implementations

### Mitigation Strategies:
- Comprehensive documentation and examples
- Builder patterns to hide complexity from consumers
- Extension methods for common scenarios
- Code generators for repetitive pattern implementations

## Alternatives Considered

1. **Simple Static Classes**: Rejected due to testing difficulties and tight coupling
2. **Traditional OOP Inheritance**: Rejected due to inflexibility and violation of composition over inheritance
3. **Functional Programming Approach**: Rejected due to C# ecosystem expectations and team familiarity

## Implementation Timeline

1. **Phase 1**: Implement Command pattern for all operations (Current)
2. **Phase 2**: Refactor capture and input to Strategy pattern
3. **Phase 3**: Add Repository pattern for template storage
4. **Phase 4**: Implement Observer pattern for events
5. **Phase 5**: Add Chain of Responsibility for workflows

## References
- [Domain-Driven Design by Eric Evans](https://domainlanguage.com/ddd/)
- [Clean Architecture by Robert Martin](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [Microsoft .NET Application Architecture Guides](https://docs.microsoft.com/en-us/dotnet/architecture/)