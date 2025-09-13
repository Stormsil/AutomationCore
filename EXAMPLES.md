# Примеры использования AutomationCore

## 🎯 Простой API (рекомендуется для начинающих)

### Базовые операции

```csharp
using AutomationCore.Public.Facades;

// Создание клиента с настройками по умолчанию
using var automation = AutomationClient.Create();

// Поиск и клик по изображению
bool clicked = await automation.ClickOnImageAsync("login_button");
if (clicked)
{
    Console.WriteLine("Кнопка нажата!");
}

// Ввод текста
await automation.TypeAsync("Hello, World!");

// Ожидание появления изображения
bool found = await automation.WaitAndClickOnImageAsync("dialog_ok", TimeSpan.FromSeconds(10));
```

### Работа с окнами

```csharp
using var automation = AutomationClient.Create();

// Поиск окна
var window = await automation.FindWindowAsync("Калькулятор");
if (window != null)
{
    // Активация окна
    await automation.ActivateWindowAsync(window.Handle);
    
    // Работа в активном окне
    await automation.TypeAsync("2+2=");
    await automation.ClickOnImageAsync("equals_button");
}
```

## 🔧 Продвинутый API (для опытных пользователей)

### Настройка системы

```csharp
using var automation = AutomationClient.Create(options =>
{
    // Пути
    options.TemplatesPath = @"C:\MyProject\Templates";
    options.TempPath = @"C:\Temp\Automation";
    
    // Производительность
    options.Capture = CaptureConfiguration.HighPerformance;
    options.Input = InputConfiguration.Fast;
    options.Matching = MatchingConfiguration.Accurate;
    
    // Кэширование
    options.Cache = CacheConfiguration.Aggressive;
    
    // Логирование
    options.Logging = LoggingConfiguration.Debug;
});
```

### Прямое использование сервисов

```csharp
using var automation = AutomationClient.Create();

// Работа с захватом экрана
var captureRequest = CaptureRequest.ForWindow(windowHandle);
var captureResult = await automation.Capture.CaptureAsync(captureRequest);

// Поиск изображений  
var imageSearch = automation.Images;
var searchResult = await imageSearch.FindAsync("template_key", 
    ImageSearchOptions.Default.WithThreshold(0.95).InRegion(searchArea));

// Точное управление вводом
await automation.Input.Mouse.MoveToAsync(new Point(100, 200), 
    new MouseMoveOptions { Duration = TimeSpan.FromMilliseconds(500) });

await automation.Input.Mouse.ClickAsync(MouseButton.Right);
```

## 🎭 Workflow API (для сложных сценариев)

### Простой workflow

```csharp
using var automation = AutomationClient.Create();

var result = await automation.CreateWorkflow("Login Process")
    .WaitForImage("login_form", TimeSpan.FromSeconds(10))
    .ClickOnImage("username_field")
    .Type("john.doe@example.com")
    .PressKeys(VirtualKey.Tab)
    .Type("mySecretPassword123")
    .ClickOnImage("login_button")
    .WaitForImage("dashboard", TimeSpan.FromSeconds(15))
    .ExecuteAsync();

if (result.Success)
{
    Console.WriteLine($"Логин успешен за {result.Duration.TotalSeconds:F1}s");
}
else
{
    Console.WriteLine($"Ошибка на шаге: {result.FailedStep}");
    Console.WriteLine($"Сообщение: {result.Error?.Message}");
}
```

### Сложный workflow с условиями и повторами

```csharp
var result = await automation.CreateWorkflow("Complex Task")
    
    // Активация приложения
    .ActivateWindow("MyApp v2.1")
    .Delay(TimeSpan.FromMilliseconds(500))
    
    // Условная логика
    .If(async ctx => await CheckIfLoginNeeded(ctx), loginFlow => loginFlow
        .ClickOnImage("login_menu")
        .WaitForImage("login_dialog") 
        .Type("username")
        .PressKeys(VirtualKey.Tab)
        .Type("password")
        .PressKeys(VirtualKey.Return))
    
    // Повторы с обработкой ошибок
    .Retry(3, retryFlow => retryFlow
        .ClickOnImage("process_button")
        .WaitForImage("success_message", TimeSpan.FromSeconds(5)))
    
    // Параллельное выполнение
    .Parallel(parallelFlow => parallelFlow
        .AddCustomStep("Monitor Progress", MonitorProgressAsync)
        .AddCustomStep("Check Errors", CheckErrorsAsync))
    
    // Цикл
    .While(async ctx => await HasMoreData(ctx), whileFlow => whileFlow
        .ClickOnImage("next_page")
        .WaitForImage("page_loaded")
        .AddCustomStep("Process Page", ProcessCurrentPageAsync))
    
    .ExecuteAsync();
```

### Кастомные шаги

```csharp
// Добавление пользовательских операций
var result = await automation.CreateWorkflow("Data Processing")
    
    .AddCustomStep("Load Configuration", async ctx =>
    {
        var config = await LoadConfigurationAsync();
        ctx.SetVariable("Config", config);
    })
    
    .AddCustomStep<List<string>>("Get File List", async ctx =>
    {
        var config = ctx.GetVariable<AppConfig>("Config");
        return await GetFilesFromDirectoryAsync(config.WorkDirectory);
    }, "FileList")
    
    .AddCustomStep("Process Files", async ctx =>
    {
        var files = ctx.GetVariable<List<string>>("FileList");
        foreach (var file in files)
        {
            await ProcessFileAsync(file, ctx.CancellationToken);
        }
    })
    
    .ExecuteAsync();
```

## 🏪 Готовые шаблоны workflow

