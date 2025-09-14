# AutomationCore Coding Standards

## Обзор

Этот документ определяет обязательные стандарты кодирования для AutomationCore. Следование этим стандартам предотвращает деградацию кода и обеспечивает консистентность при разработке.

---

## 1. Именование (Naming Conventions)

### 1.1 Общие правила
```csharp
// ✅ Хорошо - описательные имена
public sealed class WindowsGraphicsCaptureService
public ValueTask<CaptureResult> CaptureWindowAsync(WindowHandle window, CancellationToken ct)

// ❌ Плохо - сокращения и неясные имена
public sealed class WGCService
public Task<CaptResult> CaptWinAsync(IntPtr hwnd, CancellationToken ct)
```

### 1.2 Интерфейсы
```csharp
// ✅ Префикс I + описательное имя + назначение
public interface ICaptureStrategy
public interface ITemplateRepository
public interface IAutomationCommand<TResult>

// ❌ Без префикса или слишком общие
public interface CaptureService
public interface Repository
public interface Command
```

### 1.3 Асинхронные методы
```csharp
// ✅ Суффикс Async для всех асинхронных методов
public ValueTask<CaptureFrame> CaptureFrameAsync(CancellationToken ct = default)
public Task InitializeAsync()

// ❌ Без Async суффикса
public ValueTask<CaptureFrame> CaptureFrame(CancellationToken ct = default)
```

### 1.4 Команды и запросы (CQRS)
```csharp
// ✅ Команды - глагол + Command
public sealed class CaptureWindowCommand
public sealed class FindTemplateCommand
public sealed class SimulateClickCommand

// ✅ Запросы - Get/Find/Is + Query
public sealed class GetWindowInfoQuery
public sealed class FindVisibleWindowsQuery

// ❌ Неясное назначение
public sealed class WindowProcessor
public sealed class TemplateService
```

---

## 2. Структура классов

### 2.1 Порядок членов класса
```csharp
public sealed class ExampleService
{
    // 1. Константы
    private const int DefaultTimeout = 5000;

    // 2. Статические поля
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

    // 3. Поля экземпляра (readonly первыми)
    private readonly IDependency _dependency;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _disposed;

    // 4. Конструкторы
    public ExampleService(IDependency dependency)
    {
        _dependency = dependency ?? throw new ArgumentNullException(nameof(dependency));
    }

    // 5. Публичные свойства
    public bool IsActive { get; private set; }

    // 6. Публичные методы
    public async ValueTask<Result> DoSomethingAsync(CancellationToken ct = default)
    {
        // Реализация
    }

    // 7. Приватные методы
    private async ValueTask InitializeInternalAsync()
    {
        // Реализация
    }

    // 8. IDisposable (если нужен)
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _semaphore?.Dispose();
    }
}
```

### 2.2 Размер классов
```csharp
// ✅ Хорошо - один класс = одна ответственность (до 200 строк)
public sealed class WindowsInputSimulator : IInputSimulator
{
    // Только симуляция ввода, ~150 строк
}

public sealed class CaptureSessionManager : ICaptureSessionManager
{
    // Только управление сессиями, ~180 строк
}

// ❌ Плохо - божественный объект (1000+ строк)
public sealed class AutomationService
{
    // Захват + матчинг + ввод + управление окнами = 1500 строк
}
```

---

## 3. Методы и сигнатуры

### 3.1 Асинхронные методы
```csharp
// ✅ ValueTask для возможно синхронных операций
public ValueTask<CaptureFrame> GetCachedFrameAsync(string key, CancellationToken ct = default)

// ✅ Task для всегда асинхронных операций
public Task InitializeAsync(CancellationToken ct = default)

// ✅ Всегда CancellationToken параметр с default значением
public async ValueTask<Result> ProcessAsync(Request request, CancellationToken ct = default)

// ❌ Task вместо ValueTask для потенциально синхронных операций
public Task<CaptureFrame> GetCachedFrameAsync(string key)

// ❌ Без CancellationToken
public async Task ProcessAsync(Request request)
```

