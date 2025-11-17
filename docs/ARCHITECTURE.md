# Архитектура RimWatch

## 🏗️ Обзор

RimWatch построен на модульной архитектуре с четким разделением ответственности. Каждый компонент выполняет свою специфическую задачу и взаимодействует с другими через определенные интерфейсы.

## 📊 Диаграмма компонентов

```
┌─────────────────────────────────────────────────────────────┐
│                         RimWatch Mod                         │
│                       (Entry Point)                          │
└────────────────────────┬────────────────────────────────────┘
                         │
        ┌────────────────┼────────────────┐
        │                │                │
        ▼                ▼                ▼
┌──────────────┐ ┌──────────────┐ ┌──────────────┐
│   Settings   │ │   AI Core    │ │   UI Layer   │
│              │ │              │ │              │
└──────────────┘ └──────┬───────┘ └──────────────┘
                        │
        ┌───────────────┼───────────────┐
        │               │               │
        ▼               ▼               ▼
┌──────────────┐ ┌──────────────┐ ┌──────────────┐
│  Analyzers   │ │  Strategies  │ │   Patches    │
│              │ │              │ │              │
└──────────────┘ └──────────────┘ └──────────────┘
        │               │               │
        └───────────────┼───────────────┘
                        │
                        ▼
                ┌──────────────┐
                │  RimWorld    │
                │     API      │
                └──────────────┘
```

## 🔧 Основные компоненты

### 1. RimWatchMod (Entry Point)

**Расположение:** `Source/RimWatch/RimWatchMod.cs`

**Ответственность:**
- Инициализация мода
- Загрузка настроек
- Применение Harmony патчей
- Координация компонентов

**Ключевые методы:**
```csharp
public RimWatchMod(ModContentPack content)  // Конструктор
public override void DoSettingsWindowContents(Rect inRect)  // UI настроек
public override string SettingsCategory()   // Название в меню
```

### 2. AI Core (Ядро ИИ)

**Расположение:** `Source/RimWatch/AI/`

#### 2.1 DecisionEngine

**Ответственность:**
- Центральный координатор принятия решений
- Выбор активных стратегий
- Приоритизация действий
- Обработка ошибок

**Основной цикл:**
```
1. Получить данные от Analyzers
2. Оценить текущую ситуацию
3. Выбрать подходящую стратегию
4. Сформировать план действий
5. Выполнить действия через Patches
6. Записать результаты для обучения
```

#### 2.2 ColonyAnalyzer

**Ответственность:**
- Сбор данных о состоянии колонии
- Анализ ресурсов
- Оценка угроз
- Определение приоритетов

**Собираемые данные:**
- Список колонистов и их состояния
- Запасы ресурсов
- Незавершенные постройки
- Угрозы и враги
- Текущие работы

#### 2.3 ActionPlanner

**Ответственность:**
- Построение плана действий
- Оптимизация последовательности
- Проверка выполнимости
- Адаптация к изменениям

**Типы действий:**
- Назначение работ
- Строительство
- Торговля
- Боевые действия
- Медицинская помощь

### 3. Strategies (Стратегии)

**Расположение:** `Source/RimWatch/AI/Strategies/`

Каждая стратегия отвечает за определенную область управления колонией.

#### Базовый интерфейс стратегии:

```csharp
public interface IStrategy
{
    string Name { get; }
    int Priority { get; }
    bool CanExecute(ColonyState state);
    ActionPlan CreatePlan(ColonyState state);
    void Execute(ActionPlan plan);
}
```

#### 3.1 WorkStrategy

**Приоритет:** Высокий (1)

**Цели:**
- Назначение приоритетов работ
- Балансировка нагрузки
- Учет навыков и здоровья

**Логика:**
```
1. Получить список колонистов
2. Оценить их навыки и состояние
3. Определить критические работы
4. Распределить приоритеты
5. Применить изменения
```

#### 3.2 BuildStrategy

**Приоритет:** Средний (2)

**Цели:**
- Планирование построек
- Оптимальное размещение
- Управление ресурсами

**Логика:**
```
1. Оценить потребности (спальни, склады, etc)
2. Найти оптимальное место
3. Проверить доступность ресурсов
4. Создать план строительства
5. Установить приоритеты
```

