# AutomationCore Project Structure

## Обзор архитектуры

AutomationCore использует Clean Architecture с Domain-Driven Design для обеспечения четкого разделения ответственности и легкости сопровождения.

```
AutomationCore/
├── Core/                    # Ядро библиотеки - бизнес-логика
│   ├── Domain/             # Доменная логика (основная бизнес-логика)
│   ├── Abstractions/       # Интерфейсы и контракты
│   ├── Models/            # Общие модели данных
│   └── Configuration/     # Настройка DI контейнера
├── Infrastructure/         # Внешние зависимости и платформенная логика
│   ├── Capture/           # Реализации захвата экрана
│   ├── Input/             # Реализации симуляции ввода
│   ├── Storage/           # Реализации хранения данных
│   └── Platform/          # Платформо-специфичная логика
├── Public/                # Публичный API библиотеки
│   ├── Extensions/        # Extension methods для удобства
│   └── Builders/         # Builder pattern для конфигурации
├── UI/                   # UI компоненты (overlay, debugging)
├── Features/             # Высокоуровневые фичи
├── docs/                 # Документация
└── tests/               # Тесты (структура повторяет основную)
```

---

## Детальная структура Core

### Core/Domain - Доменная логика

Каждый домен содержит полный набор компонентов для своей области ответственности:

```
Core/Domain/
├── Capture/              # Домен захвата экрана/окон
│   ├── Commands/         # Команды захвата
│   │   ├── CaptureWindowCommand.cs
│   │   ├── CaptureScreenCommand.cs
│   │   └── CaptureRegionCommand.cs
│   ├── Models/           # Модели домена
│   │   ├── CaptureFrame.cs
│   │   ├── CaptureTarget.cs
│   │   ├── CaptureOptions.cs
│   │   └── CaptureMetrics.cs
│   ├── Services/         # Доменные сервисы
│   │   ├── CaptureOrchestrator.cs
│   │   └── FrameProcessor.cs
│   └── Events/           # Доменные события
│       ├── FrameCapturedEvent.cs
│       └── CaptureStartedEvent.cs
├── Input/                # Домен симуляции ввода
│   ├── Commands/
│   │   ├── ClickCommand.cs
│   │   ├── TypeTextCommand.cs
│   │   ├── KeyPressCommand.cs
│   │   └── MouseMoveCommand.cs
│   ├── Models/
│   │   ├── InputResult.cs
│   │   ├── MouseButton.cs
│   │   ├── KeyboardKey.cs
│   │   └── InputOptions.cs
│   ├── Services/
│   │   ├── InputValidator.cs
│   │   └── InputSequencer.cs
│   └── Events/
│       └── InputExecutedEvent.cs
├── Matching/             # Домен сопоставления шаблонов
│   ├── Commands/
│   │   ├── FindTemplateCommand.cs
│   │   ├── CompareImagesCommand.cs
│   │   └── CacheTemplateCommand.cs
│   ├── Models/
│   │   ├── Template.cs
│   │   ├── MatchResult.cs
│   │   ├── MatchOptions.cs
│   │   └── MatchScore.cs
│   ├── Services/
│   │   ├── TemplateMatchingEngine.cs
│   │   ├── ImagePreprocessor.cs
│   │   └── ScaleProcessor.cs
│   └── Events/
│       └── TemplateMatchedEvent.cs
├── Windows/              # Домен управления окнами
│   ├── Commands/
│   │   ├── FindWindowCommand.cs
│   │   ├── MoveWindowCommand.cs
│   │   └── ResizeWindowCommand.cs
│   ├── Models/
│   │   ├── WindowHandle.cs
│   │   ├── WindowInfo.cs
│   │   ├── WindowState.cs
│   │   └── WindowOperation.cs
│   ├── Services/
│   │   ├── WindowEnumerator.cs
│   │   ├── WindowValidator.cs
│   │   └── WindowManager.cs
│   └── Events/
│       ├── WindowFoundEvent.cs
│       └── WindowStateChangedEvent.cs
└── Workflows/            # Домен автоматизационных workflow'ов
    ├── Commands/
    │   ├── ExecuteWorkflowCommand.cs
    │   └── ValidateWorkflowCommand.cs
    ├── Models/
    │   ├── Workflow.cs
    │   ├── WorkflowStep.cs
    │   ├── WorkflowContext.cs
    │   └── WorkflowResult.cs
    ├── Services/
    │   ├── WorkflowEngine.cs
    │   ├── StepExecutor.cs
    │   └── WorkflowValidator.cs
    └── Events/
        ├── WorkflowStartedEvent.cs
        ├── WorkflowCompletedEvent.cs
        └── StepExecutedEvent.cs
```

### Core/Abstractions - Интерфейсы