### 3.2 Параметры методов
```csharp
// ✅ Валидация параметров
public async ValueTask<Result> ProcessAsync(string templateKey, CancellationToken ct = default)
{
    if (string.IsNullOrWhiteSpace(templateKey))
        throw new ArgumentException("Template key cannot be empty", nameof(templateKey));

    // Реализация
}

// ✅ Required для обязательных свойств (C# 11+)
public sealed record CaptureRequest
{
    public required WindowHandle Window { get; init; }
    public required string OutputPath { get; init; }
    public CaptureOptions Options { get; init; } = new();
}

// ❌ Без валидации
public async ValueTask<Result> ProcessAsync(string templateKey, CancellationToken ct = default)
{
    // Прямое использование без проверки
    var result = await _service.FindAsync(templateKey, ct);
}
```

### 3.3 Обработка ошибок
```csharp
// ✅ Result pattern для бизнес-логики
public async ValueTask<Result<MatchResult>> FindTemplateAsync(string key, CancellationToken ct)
{
    try
    {
        var template = await _repository.GetAsync(key, ct);
        if (template is null)
            return Result<MatchResult>.Failure($"Template '{key}' not found");

        var match = await _matcher.MatchAsync(template, ct);
        return Result<MatchResult>.Success(match);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        return Result<MatchResult>.Failure($"Template matching failed: {ex.Message}");
    }
}

// ✅ Исключения только для системных ошибок
public ValueTask InitializeAsync(CancellationToken ct = default)
{
    if (_disposed)
        throw new ObjectDisposedException(nameof(CaptureService));

    // Реализация
}

// ❌ Исключения для бизнес-логики
public async ValueTask<MatchResult> FindTemplateAsync(string key, CancellationToken ct)
{
    var template = await _repository.GetAsync(key, ct);
    if (template is null)
        throw new TemplateNotFoundException(key); // Плохо!
}
```

---

## 4. Dependency Injection

### 4.1 Регистрация сервисов
```csharp
// ✅ Explicit registration в AutomationBuilder
public class AutomationBuilder
{
    public AutomationBuilder WithWindowsCapture()
    {
        // Абстракция первой
        _services.AddScoped<ICaptureService, WindowsGraphicsCaptureService>();

        // Стратегии как именованные сервисы
        _services.AddKeyedScoped<ICaptureStrategy, WindowsGraphicsCaptureStrategy>("wgc");
        _services.AddKeyedScoped<ICaptureStrategy, GdiCaptureStrategy>("gdi");

        // Фабрика для выбора стратегии
        _services.AddScoped<ICaptureStrategyFactory, CaptureStrategyFactory>();

        return this;
    }
}

// ❌ Service Locator anti-pattern
public class SomeService
{
    private readonly IServiceProvider _services;

    public async Task DoSomethingAsync()
    {
        var dependency = _services.GetRequiredService<IDependency>(); // Плохо!
    }
}
```

### 4.2 Жизненные циклы
```csharp
// ✅ Правильные жизненные циклы
// Singleton - для stateless сервисов
services.AddSingleton<ITemplateCache, MemoryTemplateCache>();

// Scoped - для stateful сервисов с коротким жизненным циклом
services.AddScoped<ICaptureSession, CaptureSession>();

// Transient - для команд и легких объектов
services.AddTransient<ICommand<CaptureResult>, CaptureCommand>();

// ❌ Неправильные жизненные циклы
// Singleton для stateful сервиса
services.AddSingleton<ICaptureSession, CaptureSession>(); // Опасно!

// Transient для тяжелого объекта
services.AddTransient<ITemplateCache, DiskTemplateCache>(); // Неэффективно!
```

---

## 5. Файловая структура

### 5.1 Организация папок
```
Core/
├── Domain/              # Доменная логика
│   ├── Capture/        # Один домен = одна папка
│   │   ├── Commands/   # Команды домена
│   │   ├── Models/     # Модели домена
│   │   └── Services/   # Сервисы домена
│   ├── Input/
│   ├── Matching/
│   └── Windows/
├── Abstractions/       # Интерфейсы между доменами
├── Models/            # Общие модели
└── Configuration/     # DI настройки
```