#### 3.3 FarmingStrategy

**Приоритет:** Средний (3)

**Цели:**
- Планирование полей
- Выбор культур
- Управление животными

**Логика:**
```
1. Оценить запасы еды
2. Учесть сезон и климат
3. Спланировать поля
4. Назначить фермеров
5. Контролировать урожай
```

#### 3.4 DefenseStrategy (v1.0+)

**Приоритет:** Критический (0)

**Цели:**
- Расстановка в бою
- Управление турелями
- Тактические решения

#### 3.5 TradeStrategy (v1.0+)

**Приоритет:** Низкий (4)

**Цели:**
- Управление запасами
- Караваны
- Оптимизация сделок

### 4. Analyzers (Анализаторы)

**Расположение:** `Source/RimWatch/AI/Analyzers/`

#### 4.1 ColonyAnalyzer

Главный анализатор состояния колонии.

**Методы:**
```csharp
public List<Pawn> GetColonists()
public ResourceState GetResources()
public List<Threat> GetThreats()
public ColonyState GetState()
```

#### 4.2 PawnAnalyzer

Анализ отдельных поселенцев.

**Методы:**
```csharp
public PawnCapabilities GetCapabilities(Pawn pawn)
public float GetEffectiveness(Pawn pawn)
public List<WorkType> GetBestWorks(Pawn pawn)
```

#### 4.3 NeedsAnalyzer (v0.5+)

Анализ потребностей колонии.

**Методы:**
```csharp
public List<Need> GetCriticalNeeds()
public List<Need> GetUpcomingNeeds()
public Priority CalculatePriority(Need need)
```

### 5. Settings (Настройки)

**Расположение:** `Source/RimWatch/Settings/`

#### RimWatchSettings

**Хранимые данные:**
```csharp
public class RimWatchSettings
{
    // Режим работы
    public AutomationLevel Level { get; set; }
    
    // Категории автоматизации
    public bool AutoWork { get; set; }
    public bool AutoBuild { get; set; }
    public bool AutoFarm { get; set; }
    public bool AutoDefense { get; set; }
    public bool AutoTrade { get; set; }
    public bool AutoMedical { get; set; }
    
    // Настройки визуализации
    public bool ShowOverlay { get; set; }
    public bool ShowDecisions { get; set; }
    
    // Логирование
    public bool EnableLogging { get; set; }
    public LogLevel LogLevel { get; set; }
}
```

#### AutomationLevel (Enum)

```csharp
public enum AutomationLevel
{
    Assistant = 0,    // Минимальная помощь
    Manager = 1,      // Сбалансированное управление
    Observer = 2,     // Высокая автоматизация
    Autopilot = 3     // Полная автоматизация
}
```

### 6. Utils (Утилиты)

**Расположение:** `Source/RimWatch/Utils/`

#### 6.1 RimWatchLogger

Централизованная система логирования.

**Методы:**
```csharp
public static void Info(string message)
public static void Warning(string message)
public static void Error(string message, Exception? ex = null)
public static void Debug(string message)
```

#### 6.2 DebugOverlay (v0.5+)

Визуализация работы ИИ.

**Отображаемая информация:**
- Текущий режим
- Активные стратегии
- Планируемые действия
- Статистика эффективности

#### 6.3 PerformanceMonitor (v1.0+)

Отслеживание производительности.

**Метрики:**
- Время выполнения стратегий
- Использование памяти
- Влияние на FPS/TPS

### 7. Patches (Патчи Harmony)

**Расположение:** `Source/RimWatch/Patches/`

Используем Harmony для патчинга методов RimWorld.

#### 7.1 WorkPriority_Patch

**Цель:** `Verse.Pawn_WorkSettings.SetPriority()`

**Логика:**
```csharp
[HarmonyPatch(typeof(Pawn_WorkSettings), nameof(Pawn_WorkSettings.SetPriority))]
public class WorkPriority_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(Pawn_WorkSettings __instance, WorkTypeDef w, int priority)
    {
        if (RimWatchMod.IsAutomated)
        {
            // Наша логика назначения приоритетов
            return false; // Блокируем оригинальный метод
        }
        return true; // Пропускаем оригинальный метод
    }
}
```

#### 7.2 Building_Patch (v0.5+)

Патчинг строительства.

