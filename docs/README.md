# AutomationCore Documentation

## Обзор

Добро пожаловать в документацию AutomationCore - библиотеки автоматизации Windows с чистой архитектурой и современными паттернами проектирования.

## 📋 Содержание документации

### 🏗️ Архитектура
- **[ADR-001: Core Architecture Patterns](./architecture/ADR-001-Core-Architecture-Patterns.md)** - Архитектурные решения и обоснования
- **[Automation Library Patterns](./architecture/Automation-Library-Patterns.md)** - Специфичные паттерны для автоматизации

### 📏 Стандарты кодирования
- **[Coding Standards](./standards/Coding-Standards.md)** - Обязательные стандарты кодирования для проекта

### 📁 Структура проекта
- **[Project Structure](./structure/Project-Structure.md)** - Детальное описание организации кода

## 🎯 Быстрый старт для разработчиков

### Новый разработчик в проекте
1. Прочитайте [ADR-001](./architecture/ADR-001-Core-Architecture-Patterns.md) для понимания архитектурных решений
2. Изучите [Coding Standards](./standards/Coding-Standards.md) - они обязательны к соблюдению
3. Ознакомьтесь с [Project Structure](./structure/Project-Structure.md) для навигации по коду

### Добавление новой функциональности
1. **Определите домен** - куда относится ваша функциональность (Capture, Input, Matching, Windows, Workflows)
2. **Следуйте паттернам** - используйте Command Pattern, Strategy Pattern и другие из [Automation Library Patterns](./architecture/Automation-Library-Patterns.md)
3. **Соблюдайте структуру** - создавайте файлы согласно [Project Structure](./structure/Project-Structure.md)
4. **Пишите тесты** - каждая новая функциональность должна быть покрыта тестами

### Code Review
При проведении code review проверяйте:
- ✅ Соблюдение [Coding Standards](./standards/Coding-Standards.md)
- ✅ Следование архитектурным паттернам из [ADR-001](./architecture/ADR-001-Core-Architecture-Patterns.md)
- ✅ Правильное размещение файлов согласно [Project Structure](./structure/Project-Structure.md)
- ✅ Наличие unit тестов
- ✅ XML документация для публичных API

## 🔧 Архитектурные принципы

### Domain-Driven Design
Проект организован по доменам:
- **Capture** - захват экрана и окон
- **Input** - симуляция пользовательского ввода
- **Matching** - сопоставление изображений и шаблонов
- **Windows** - управление окнами системы
- **Workflows** - автоматизационные сценарии

### Clean Architecture
- **Core** - бизнес-логика, не зависит от внешних технологий
- **Infrastructure** - реализации, зависящие от конкретных технологий
- **Public** - публичный API библиотеки

### Ключевые паттерны
- **Command Pattern** - для всех операций автоматизации
- **Strategy Pattern** - для разных реализаций (WGC vs GDI, SendInput vs DirectInput)
- **Repository Pattern** - для работы с данными
- **Result Pattern** - для обработки ошибок без исключений

## 📈 Развитие проекта

### Планируемые фичи
Согласно архитектурным решениям, в будущем планируется добавить:
- Команды запуска приложений
- Выполнение скриптов
- Расширенные workflow с условной логикой
- Поддержка Linux/macOS через стратегии

### Как добавить новый домен
Подробные инструкции в [Project Structure](./structure/Project-Structure.md#добавление-новой-функциональности).

### Как предложить изменения в архитектуру
1. Создайте новый ADR документ в папке `docs/architecture/`
2. Обсудите изменения с командой
3. Обновите соответствующую документацию

## 🧪 Тестирование

### Стратегия тестирования
- **Unit тесты** - для всех команд и сервисов в изоляции
- **Integration тесты** - для взаимодействия между доменами
- **End-to-end тесты** - для полных автоматизационных сценариев

### Именование тестов
Следуйте паттерну `Subject_Scenario_ExpectedResult` из [Coding Standards](./standards/Coding-Standards.md).

## 🚀 Производительность

### Ключевые требования
- Минимизация аллокаций через `ValueTask`, `ArrayPool`, `StringPool`
- Правильное управление ресурсами через `IDisposable`
- Эффективное кеширование шаблонов и конфигурации
- Асинхронность везде с правильным использованием `ConfigureAwait(false)`

## 📞 Поддержка

### Вопросы по архитектуре
Обратитесь к [ADR-001](./architecture/ADR-001-Core-Architecture-Patterns.md) и [Automation Library Patterns](./architecture/Automation-Library-Patterns.md).

### Вопросы по стилю кода
Все ответы в [Coding Standards](./standards/Coding-Standards.md).

### Вопросы по структуре
Подробное описание в [Project Structure](./structure/Project-Structure.md).

---

## 📝 История изменений документации

| Дата | Документ | Изменения |
|------|----------|-----------|
| 2025-09-14 | Все документы | Первоначальная версия архитектурной документации |

---

**Помните**: Цель этой документации - предотвратить деградацию кода и обеспечить консистентность развития проекта. Всегда следуйте этим стандартам и паттернам!