### 5.2 Один класс = один файл
```csharp
// ✅ Хорошо
// WindowsGraphicsCaptureService.cs
public sealed class WindowsGraphicsCaptureService : ICaptureService
{
    // Реализация
}

// ✅ Исключение - тесно связанные типы
// CaptureModels.cs
public sealed record CaptureRequest(WindowHandle Window, CaptureOptions Options);
public sealed record CaptureResult(CaptureFrame Frame, CaptureMetrics Metrics);
public enum CaptureStatus { Ready, Capturing, Stopped, Error }

// ❌ Плохо - несколько не связанных классов
// Services.cs
public class CaptureService { }
public class InputService { }
public class WindowService { }
```

### 5.3 Имена файлов
```csharp
// ✅ Соответствие имени класса
public sealed class TemplateMatchingEngine    // TemplateMatchingEngine.cs
public interface ICaptureStrategy            // ICaptureStrategy.cs

// ✅ Множественное число для коллекций типов
// CaptureModels.cs - несколько моделей Capture домена
// InputCommands.cs - несколько команд Input домена

// ❌ Не соответствует содержимому
// Utils.cs
// Helper.cs
// Common.cs
```

---

## 6. Async/Await Patterns

### 6.1 ConfigureAwait
```csharp
// ✅ В библиотеках всегда ConfigureAwait(false)
public async ValueTask<CaptureFrame> CaptureAsync(CancellationToken ct = default)
{
    var result = await _captureService.GetFrameAsync(ct).ConfigureAwait(false);
    await ProcessFrameAsync(result, ct).ConfigureAwait(false);
    return result;
}

// ❌ Без ConfigureAwait в библиотеке
public async ValueTask<CaptureFrame> CaptureAsync(CancellationToken ct = default)
{
    var result = await _captureService.GetFrameAsync(ct); // Потенциальный deadlock!
    return result;
}
```

### 6.2 Cancellation Token
```csharp
// ✅ Передача CancellationToken во все асинхронные вызовы
public async ValueTask<Result> ProcessAsync(CancellationToken ct = default)
{
    ct.ThrowIfCancellationRequested();

    var frame = await _captureService.CaptureAsync(ct).ConfigureAwait(false);
    var result = await _processor.ProcessFrameAsync(frame, ct).ConfigureAwait(false);

    return result;
}

// ❌ Игнорирование cancellation token
public async ValueTask<Result> ProcessAsync(CancellationToken ct = default)
{
    var frame = await _captureService.CaptureAsync().ConfigureAwait(false); // ct не передан!
    return ProcessFrame(frame);
}
```

---

## 7. Тестирование

### 7.1 Именование тестов
```csharp
// ✅ Given_When_Then или Subject_Scenario_ExpectedResult
[Test]
public async Task CaptureAsync_WhenWindowNotFound_ReturnsFailureResult()
{
    // Arrange
    var invalidHandle = new WindowHandle(IntPtr.Zero);

    // Act
    var result = await _captureService.CaptureAsync(invalidHandle);

    // Assert
    Assert.That(result.IsSuccess, Is.False);
    Assert.That(result.Error, Contains.Substring("Window not found"));
}

// ❌ Неясные имена тестов
[Test]
public void Test1() { }

[Test]
public void CaptureTest() { }
```

### 7.2 Моки и стабы
```csharp
// ✅ Моки для поведения, стабы для данных
[Test]
public async Task ProcessAsync_WhenCalled_InvokesRepositorySaveOnce()
{
    // Arrange
    var mockRepository = new Mock<ITemplateRepository>();
    var service = new TemplateService(mockRepository.Object);

    // Act
    await service.SaveTemplateAsync("key", template);

    // Assert - проверяем поведение
    mockRepository.Verify(x => x.SaveAsync("key", template, It.IsAny<CancellationToken>()),
                         Times.Once);
}

// ✅ Стабы для возврата данных
[SetUp]
public void SetUp()
{
    _stubRepository = new Mock<ITemplateRepository>();
    _stubRepository.Setup(x => x.GetAsync("template1", It.IsAny<CancellationToken>()))
               .ReturnsAsync(new Template("template1", new byte[100]));
}
```

---

## 8. Производительность