#### 7.3 Combat_Patch (v1.0+)

Патчинг боевых действий.

## 🔄 Цикл работы

### Общий цикл

```
Game Tick
    ↓
Should Update? (каждые N тиков)
    ↓
ColonyAnalyzer.GetState()
    ↓
DecisionEngine.Evaluate(state)
    ↓
Select Active Strategies
    ↓
For Each Strategy:
    ↓
    Strategy.CanExecute(state)?
        ↓
    Strategy.CreatePlan(state)
        ↓
    ActionPlanner.Optimize(plan)
        ↓
    Strategy.Execute(plan)
        ↓
    Log Results
```

### Частота обновлений

- **Критические:** Каждый тик (60 TPS = каждый тик)
- **Высокий приоритет:** Каждые 60 тиков (~1 секунда)
- **Средний приоритет:** Каждые 300 тиков (~5 секунд)
- **Низкий приоритет:** Каждые 600 тиков (~10 секунд)

## 💾 Хранение данных

### Сохранение состояния

RimWorld автоматически сохраняет `ModSettings`. Для дополнительных данных используем `GameComponent`:

```csharp
public class RimWatchGameComponent : GameComponent
{
    private DecisionHistory history;
    private PerformanceStats stats;
    
    public override void ExposeData()
    {
        Scribe_Deep.Look(ref history, "history");
        Scribe_Deep.Look(ref stats, "stats");
    }
}
```

## 🧪 Тестирование

### Структура тестов

```
Tests/
├── Unit/                    # Юнит-тесты
│   ├── AI/
│   │   ├── DecisionEngineTests.cs
│   │   ├── ColonyAnalyzerTests.cs
│   │   └── Strategies/
│   │       ├── WorkStrategyTests.cs
│   │       └── BuildStrategyTests.cs
│   └── Utils/
│       └── RimWatchLoggerTests.cs
├── Integration/             # Интеграционные тесты
│   ├── StrategyIntegrationTests.cs
│   └── PatchIntegrationTests.cs
└── Mocks/                   # Моки RimWorld API
    ├── MockMap.cs
    ├── MockPawn.cs
    └── MockColony.cs
```

## 🔐 Безопасность

### Thread Safety

RimWorld **не является thread-safe**. Все обращения к RimWorld API должны выполняться в главном потоке.

**Правило:** Если используем многопоточность (для расчетов), результаты применяем только в главном потоке.

### Обработка ошибок

```csharp
try
{
    strategy.Execute(plan);
}
catch (Exception ex)
{
    RimWatchLogger.Error($"Strategy {strategy.Name} failed", ex);
    // Откат к безопасному состоянию
    FallbackToSafeState();
}
```

## 📈 Производительность

### Оптимизации

1. **Кэширование**
   - Кэшируем результаты дорогих операций
   - Инвалидируем кэш при изменениях

2. **Ленивая загрузка**
   - Загружаем данные только когда нужно

3. **Object Pooling**
   - Переиспользуем объекты вместо создания новых

4. **Профилирование**
   - Используем PerformanceMonitor для отслеживания

### Целевые показатели

- **Overhead:** < 5% CPU в idle
- **Peak Usage:** < 10% CPU при активной работе
- **Memory:** < 50 MB дополнительной памяти
- **TPS Impact:** < 5% снижение

## 🔮 Будущие улучшения

### Версия 2.0

**Машинное обучение:**
```
PlayerStyleAnalyzer
    ↓
Collect User Actions
    ↓
Train Model (ML.NET)
    ↓
Adaptive DecisionEngine
    ↓
Execute with User Style
```

**Архитектура ML:**
- Сбор данных о действиях игрока
- Обучение модели на истории
- Адаптивное принятие решений
- Сохранение профилей стиля

### Версия 3.0

**Глубокое обучение:**
- Нейронные сети для сложных решений
- Обучение на данных множества игр
- Распознавание паттернов

## 📚 Ресурсы

### Документация
- [Harmony Wiki](https://harmony.pardeike.net/)
- [RimWorld Modding Wiki](https://rimworldwiki.com/wiki/Modding)

### Примеры кода
- См. `RimAsync` для примеров патчинга
- См. официальные моды Ludeon

---

**Последнее обновление:** 7 ноября 2025

