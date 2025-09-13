# План миграции AutomationCore

## 📊 Текущее состояние

### ✅ Создано (новая архитектура):
- Core/Models/ - Типизированные модели предметной области
- Core/Abstractions/ - Чистые интерфейсы без зависимостей  
- Core/Exceptions/ - Иерархия исключений
- Infrastructure/Platform/ - Изолированный P/Invoke слой
- Services/ - Бизнес-логика (Windows, Capture, Matching, Input)
- Features/ - Высокоуровневые компоненты
- Public/ - Простой API для пользователей

### ❌ Требует миграции (старый код):
- EnhancedScreenCapture.cs (1000+ строк монстр)
- Assets/FlatFileTemplateStore.cs → Infrastructure/Storage/
- Input/HumanizedInput.cs → Services/Input/ 
- UI/ → Infrastructure/UI/
- Старые интерфейсы в Core/Abstractions/

## 🔄 План миграции (по приоритету)

### Фаза 1: Перенос шаблонов (КРИТИЧНО)
1. **ДЕЙСТВИЕ**: Создать `Infrastructure/Storage/FileTemplateStorage.cs`
2. **ИСТОЧНИК**: `Assets/FlatFileTemplateStore.cs`
3. **ИЗМЕНЕНИЯ**:
   - Реализовать интерфейс `ITemplateStorage`
   - Убрать OpenCV зависимости из Storage слоя
   - Добавить async/await паттерны

```bash
# Команды:
1. Скопировать Assets/FlatFileTemplateStore.cs → Infrastructure/Storage/FileTemplateStorage.cs
2. Обновить namespace на AutomationCore.Infrastructure.Storage
3. Заменить ITemplateStore на ITemplateStorage
4. Заменить Mat GetTemplate() на ValueTask<TemplateData> LoadAsync()
```

### Фаза 2: Перенос WGC захвата
1. **ДЕЙСТВИЕ**: Создать `Infrastructure/Capture/WindowsGraphicsCapture/WgcDevice.cs`
2. **ИСТОЧНИК**: Логика из `Capture/WindowsGraphicsCapture.cs`
3. **ИЗМЕНЕНИЯ**:
   - Разбить монолитный класс на: WgcDevice, WgcSession, WgcFramePool
   - Реализовать ICaptureDevice интерфейс
   - Убрать бизнес-логику (оставить только D3D/WinRT)

```bash
# Команды:
1. Создать Infrastructure/Capture/WindowsGraphicsCapture/
2. Разбить EnhancedWindowsGraphicsCapture на компоненты
3. Сохранить только низкоуровневую логику WGC
```

### Фаза 3: Перенос симуляции ввода
1. **ДЕЙСТВИЕ**: Переместить `Input/HumanizedInput.cs`
2. **НАЗНАЧЕНИЕ**: `Infrastructure/Input/WindowsInputProvider.cs`
3. **ИЗМЕНЕНИЯ**:
   - Реализовать IPlatformInputProvider
   - Вынести алгоритмы в Services/Input/

```bash
# Команды:
1. Переместить Input/ → Infrastructure/Input/
2. Обновить интерфейсы
3. Перенести человекоподобные алгоритмы в сервисы
```

### Фаза 4: Обновление UI слоя
1. **ДЕЙСТВИЕ**: `UI/ → Infrastructure/UI/`
2. **ИЗМЕНЕНИЯ**: Минимальные, только namespace

### Фаза 5: Удаление старых файлов
1. **УДАЛИТЬ**:
   - `Core/EnhancedScreenCapture.cs` (заменен сервисами)
   - Старые файлы из Core/Abstractions/
   - Дублирующиеся классы

## 📝 Пошаговые команды миграции

### Шаг 1: Создание FileTemplateStorage.cs

**Создайте файл** `Infrastructure/Storage/FileTemplateStorage.cs`:

```csharp
// Infrastructure/Storage/FileTemplateStorage.cs
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AutomationCore.Core.Abstractions;
using AutomationCore.Core.Models;

namespace AutomationCore.Infrastructure.Storage
{
    public sealed class FileTemplateStorage : ITemplateStorage
    {
        private readonly string _basePath;
        private readonly ConcurrentDictionary<string, TemplateData> _cache = new();
        private readonly FileSystemWatcher _watcher;
        
        // Перенести логику из FlatFileTemplateStore.cs
        // с адаптацией под новые интерфейсы
    }
}
```