### 8.1 Allocations
```csharp
// ✅ Минимизация аллокаций
public ValueTask<Result<T>> GetCachedAsync<T>(string key) where T : class
{
    if (_cache.TryGetValue(key, out var cached))
        return new ValueTask<Result<T>>(Result<T>.Success((T)cached)); // Не создаем Task

    return GetFromRepositoryAsync<T>(key); // Task только если нужен
}

// ✅ ArrayPool для временных буферов
private static readonly ArrayPool<byte> BytePool = ArrayPool<byte>.Shared;

public async ValueTask ProcessDataAsync(ReadOnlyMemory<byte> data)
{
    var buffer = BytePool.Rent(data.Length);
    try
    {
        data.Span.CopyTo(buffer);
        await ProcessBufferAsync(buffer.AsMemory(0, data.Length));
    }
    finally
    {
        BytePool.Return(buffer);
    }
}

// ❌ Избыточные аллокации
public Task<Result<T>> GetCachedAsync<T>(string key) where T : class
{
    if (_cache.TryGetValue(key, out var cached))
        return Task.FromResult(Result<T>.Success((T)cached)); // Лишняя аллокация Task
}
```

### 8.2 StringBuilder для строк
```csharp
// ✅ StringBuilder для множественных конкатенаций
public string BuildLogMessage(IEnumerable<LogEntry> entries)
{
    var sb = new StringBuilder();
    foreach (var entry in entries)
    {
        sb.Append('[').Append(entry.Timestamp).Append("] ");
        sb.Append(entry.Level).Append(": ");
        sb.AppendLine(entry.Message);
    }
    return sb.ToString();
}

// ❌ String concatenation в циклах
public string BuildLogMessage(IEnumerable<LogEntry> entries)
{
    var result = "";
    foreach (var entry in entries)
    {
        result += $"[{entry.Timestamp}] {entry.Level}: {entry.Message}\n"; // Много аллокаций!
    }
    return result;
}
```

---

## 9. Документирование кода

### 9.1 XML Documentation
```csharp
/// <summary>
/// Captures a frame from the specified window using Windows Graphics Capture API.
/// </summary>
/// <param name="windowHandle">Handle to the target window.</param>
/// <param name="options">Capture options. If null, default options are used.</param>
/// <param name="ct">Cancellation token to cancel the operation.</param>
/// <returns>
/// A <see cref="Result{T}"/> containing the captured frame on success,
/// or error information on failure.
/// </returns>
/// <exception cref="ObjectDisposedException">Thrown when the service is disposed.</exception>
/// <exception cref="ArgumentException">Thrown when <paramref name="windowHandle"/> is invalid.</exception>
public async ValueTask<Result<CaptureFrame>> CaptureAsync(
    WindowHandle windowHandle,
    CaptureOptions? options = null,
    CancellationToken ct = default)
```

### 9.2 Код должен быть самодокументирующимся
```csharp
// ✅ Ясные имена без комментариев
public async ValueTask<bool> WaitForTemplateToAppearAsync(
    string templateKey,
    TimeSpan timeout,
    CancellationToken ct = default)
{
    var deadline = DateTime.UtcNow + timeout;

    while (DateTime.UtcNow < deadline)
    {
        var match = await FindTemplateAsync(templateKey, ct);
        if (match.IsSuccess && match.Value.Score > MinimumMatchScore)
            return true;

        await Task.Delay(PollingInterval, ct);
    }

    return false;
}

// ❌ Комментарии вместо ясного кода
public async ValueTask<bool> WaitAsync(string key, TimeSpan timeout, CancellationToken ct = default)
{
    var end = DateTime.UtcNow + timeout; // Вычисляем когда закончить ожидание

    while (DateTime.UtcNow < end) // Цикл до таймаута
    {
        var result = await FindAsync(key, ct); // Ищем шаблон
        if (result.IsSuccess && result.Value.Score > 0.8) // Если найден с хорошим скором
            return true;

        await Task.Delay(100, ct); // Ждем немного перед следующей попыткой
    }

    return false; // Не найден за отведенное время
}
```

---

## Заключение

Эти стандарты обязательны для всего кода в AutomationCore. Они обеспечивают:

1. **Консистентность** - код выглядит единообразно независимо от автора
2. **Читаемость** - любой разработчик может быстро понять код
3. **Поддерживаемость** - изменения безопасны и предсказуемы
4. **Тестируемость** - код легко покрывается тестами
5. **Производительность** - избегаем распространенных проблем производительности

При code review все нарушения этих стандартов должны быть исправлены до merge'а.