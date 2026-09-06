# Участие в разработке RimWatch

Спасибо за интерес к проекту RimWatch! Мы рады любому вкладу в развитие мода.

## 🧭 Planning Workflow (OpenSpec-First)

- Для нетривиальных изменений используем OpenSpec-first процесс.
- Обязательные артефакты до начала реализации: `proposal`, `design`, `specs`, `tasks`.
- Для tiny-fix допустимо исключение (без полного change-цикла), но нужно оставить trace note в OpenSpec planning registry.
- Авторитетный planning source: `openspec/planning/PLANNING_REGISTRY.md`.
- `ROADMAP.md` используется как стратегический индекс и не является authoritative task backlog.
- Текущий процесс и quality gates: `docs/OPENSPEC_WORKFLOW.md`.

## 🎯 Как помочь

### 1. Программирование
- Реализация новых стратегий ИИ
- Оптимизация производительности
- Исправление багов
- Написание тестов

### 2. Тестирование
- Игра с модом и репорт багов
- Тестирование совместимости
- Проверка производительности

### 3. Документация
- Написание гайдов
- Улучшение README
- Перевод на другие языки

### 4. Дизайн
- UI/UX для интерфейса
- Иконки и графика
- Визуализация данных

## 🔧 Начало работы

### Требования
- Git
- Docker (для сборки)
- RimWorld 1.6

### Настройка среды разработки

1. Форкни репозиторий
2. Клонируй свой форк:
```bash
git clone https://github.com/YOUR_USERNAME/RimWatch.git
cd RimWatch
```

3. Собери проект с Docker:
```bash
# Будет добавлено позже
# make build
```

4. Создай ветку для своих изменений:
```bash
git checkout -b feature/my-awesome-feature
```

## 📝 Стандарты кодирования

### Общие правила
- Используй **осмысленные имена** для переменных и методов
- Пиши **комментарии** для сложной логики
- Следуй **StyleCop** правилам (настроены в проекте)
- Добавляй **тесты** для новой функциональности

### Стиль кода

```csharp
// ✅ ХОРОШО
public class WorkStrategy
{
    private readonly ColonyAnalyzer _analyzer;
    
    public WorkStrategy(ColonyAnalyzer analyzer)
    {
        _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
    }
    
    public void AssignPriorities()
    {
        var colonists = _analyzer.GetColonists();
        foreach (var colonist in colonists)
        {
            // Логика назначения приоритетов
        }
    }
}

// ❌ ПЛОХО
public class workstrat
{
    public void DoStuff()
    {
        var a = GetData();
        for(int i=0;i<a.Count;i++){
            // magic happens
        }
    }
}
```

### Логирование

Используй `RimWatchLogger` для всех логов:

```csharp
// ✅ ХОРОШО
RimWatchLogger.Info("Work priorities assigned successfully");
RimWatchLogger.Warning($"Colonist {pawn.Name} has low mood: {mood}");
RimWatchLogger.Error("Failed to assign priorities", exception);

// ❌ ПЛОХО
Log.Message("something happened"); // Слишком общее
Console.WriteLine("Debug info"); // Не работает в RimWorld
```

### Производительность

- **Избегай** выделения памяти в горячих путях
- **Кэшируй** результаты дорогих операций
- **Используй** object pooling для часто создаваемых объектов
- **Профилируй** перед оптимизацией

```csharp
// ✅ ХОРОШО - кэширование
private List<Pawn>? _cachedColonists;
private int _lastUpdateTick;

public List<Pawn> GetColonists()
{
    if (_cachedColonists == null || Find.TickManager.TicksGame - _lastUpdateTick > 60)
    {
        _cachedColonists = Find.CurrentMap.mapPawns.FreeColonists.ToList();
        _lastUpdateTick = Find.TickManager.TicksGame;
    }
    return _cachedColonists;
}

// ❌ ПЛОХО - каждый вызов пересчитывает
public List<Pawn> GetColonists()
{
    return Find.CurrentMap.mapPawns.FreeColonists.ToList();
}
```

## 🧪 Тестирование

### Написание тестов

Используй xUnit для тестов:

```csharp
public class WorkStrategyTests
{
    [Fact]
    public void AssignPriorities_WithValidColonists_ShouldSucceed()
    {
        // Arrange
        var analyzer = new MockColonyAnalyzer();
        var strategy = new WorkStrategy(analyzer);
        
        // Act
        strategy.AssignPriorities();
        
        // Assert
        Assert.True(strategy.PrioritiesAssigned);
    }
}
```

### Запуск тестов

```bash
# Будет добавлено позже
# make test
```

## 📤 Отправка изменений

### Процесс Pull Request

1. **Убедись** что код компилируется без ошибок
2. **Запусти** все тесты
3. **Проверь** StyleCop warnings
4. **Закоммить** изменения с осмысленным сообщением
5. **Push** в свой форк
6. **Создай** Pull Request