```
Core/Abstractions/
├── Capture/
│   ├── ICaptureService.cs          # Основной сервис захвата
│   ├── ICaptureStrategy.cs         # Стратегия захвата (WGC, GDI+, etc.)
│   ├── ICaptureSession.cs          # Сессия захвата для потоковых операций
│   └── ICaptureFactory.cs          # Фабрика создания capture объектов
├── Input/
│   ├── IInputSimulator.cs          # Основной интерфейс симуляции ввода
│   ├── IMouseSimulator.cs          # Симуляция мыши
│   ├── IKeyboardSimulator.cs       # Симуляция клавиатуры
│   └── IInputValidator.cs          # Валидация input операций
├── Matching/
│   ├── ITemplateMatcherService.cs  # Основной сервис матчинга
│   ├── IMatchingEngine.cs          # Движок матчинга (OpenCV wrapper)
│   ├── IPreprocessor.cs            # Предобработка изображений
│   └── ITemplateStorage.cs         # Хранилище шаблонов
├── Storage/
│   ├── IRepository.cs<T>           # Базовый репозиторий
│   ├── ITemplateRepository.cs      # Репозиторий шаблонов
│   ├── IConfigRepository.cs        # Репозиторий конфигурации
│   └── ICacheService.cs<T>         # Сервис кеширования
├── Platform/
│   ├── IPlatformService.cs         # Платформо-специфичные операции
│   ├── IWindowOperations.cs        # Операции с окнами
│   └── ISystemInfo.cs              # Системная информация
├── Events/
│   ├── IEventPublisher.cs          # Публикация событий
│   ├── IEventSubscriber.cs         # Подписка на события
│   └── IEventStore.cs              # Хранилище событий
└── Commands/
    ├── ICommand.cs<TResult>        # Базовая команда
    ├── ICommandHandler.cs<T,R>     # Обработчик команд
    ├── ICommandValidator.cs<T>     # Валидатор команд
    └── ICommandQueue.cs            # Очередь команд
```

### Core/Models - Общие модели

```
Core/Models/
├── Common/
│   ├── Result.cs<T>               # Result pattern
│   ├── Point.cs                   # Координаты
│   ├── Rectangle.cs               # Прямоугольник
│   ├── Size.cs                    # Размеры
│   └── TimeRange.cs               # Временной диапазон
├── Configuration/
│   ├── AutomationOptions.cs       # Главные опции
│   ├── CaptureOptions.cs          # Опции захвата
│   ├── InputOptions.cs            # Опции ввода
│   ├── MatchingOptions.cs         # Опции матчинга
│   └── LoggingOptions.cs          # Опции логирования
├── Events/
│   ├── BaseEvent.cs               # Базовое событие
│   ├── AutomationEvent.cs         # Событие автоматизации
│   └── ErrorEvent.cs              # Событие ошибки
└── Exceptions/
    ├── AutomationException.cs     # Базовое исключение
    ├── CaptureException.cs        # Ошибки захвата
    ├── InputException.cs          # Ошибки ввода
    ├── MatchingException.cs       # Ошибки матчинга
    └── ConfigurationException.cs  # Ошибки конфигурации
```

---

## Детальная структура Infrastructure

### Infrastructure/Capture - Реализации захвата

```
Infrastructure/Capture/
├── WindowsGraphicsCapture/
│   ├── WgcCaptureService.cs           # Основной сервис WGC
│   ├── WgcCaptureSession.cs           # WGC сессия
│   ├── WgcInterop.cs                  # WinRT interop код
│   └── WgcFrameProcessor.cs           # Обработка WGC фреймов
├── GdiCapture/
│   ├── GdiCaptureService.cs           # GDI+ захват
│   ├── GdiScreenCapture.cs            # Захват экрана через GDI
│   └── GdiWindowCapture.cs            # Захват окна через GDI
├── Shared/
│   ├── CaptureFrameConverter.cs       # Конвертация форматов
│   ├── ImageProcessor.cs              # Обработка изображений
│   └── CaptureMetricsCollector.cs     # Сбор метрик
└── Factories/
    ├── CaptureServiceFactory.cs       # Фабрика сервисов захвата
    └── CaptureStrategyFactory.cs      # Фабрика стратегий
```

### Infrastructure/Input - Реализации ввода

```
Infrastructure/Input/
├── Windows/
│   ├── WindowsInputProvider.cs        # Основной провайдер Win32
│   ├── Win32MouseSimulator.cs         # Мышь через SendInput
│   ├── Win32KeyboardSimulator.cs      # Клавиатура через SendInput
│   ├── Win32Interop.cs                # P/Invoke определения
│   └── VirtualKeyConverter.cs         # Конвертация клавиш
├── DirectInput/
│   ├── DirectInputProvider.cs         # DirectInput провайдер
│   └── DirectInputDevice.cs           # DI устройство
├── Shared/
│   ├── InputValidator.cs              # Валидация ввода
│   ├── InputThrottler.cs              # Ограничение частоты
│   └── InputMetrics.cs                # Метрики ввода
└── Factories/
    └── InputProviderFactory.cs        # Фабрика провайдеров
```