### Заполнение формы

```csharp
var formData = new Dictionary<string, string>
{
    ["name_field"] = "John Doe",
    ["email_field"] = "john@example.com", 
    ["phone_field"] = "+1-555-0123",
    ["address_field"] = "123 Main St"
};

var result = await WorkflowBuilder.FillForm(
    "Contact Form", 
    "Contact Application", 
    formData, 
    services, 
    logger)
    .ClickOnImage("submit_button")
    .WaitForImage("success_message")
    .ExecuteAsync();
```

### Быстрый клик

```csharp
var result = await WorkflowBuilder.QuickClick(
    "Quick Start", 
    "start_button", 
    services, 
    logger)
    .ExecuteAsync();
```

## 🔬 Диагностика и отладка

### Проверка состояния системы

```csharp
using var automation = AutomationClient.Create();

// Проверка доступности функций
var status = await automation.GetSystemStatusAsync();
if (!status.IsHealthy)
{
    Console.WriteLine($"Система не готова:");
    Console.WriteLine($"- Захват экрана: {status.IsCaptureSupported}");
    Console.WriteLine($"- Симуляция ввода: {status.IsInputSupported}");  
    Console.WriteLine($"- Хранилище шаблонов: {status.TemplateStorageAccessible}");
}

// Метрики производительности
var metrics = await automation.GetPerformanceMetricsAsync();
Console.WriteLine($"Среднее время захвата: {metrics.AverageCaptureTime.TotalMilliseconds}ms");
Console.WriteLine($"Среднее время поиска: {metrics.AverageMatchTime.TotalMilliseconds}ms");
Console.WriteLine($"Процент успеха: {metrics.SuccessRate:P}");
```

### Детальное логирование

```csharp
using var automation = AutomationClient.Create(options =>
{
    options.Logging = new LoggingConfiguration
    {
        EnableVerboseLogging = true,
        LogPerformanceMetrics = true,
        LogCaptureEvents = true,
        LogInputEvents = true,
        LogFilePath = @"C:\Logs\automation.log"
    };
});

// Все операции теперь подробно логируются
await automation.ClickOnImageAsync("test_button");
```

## 🧪 Тестирование автоматизации

### Unit тесты для workflow

```csharp
[Test]
public async Task LoginWorkflow_ShouldSucceed()
{
    // Arrange
    using var automation = AutomationClient.Create(options =>
    {
        options.TemplatesPath = @"TestData\Templates";
    });

    // Act
    var result = await automation.CreateWorkflow("Test Login")
        .WaitForImage("login_button", TimeSpan.FromSeconds(5))
        .ClickOnImage("login_button")
        .WaitForImage("username_field") 
        .Type("testuser")
        .PressKeys(VirtualKey.Tab)
        .Type("testpass")
        .PressKeys(VirtualKey.Return)
        .WaitForImage("dashboard", TimeSpan.FromSeconds(10))
        .ExecuteAsync();

    // Assert
    Assert.IsTrue(result.Success);
    Assert.AreEqual(6, result.CompletedSteps.Count);
    Assert.IsTrue(result.Duration.TotalSeconds < 20);
}
```

### Интеграционные тесты

```csharp
[Test]
public async Task FullApplicationTest_ShouldWork()
{
    using var automation = AutomationClient.Create();
    
    // Запуск приложения
    Process.Start("MyTestApp.exe");
    await Task.Delay(TimeSpan.FromSeconds(3));
    
    // Тестирование основного сценария
    var mainResult = await automation.CreateWorkflow("Main Scenario")
        .WaitForImage("app_loaded", TimeSpan.FromSeconds(10))
        .ClickOnImage("new_document")
        .Type("Hello, World!")
        .PressKeys(VirtualKey.Control, VirtualKey.S)
        .WaitForImage("save_dialog")
        .Type("test_document.txt")
        .PressKeys(VirtualKey.Return)
        .WaitForImage("document_saved")
        .ExecuteAsync();
    
    Assert.IsTrue(mainResult.Success);
    
    // Проверка сохранения
    Assert.IsTrue(File.Exists("test_document.txt"));
}
```

## 🚀 Производительность

### Быстрый режим для CI/CD

```csharp
using var automation = AutomationClient.Create(AutomationOptions.HighPerformance);

// Отключение всех замедляющих "человеческих" эффектов
await automation.ClickOnImageAsync("button");  // ~10ms вместо 500ms
await automation.TypeAsync("text");            // ~50ms вместо 2s
```

### Пакетная обработка

```csharp
// Обработка множества элементов
var imageSearch = automation.Images;
var searchOptions = ImageSearchOptions.Default.WithThreshold(0.9);

// Поиск всех кнопок одновременно
var allButtons = await imageSearch.FindAllAsync("generic_button", searchOptions);
Console.WriteLine($"Найдено {allButtons.Count} кнопок");

// Клик по всем найденным кнопкам
foreach (var button in allButtons.AllLocations)
{
    await automation.Input.Mouse.ClickAsync(MouseButton.Left);
    await Task.Delay(100); // Небольшая пауза между кликами
}
```

## 🎨 Пользовательский интерфейс

### Визуальная отладка

```csharp
using var automation = AutomationClient.Create();

// Поиск с подсветкой найденных областей (для отладки)
var searchResult = await automation.Images.FindAsync("target_image");
if (searchResult.IsFound)
{
    // В режиме отладки можно включить визуальную подсветку
    automation.ShowHighlight(searchResult.Location.Bounds, Color.Green, 2000);
}
```

Эти примеры показывают, насколько гибкой и мощной стала новая архитектура AutomationCore! 🎉