### Шаг 2: Обновление ServiceCollectionExtensions.cs

**Замените заглушку** в `ServiceCollectionExtensions.cs`:

```csharp
// Было:
services.AddSingleton<ITemplateStorage>(provider => new InMemoryTemplateStorage());

// Стало:
services.AddSingleton<ITemplateStorage>(provider =>
{
    var options = provider.GetRequiredService<AutomationOptions>();
    return new FileTemplateStorage(options.TemplatesPath);
});
```

### Шаг 3: Создание OpenCvMatchingEngine.cs

**Создайте файл** `Infrastructure/Matching/OpenCvMatchingEngine.cs`:

```csharp
// Infrastructure/Matching/OpenCvMatchingEngine.cs  
using OpenCvSharp;
using AutomationCore.Core.Abstractions;
using AutomationCore.Services.Matching;

namespace AutomationCore.Infrastructure.Matching
{
    internal sealed class OpenCvMatchingEngine : IMatchingEngine
    {
        // Перенести чистые алгоритмы OpenCV из старых классов
        // Убрать всё кроме Cv2.MatchTemplate логики
    }
}
```

### Шаг 4: Обновление старых файлов

**Добавьте атрибуты устаревания**:

```csharp
// В начало EnhancedScreenCapture.cs:
[Obsolete("Use AutomationClient.Create() instead. This class will be removed in v2.0.")]
public class EnhancedScreenCapture : IDisposable
{
    // существующий код...
}
```

### Шаг 5: Создание миграционного помощника

**Создайте файл** `Public/Migration/LegacyAdapter.cs`:

```csharp
// Public/Migration/LegacyAdapter.cs
using AutomationCore.Public.Facades;

namespace AutomationCore.Public.Migration
{
    /// <summary>
    /// Адаптер для плавной миграции со старого API
    /// </summary>
    [Obsolete("Use AutomationClient directly. Will be removed in v2.0.")]
    public static class LegacyAdapter
    {
        public static AutomationClient CreateFromLegacySettings()
        {
            return AutomationClient.Create(options =>
            {
                // Маппинг старых настроек на новые
                options.TemplatesPath = "./assets/templates";
            });
        }
    }
}
```

## 🧪 План тестирования миграции

### Тест 1: Базовый сценарий
```csharp
[Test]
public async Task BasicScenario_ShouldWork()
{
    using var client = AutomationClient.Create();
    
    var found = await client.ClickOnImageAsync("test_button");
    Assert.IsTrue(found);
}
```

### Тест 2: Workflow сценарий
```csharp
[Test] 
public async Task WorkflowScenario_ShouldWork()
{
    using var client = AutomationClient.Create();
    
    var result = await client.CreateWorkflow("Test")
        .WaitForImage("login_form")
        .Type("username")
        .PressKeys(VirtualKey.Tab)
        .Type("password")
        .ClickOnImage("login_button")
        .ExecuteAsync();
        
    Assert.IsTrue(result.Success);
}
```

## 📅 Временные рамки

- **Фаза 1** (FileTemplateStorage): 1 день
- **Фаза 2** (WGC Device): 2 дня  
- **Фаза 3** (Input Provider): 1 день
- **Фаза 4** (UI Update): 0.5 дня
- **Фаза 5** (Cleanup): 0.5 дня
- **Тестирование**: 1 день

**Общее время**: ~6 дней

## ⚠️ Риски и Решения

### Риск: Ломающие изменения
**Решение**: Сохранить старое API до v2.0 с атрибутами [Obsolete]

### Риск: Производительность
**Решение**: Benchmark тесты до/после миграции

### Риск: Сложные зависимости
**Решение**: Поэтапная миграция с промежуточными адаптерами

## ✅ Критерии завершения

1. ✅ Все тесты проходят
2. ✅ Производительность не хуже старой версии  
3. ✅ Старое API работает через адаптеры
4. ✅ Документация обновлена
5. ✅ Примеры переписаны на новый API

## 🚀 Готовы к миграции!

После завершения у нас будет:
- Чистая архитектура по SOLID
- Модульная система
- Простой публичный API
- Полная обратная совместимость
- Отличная производительность