### Infrastructure/Storage - Реализации хранения

```
Infrastructure/Storage/
├── FileSystem/
│   ├── FileTemplateStorage.cs         # Файловое хранилище шаблонов
│   ├── FileConfigStorage.cs           # Файловое хранилище конфигурации
│   └── FileSystemWatcher.cs           # Мониторинг изменений
├── Memory/
│   ├── MemoryCache.cs<T>              # In-memory кеш
│   ├── MemoryTemplateCache.cs         # Кеш шаблонов
│   └── LruCache.cs<T>                 # LRU кеш
├── Database/
│   ├── SqliteRepository.cs<T>         # SQLite репозиторий
│   └── DatabaseMigrator.cs            # Миграции БД
└── Serialization/
    ├── JsonSerializer.cs<T>           # JSON сериализация
    ├── BinarySerializer.cs<T>         # Бинарная сериализация
    └── TemplateSerializer.cs          # Сериализация шаблонов
```

---

## Public API Structure

```
Public/
├── AutomationEngine.cs              # Главный entry point
├── Extensions/
│   ├── ServiceCollectionExtensions.cs  # DI регистрация
│   ├── WindowExtensions.cs              # Extensions для окон
│   ├── ImageExtensions.cs               # Extensions для изображений
│   └── CommandExtensions.cs             # Extensions для команд
├── Builders/
│   ├── AutomationBuilder.cs             # Основной builder
│   ├── CaptureBuilder.cs                # Builder для захвата
│   ├── InputBuilder.cs                  # Builder для ввода
│   ├── MatchingBuilder.cs               # Builder для матчинга
│   └── WorkflowBuilder.cs               # Builder для workflow
└── Fluent/
    ├── FluentAutomation.cs              # Fluent API entry point
    ├── FluentCapture.cs                 # Fluent API для захвата
    ├── FluentInput.cs                   # Fluent API для ввода
    └── FluentWorkflow.cs                # Fluent API для workflow
```

---

## Правила организации файлов

### 1. Один класс = один файл
```csharp
// ✅ Хорошо
// WindowsGraphicsCaptureService.cs
public sealed class WindowsGraphicsCaptureService : ICaptureService
{
    // Реализация
}

// ✅ Исключение для тесно связанных типов
// CaptureModels.cs
public record CaptureRequest(WindowHandle Window);
public record CaptureResult(CaptureFrame Frame);
```

### 2. Группировка по функциональности
```csharp
// ✅ Группировка команд одного домена
Core/Domain/Input/Commands/
├── ClickCommand.cs
├── TypeTextCommand.cs
├── KeyPressCommand.cs
└── MouseMoveCommand.cs

// ✅ Группировка моделей одного домена
Core/Domain/Input/Models/
├── InputResult.cs
├── MouseButton.cs
└── KeyboardKey.cs
```

### 3. Интерфейсы отдельно от реализаций
```csharp
// ✅ Интерфейсы в Abstractions
Core/Abstractions/Input/IInputSimulator.cs

// ✅ Реализации в Infrastructure
Infrastructure/Input/Windows/WindowsInputProvider.cs
```

### 4. Фабрики в отдельной папке
```csharp
// ✅ Все фабрики домена в одном месте
Infrastructure/Capture/Factories/
├── CaptureServiceFactory.cs
├── CaptureStrategyFactory.cs
└── CaptureSessionFactory.cs
```

---

## Добавление новой функциональности

### Добавление нового домена
1. Создать папку в `Core/Domain/{NewDomain}`
2. Добавить подпапки: `Commands/`, `Models/`, `Services/`, `Events/`
3. Создать интерфейсы в `Core/Abstractions/{NewDomain}/`
4. Создать реализации в `Infrastructure/{NewDomain}/`
5. Добавить регистрацию в `Core/Configuration/AutomationBuilder.cs`
6. Создать тесты в `tests/{NewDomain}/`

### Добавление новой команды
1. Создать класс команды в `Core/Domain/{Domain}/Commands/`
2. Наследовать от `IAutomationCommand<TResult>`
3. Добавить обработчик в соответствующий сервис
4. Создать unit тесты
5. Обновить документацию

### Добавление новой стратегии
1. Создать интерфейс стратегии в `Core/Abstractions/`
2. Создать реализации в `Infrastructure/`
3. Обновить фабрику для выбора стратегии
4. Добавить конфигурацию в `AutomationBuilder`
5. Создать integration тесты

---

## Заключение

Эта структура обеспечивает:

- **Четкое разделение ответственности** между доменами
- **Легкость тестирования** через интерфейсы и DI
- **Простоту расширения** через паттерны и соглашения
- **Поддерживаемость** через консистентную организацию
- **Переиспользование** кода между доменами

При добавлении новой функциональности всегда следуйте этой структуре и паттернам.