### Сообщения коммитов

Используй conventional commits:

```bash
# Типы коммитов:
# feat: новая функция
# fix: исправление бага
# docs: изменения документации
# style: форматирование кода
# refactor: рефакторинг без изменения функциональности
# test: добавление тестов
# chore: обновление конфигурации, зависимостей и т.д.

✅ ХОРОШО:
feat: add work priority assignment strategy
fix: correct colonist mood calculation
docs: update README with installation instructions

❌ ПЛОХО:
updated files
fix bug
changes
```

### Описание Pull Request

Используй шаблон:

```markdown
## Описание
Краткое описание изменений

## Тип изменений
- [ ] Исправление бага
- [ ] Новая функция
- [ ] Улучшение производительности
- [ ] Документация
- [ ] Другое

## Тестирование
Опиши как ты тестировал изменения

## Скриншоты (если применимо)

## Checklist
- [ ] Код компилируется без ошибок
- [ ] Все тесты проходят
- [ ] Добавлены новые тесты (если нужно)
- [ ] Документация обновлена (если нужно)
- [ ] StyleCop warnings исправлены
```

## 🐛 Репорт багов

### Информация для репорта

При создании issue включи:

1. **Версия RimWorld:** (например, 1.6.4630)
2. **Версия RimWatch:** (например, 0.5.0)
3. **Список модов:** Используй [RimPy](https://github.com/rimpy-custom/RimPy)
4. **Описание бага:** Что произошло и что ожидалось
5. **Шаги воспроизведения:** Как повторить баг
6. **Логи:** HugsLib (Ctrl+F12) или Player.log
7. **Скриншоты:** Если применимо

### Шаблон issue

```markdown
**Версия RimWorld:** 1.6.4630
**Версия RimWatch:** 0.5.0

**Описание:**
ИИ не назначает приоритеты работ после загрузки сохранения

**Шаги воспроизведения:**
1. Включить режим "Менеджер"
2. Сохранить игру
3. Загрузить сохранение
4. Приоритеты не обновляются

**Ожидаемое поведение:**
Приоритеты должны обновиться после загрузки

**Логи:**
[Прикрепить Player.log или HugsLib лог]

**Скриншоты:**
[Если есть]

**Список модов:**
[Список модов из RimPy]
```

## 💡 Предложения функций

Перед созданием issue с предложением функции:

1. **Проверь** [ROADMAP.md](ROADMAP.md) - возможно это уже запланировано
2. **Проверь** существующие issues - возможно кто-то уже предложил это
3. **Опиши** детально что и зачем
4. **Объясни** как это улучшит мод

### Шаблон предложения

```markdown
**Название функции:** Автоматическое управление зонами

**Описание:**
ИИ должен автоматически создавать и управлять зонами для животных, складирования и т.д.

**Обоснование:**
Упростит управление колонией, особенно при большом количестве животных

**Предлагаемая реализация:**
1. Анализ типов животных
2. Создание зон в подходящих местах
3. Динамическое изменение размеров зон

**Альтернативы:**
Возможно просто уведомлять игрока о необходимости создания зон

**Дополнительный контекст:**
[Скриншоты, примеры и т.д.]
```

## 📚 Полезные ресурсы

### Документация RimWorld
- [Official Modding Wiki](https://rimworldwiki.com/wiki/Modding_Tutorials)
- [RimWorld Discord](https://discord.gg/rimworld)
- [Ludeon Forums](https://ludeon.com/forums/)

### Инструменты
- [RimPy](https://github.com/rimpy-custom/RimPy) - Менеджер модов
- [HugsLib](https://github.com/UnlimitedHugs/RimworldHugsLib) - Библиотека для модов
- [Harmony](https://harmony.pardeike.net/) - Патчинг методов

### Обучение
- [C# Documentation](https://docs.microsoft.com/en-us/dotnet/csharp/)
- [Harmony Documentation](https://harmony.pardeike.net/articles/intro.html)
- [RimWorld Modding Examples](https://github.com/Mehni/ExampleMod)

## 🤝 Кодекс поведения

### Наши стандарты

- **Уважай** других участников
- **Будь конструктивен** в критике
- **Помогай** новичкам
- **Открыт** к разным точкам зрения

### Недопустимо

- Оскорбления и личные атаки
- Харассмент любого вида
- Публикация личной информации
- Спам и троллинг

## 📞 Связь

- **GitHub Issues:** Для багов и предложений
- **GitHub Discussions:** Для обсуждений
- **Discord:** (будет создан позже)

## ❓ Вопросы?

Если у тебя есть вопросы:

1. Проверь [README.md](README.md)
2. Проверь [ROADMAP.md](ROADMAP.md)
3. Проверь существующие issues
4. Создай новый issue с вопросом

---

**Спасибо за вклад в RimWatch!** 🤖❤️

