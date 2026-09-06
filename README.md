# RimWatch

**Автоматический игрок-наблюдатель для RimWorld с поддержкой ручного управления**

[![RimWorld Version](https://img.shields.io/badge/RimWorld-1.6-green.svg)](https://rimworldgame.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Development Status](https://img.shields.io/badge/Status-BETA-orange.svg)]()
[![Version](https://img.shields.io/badge/Version-1.4.0--dev-blue.svg)]()

## 🔔 v1.4.0-dev ACTION NOTIFICATIONS (2025-12-10)

**СЛЕДИТЕ ЗА ДЕЙСТВИЯМИ МОДА В РЕАЛЬНОМ ВРЕМЕНИ!**

### ✨ Что это значит

RimWatch теперь **показывает уведомления о ВСЕХ реальных действиях** прямо в игре! Каждое действие мода (постройка blueprint, изменение приоритета, спасение колониста) показывается через стандартные игровые Messages.

**Настраиваемые уровни детализации** - от критических событий до подробных отчетов с координатами!

### 🎯 Уровни детализации

- **Off** - нет уведомлений
- **Critical** - только жизненно важные (спасение, пожары, рейды)
- **Important** - + важные действия (blueprints, priorities, draft)
- **Moderate** - + обычные действия (hauling, planting, mining)
- **Verbose** - + детали (координаты, материалы, качество)
- **Debug** - всё включая execution time

### 📋 Примеры уведомлений

```
RimWatch 🏥 [Medical]: Medical emergency - DOWNED
RimWatch 🏗️ [Building]: Blueprint created - Bed at (120, 45)
RimWatch 👷 [Work]: Priority changed - Construction priority 1
RimWatch ⛏️ [Resources]: Mining designated - Steel ore (15 cells)
```

### ⚙️ Настройки

1. Откройте **Mod Settings → RimWatch → Notifications**
2. Включите **Enable Notification System**
3. Настройте уровень детализации для каждой из 9 категорий
4. Настройте формат (emoji, координаты, имена, материалы)

**Быстрый доступ:** Shift+R → Notifications ON/OFF

## 🎮 v1.3.0 PER-SAVE SETTINGS (2025-12-09)

**КАЖДЫЙ СЕЙВ ПОМНИТ СВОИ НАСТРОЙКИ!**

### ✨ Что это значит

RimWatch теперь **автоматически сохраняет настройки отдельно для каждого сейва**! Когда вы заходите в сейв, автоматически применяются те настройки, которые были активны когда вы последний раз играли этот сейв.

**По умолчанию ВКЛЮЧЕНО** - работает "из коробки" для всех новых и существующих сейвов!

### 🎯 Примеры использования

**Сценарий 1: Разные колонии - разные стратегии**
- Сейв "Desert Base" - включены farming + trade (выживание в пустыне)
- Сейв "Mountain Fortress" - включены building + defense (укрепленная база)
- Сейв "Tribal Start" - только work + social (примитивная колония)

**Сценарий 2: Тестирование**
- Сейв "Test Colony" - все автоматизации включены + debug mode
- Сейв "Normal Play" - только базовые автоматизации

**Переключение между сейвами автоматически меняет настройки!**

### ⚙️ Управление

1. Откройте **Mod Settings → RimWatch**
2. В самом верху (только в игре):
   - ✓ **Use per-save settings** (включено по умолчанию)
   - Кнопки для копирования настроек между глобальными и per-save
   - Индикатор текущего режима

3. Настройки сохраняются автоматически при каждом сохранении игры

### 🔄 Миграция старых сейвов

Старые сейвы (до v1.3.0) **автоматически мигрируют**:
- При первой загрузке используются текущие глобальные настройки
- При первом сохранении настройки записываются в save file
- **Никаких действий от вас не требуется!**

📖 **Технические детали:** см. `ROADMAP.md` (legacy release context) и `docs/archive/ARCHIVE_INDEX.md`.

## 🧪 v1.1.0 TESTING PHASE (2025-11-22)

**FIRST PLAYTEST SESSION: ✅ EXCELLENT RESULTS**

### 📊 Test Results Summary
- ✅ **0 errors, 0 crashes** - perfect stability
- ✅ **All 8 automation systems working** flawlessly
- ✅ **Performance: <2ms average** execution time per system
- ✅ **TPS Impact: <1%** - excellent optimization
- ✅ **Smart decision making** - correct priorities, emergency detection, resource management

**Detailed Analysis:** historical reference moved to `docs/archive/ARCHIVE_INDEX.md`.

### 🔍 What Was Tested
- ✅ WorkAutomation - Job priorities, emergency responses
- ✅ ResourceAutomation - Tree cutting, stone processing, emergency wood management
- ✅ ConstructionMonitor - 63 unfinished objects tracked correctly
- ✅ FurnitureRelocator - Bed relocation and installation
- ✅ ColonyDevelopment - Stage-based priorities (beds=100, food=95, storage=90)
- ✅ MedicalAutomation - Health monitoring, low medicine detection
- ✅ FarmingAutomation - 2888 plants harvested, smart taming (muffalo utility: 72.0)
- ✅ TradeAutomation - Forbid/allow management (996/1001 items processed)
- ✅ FloorBuilder - Room processing, ore detection working
- ✅ ResearchAutomation - Project tracking

### 🔜 Next Testing Steps
- [ ] **Long-term stability** - 5+ hour sessions
- [ ] **ML Systems verification** - Check startup initialization
- [ ] **Late game scenarios** - 10+ colonists, year 2+
- [ ] **Combat stress test** - Raid response, formations
- [ ] **Emergency scenarios** - Fire, medical crises, starvation

## 🎮 v1.3.0 PER-SAVE SETTINGS (2025-12-09)

**КАЖДЫЙ СЕЙВ ПОМНИТ СВОИ НАСТРОЙКИ!**

### ✨ Что это значит

RimWatch теперь **автоматически сохраняет настройки отдельно для каждого сейва**! Когда вы заходите в сейв, автоматически применяются те настройки, которые были активны когда вы последний раз играли этот сейв.

**По умолчанию ВКЛЮЧЕНО** - работает "из коробки" для всех новых и существующих сейвов!

### 🎯 Примеры использования

**Сценарий 1: Разные колонии - разные стратегии**
- Сейв "Desert Base" - включены farming + trade (выживание в пустыне)
- Сейв "Mountain Fortress" - включены building + defense (укрепленная база)
- Сейв "Tribal Start" - только work + social (примитивная колония)

**Сценарий 2: Тестирование**
- Сейв "Test Colony" - все автоматизации включены + debug mode
- Сейв "Normal Play" - только базовые автоматизации

**Переключение между сейвами автоматически меняет настройки!**

### ⚙️ Управление

1. Откройте **Mod Settings → RimWatch**
2. В самом верху (только в игре):
   - ✓ **Use per-save settings** (включено по умолчанию)
   - Кнопки для копирования настроек между глобальными и per-save
   - Индикатор текущего режима

3. Настройки сохраняются автоматически при каждом сохранении игры

### 🔄 Миграция старых сейвов

Старые сейвы (до v1.3.0) **автоматически мигрируют**:
- При первой загрузке используются текущие глобальные настройки
- При первом сохранении настройки записываются в save file
- **Никаких действий от вас не требуется!**

### 🔜 Next Testing Steps
- [ ] **Long-term stability** - 5+ hour sessions
- [ ] **ML Systems verification** - Check startup initialization
- [ ] **Late game scenarios** - 10+ colonists, year 2+
- [ ] **Combat stress test** - Raid response, formations
- [ ] **Emergency scenarios** - Fire, medical crises, starvation

## 🎉 v1.1.0 MACHINE LEARNING REVOLUTION (2025-11-22)

**ML SYSTEMS FULLY ACTIVATED - AI THAT LEARNS & PREDICTS:**

### 🧠 Machine Learning Features (NEW!)
- **Decision Analyzer**: AI learns from past decisions, improves strategies over time
- **Colony Predictor**: Forecasts food shortages, raids, resource depletion 1-3 days ahead
- **Player Style Learning**: Observes your manual overrides, adapts AI to match YOUR playstyle
- **Pattern Recognition**: Identifies successful strategies and avoids repeated failures

### 🏥 Medical Intelligence (ENHANCED!)
- **Auto-Surgery Scheduling**: Automatically schedules bionic upgrades, scar removal, organ replacements
- **Preventive Care**: Monitors colonist health, catches issues before they become critical
- **Bed Assignment**: Smart medical bed allocation for urgent cases
- **Pain Management**: Prioritizes treatment of most painful conditions first

### 🎛️ ML Configuration (NEW SETTINGS!)
- **Learning Rate**: Control how fast AI adapts (0.0-1.0, default 0.1)
- **Prediction Sensitivity**: Adjust warning thresholds (0.0-1.0, default 0.7)
- **Analysis Intervals**: Configure ML update frequency (default: 1 game day)
- **System Toggles**: Enable/disable individual ML systems

### 🚀 Advanced Systems Now Active
- **Tactical Combat**: Formations, positioning, retreat logic fully operational
- **Advanced Farming**: Crop rotation, soil fertility, breeding genetics implemented
- **Caravan Management**: Auto-formation, tracking, route optimization working
- **Base Layout Intelligence**: Multi-room planning with defensive considerations

## 🎉 v1.0.6 PRODUCTION RELEASE (2025-11-18)

**PRODUCTION-READY AI COLONY MANAGER:**

### 🎭 AI Storyteller System
- **5 Unique Personalities**: Cautious, Balanced, Aggressive, Chaotic, Custom
- **Storyteller UI**: Beautiful cards with preview & personality comparison
- **Profile Manager**: Save, load, and share storyteller configurations
- **Dynamic Switching**: Change storytellers mid-game seamlessly

### 🏗️ Advanced Building Intelligence  
- **Base Layout Planner**: Multi-room planning with optimal layouts
- **Material Intelligence**: Cost-benefit analysis, fire safety, beauty optimization
- **Building Upgrades**: Auto-upgrade when better tech researched
- **Furniture Decorator**: Smart placement of furniture, art, lighting

### 🤖 Machine Learning & Prediction
- **Decision Analyzer**: Learn from past AI decisions and patterns
- **Colony Predictor**: Forecast food shortages, raids, resource needs
- **Player Style Learning**: AI adapts to your manual overrides
- **Pattern Recognition**: Identifies successful strategies

### 🎨 Modern UI & Visualization
- **Dashboard**: Tabbed interface with Overview, Statistics, Settings, Alerts
- **Real-time Stats**: Colony status, automation systems, storyteller info
- **Debug Overlay**: Visualize AI plans (buildings, defense, pathfinding)
- **Decision History**: Complete log viewer with filters and search

### ⚡ Performance & Optimization
- **<5% TPS Impact**: Optimized for smooth gameplay
- **Smart Caching**: Cache expensive calculations
- **Dynamic Intervals**: Adjust update frequency based on load
- **Throttled Logging**: Reduce log spam

### 📊 Complete Automation (8 Systems)
- 🏗️ **Building**: Base layouts, room planning, furniture placement
- 👷 **Work**: Job priorities, skill optimization, emergency tasks
- 🌾 **Farming**: Crop rotation, animal breeding, seasonal planning
- 🛡️ **Defense**: Tactical positioning, combat roles, formations
- 💰 **Trade**: Auto-caravans, route optimization, resource management
- ⚕️ **Medical**: Operation scheduling, preventive care, health monitoring
- 👥 **Social**: Mood crisis detection, event planning, conflict resolution
- 🔬 **Research**: Priority-based research queue

📖 **[Read QUICK_START.md](QUICK_START.md)** | **[Storytellers Guide](STORYTELLERS_GUIDE.md)** | **[Full Changelog](CHANGELOG.md)**

## 🔙 ПРЕДЫДУЩЕЕ: v0.8.5 Beta (2025-11-17)

**CRITICAL BUG FIXES & PRODUCTION INTEGRATION:**
- 🐛 **ColonistCommandSystem** - ExecuteRescue полностью переписан (6,724→0 errors expected)
  - 8-шаговая валидация с try-catch на каждом этапе
  - Детальный trace: Task→Pawn→AlreadyRescued→FindRescuer→RescuerValidation→CreateJob→AssignJob→Success
  - Stack traces для диагностики, очистка failure count при успехе
- 🔇 **MedicalAutomation** - Global emergency throttling (тысячи→<100 warnings)
  - 60-секундный cooldown вместо 2 секунд
  - Убраны дублирующие Info логи
- 🎯 **DefenseAutomation** - State-based enemy logging (тысячи→state-based)
  - LogDecision с контекстом (enemyCount, previousCount, armedColonists)
  - LogStateChange для raid transitions (Peace→Raid, Combat→Peace)
- 📝 **DecisionLogger** - Улучшены комментарии для JSON formatting
- 🏠 **BuildingAutomation** - Улучшенный FindKitchenLocation с 3 fallback стратегиями
  - Strategy 1: Roofed near base
  - Strategy 2: Wider radius (30-50)
  - Strategy 3: Desperate 1x1 (радиус до 60)
- ⚙️ **ProductionAutomation** - Полная интеграция
  - Вызов в RimWatchMapComponent.Tick()
  - Настройка productionAutomationEnabled в Settings
  - LogDecision с контекстом (stage, colonists, tick)

📖 **[Полный Changelog в ROADMAP.md](ROADMAP.md#-версия-085---critical-bug-fixes--integration-phase)**

## 🏗️ НОВОЕ В v0.8.4+ (2025-11-16)

**MAJOR UI/UX IMPROVEMENTS + PRIORITY MEDICAL RESCUE:**
- 🎨 **Increased Scroll** - Высота настроек +600px (2400/3000px) - всё влезает комфортно!
- 🌐 **Global Logging Toggle** - Мастер-переключатель "Enable All Logging" для всех логов
- 📋 **Logging Settings Group** - Все логи в одной коллапсируемой секции, единый стиль
- ⚡ **Instant Apply** - Удалена кнопка "Применить", все настройки сохраняются мгновенно
- 💾 **Settings Persistence** - Все настройки теперь сохраняются корректно
- 🏥 **Priority Medical Rescue** - Доктора помогают сначала самому критичному пациенту (Downed+Bleeding > Downed > Heavy Bleeding)
- 📊 **Per-System Log Levels** - 9 систем с отдельными уровнями (Off/Minimal/Moderate/Verbose/Debug)

📖 **Полный Summary:** historical reference moved to `docs/archive/ARCHIVE_INDEX.md`.

## 🏗️ НОВОЕ В v0.8.4 (2025-11-16)

**FOUNDATION FOR INTELLIGENT BUILDING AUTOMATION:**
- 🏠 **RoomSizeCalculator** - Оптимальные размеры комнат по стадиям развития (12 типов)
- 📐 **BuildingSequencer** - Приоритеты строительства для Emergency/Early/Mid/Late/End
- ⚙️ **ProductionAutomation** - Автоматические bills для производства по стадиям
- 🐛 **Critical Bug Fixes** - ColonistCommandSystem Rescue (6,724→0 errors), MedicalAutomation spam (95% reduction)

📖 **Release Notes v0.8.4 / Implementation Summary:** historical references moved to `docs/archive/ARCHIVE_INDEX.md`.

## 🔍 ПРЕДЫДУЩЕЕ: v0.8.3 (2025-11-16)

**COMPREHENSIVE LOGGING & DEBUGGING INFRASTRUCTURE:**
- 📊 **Structured Logging Framework** - LogDecision, LogStateChange, LogExecutionStart/End, LogPerformance, LogFailure
- 🎯 **All Automation Systems Logged** - BuildingAutomation, WorkAutomation, FarmingAutomation, DefenseAutomation, MedicalAutomation, TradeAutomation, FloorBuilder
- ⚡ **Performance Monitoring** - Automatic detection операций >5ms с Stopwatch tracking
- 🐛 **Failure Pattern Detection** - Automatic recurring issue detection с warnings после 5+ failures
- 🛠️ **CRITICAL BUG FIX:** FloorBuilder теперь проверяет ore перед размещением пола! (из V079_ISSUES.md)
- 📈 **Full Decision Trail** - Видно каждое AI решение с контекстом для debugging

📖 **[Подробные Release Notes в ROADMAP.md](ROADMAP.md#-версия-083---comprehensive-logging--debugging-infrastructure)**

## 🔧 НОВОЕ В v0.8.2 (2025-11-12)

**КРИТИЧЕСКИЕ ИСПРАВЛЕНИЯ ПРОИЗВОДИТЕЛЬНОСТИ:**
- ⚡ **Rejected Location Cache** - предотвращение 423 повторных попыток размещения в одно место
- 🔇 **Warning Throttling** - сокращение warnings с 14,326 до <100 за 20 минут (99.3% reduction!)
- 📊 **Enhanced Diagnostics** - детальная информация о материалах для строительства комнат
- ⚡ **Adaptive Defense Interval** - 90% reduction проверок в мирное время

📖 **Release Notes v0.8.2:** historical reference moved to `docs/archive/ARCHIVE_INDEX.md`.

## 🐛 КРИТИЧЕСКОЕ ИСПРАВЛЕНИЕ v0.7.8 (2025-11-10)

**ИСПРАВЛЕН КРАХ ИГРЫ:**
- 💥 **GAME CRASH FIX:** Исправлена критическая ошибка, вызывавшая падение игры при клике на галочки в настройках
- 🛡️ **SAFE DIAGNOSTICS:** Добавлены безопасные проверки null/spawn в ConstructionMonitor
- ✅ **REACHABILITY CHECKS:** Защищено выполнение `CanReach()` от исключений с try-catch блоками
- 🔍 **DEFENSIVE CODING:** Проверки валидности pawn (Spawned, !Dead, !Downed, Map consistency)
- 📊 **BETTER LOGGING:** Использование Warning вместо Error для некритичных проблем

## ✨ ПРЕДЫДУЩЕЕ ОБНОВЛЕНИЕ (UI) v0.7.8 (2025-11-10)

**КРАСИВЫЙ И УМНЫЙ ИНТЕРФЕЙС:**
- 🎨 **STUNNING DESIGN:** Глубокие градиенты, акцентные рамки, идеальные цвета для Level 1/2/3
- 🖱️ **INTERACTIVE UI:** Hover-эффекты на каждом элементе, плавные переходы, визуальная обратная связь
- ✓ **STATUS ICONS:** Каждая настройка показывает ✓ (включено) или ○ (выключено) + цветовое кодирование:
  - Level 1: Зелёный (Success) → основные категории
  - Level 2: Синий (Accent) → подкатегории
  - Level 3: Белый (Info) → детальные опции
- 🎯 **SMART HIERARCHY:** Правильная логика parent-child:
  - Включение ребенка → автоматически включает родителя
  - Выключение родителя → автоматически выключает всех детей
  - Логичная иерархия, никакой путаницы!

## ⚠️ ПРЕДЫДУЩЕЕ ОБНОВЛЕНИЕ v0.7.7 (2025-11-10)

**УМНОЕ УПРАВЛЕНИЕ КРОВАТЯМИ:**
- 🛏️ **STORED BEDS INSTALLATION:** Автоматическая установка разобранных кроватей со склада!
- 🔍 **SMART BED DETECTION:** Система находит minified beds и устанавливает их в пустые спальни
- ✅ **PRIORITY TO EXISTING:** Приоритет существующим кроватям перед строительством новых

## ⚠️ КРИТИЧЕСКОЕ ОБНОВЛЕНИЕ v0.7.6 (2025-11-10)

**ЭКСТРЕННЫЕ ИСПРАВЛЕНИЯ (жизненно важны для выживания колонии!):**
- 🚨 **MEDICAL EMERGENCY SYSTEM:** Автоматическое спасение раненых/умирающих колонистов! Система проверяет downed/bleeding colonists каждые 2 секунды и автоматически назначает Doctor priority 1 всем способным колонистам
- 🏗️ **CONSTRUCTION IMPROVEMENTS:** Расширенный поиск локаций для строительства (радиус 3-60 вместо 5-40) + fallback на меньшие размеры комнат (5x6 вместо 6x8 для бараков)
- 🌙 **WORK SCHEDULE AUTOMATION:** Автоматическое управление расписанием с учетом Night Owl trait! Совы работают ночью, спят днем; обычные колонисты наоборот

## ⚠️ ВАЖНОЕ ОБНОВЛЕНИЕ v0.7.3 (2025-11-07)

**КРИТИЧЕСКИЕ ИСПРАВЛЕНИЯ (5 БАГОВ):**
- 🔴 **ОРУЖИЕ ДОСТУПНО ВО ВРЕМЯ БОЯ:** TradeAutomation больше не блокирует оружие/медикаменты при рейдах
- 🔴 **АВТОМАТИЧЕСКОЕ НАЗНАЧЕНИЕ ДОКТОРОВ:** WorkAutomation распознаёт кровотечения и назначает лечение
- 🔴 **РАЗМЕЩЕНИЕ КУХОНЬ/СКЛАДОВ:** BuildingAutomation теперь размещает даже на открытом воздухе
- 🔴 **GATHERING SPOTS:** BuildingAutomation создаёт места для отдыха (предотвращает mental breaks)
- ✅ **Скроллинг в настройках:** Теперь видны все секции настроек (добавлен scrollbar)

**НОВЫЕ ВОЗМОЖНОСТИ (v0.7.3):**
- ✅ **Debug Mode:** Детальные логи для диагностики проблем
- ✅ **File Logging:** Автоматическая запись всех логов в файл
- ✅ **Открытие папки логов:** Кнопка для быстрого доступа к файлам

**ПРЕДЫДУЩИЕ ИСПРАВЛЕНИЯ (v0.7.2):**
- ✅ **Вооружение колонистов:** Автопилот ищет оружие везде (земля, хранилище)
- ✅ **Размещение построек:** Смягчены требования для кухни/склада

**ПРЕДЫДУЩИЕ ИСПРАВЛЕНИЯ (v0.7.1):**
- ✅ **Проверка исследований:** Автопилот не размещает неизученные здания
- ✅ **Умный драфт:** Колонисты драфтятся только при реальной угрозе
- ✅ **Cooldown для животных:** Приручение/охота/забой с разумными интервалами

📖 **v0.7.x historical notes:** references moved to `docs/archive/ARCHIVE_INDEX.md`.

> 🎮 **Концепция:** Играй сколько хочешь, наблюдай сколько хочешь. RimWatch автоматически управляет колонией, пока ты отдыхаешь или решаешь стратегические задачи.

## Что такое RimWatch?

RimWatch - это мод, который превращает RimWorld в **полностью автоматический симулятор**: умный ИИ играет за тебя, управляя всеми аспектами колонии, пока ты наслаждаешься просмотром развития своей истории. Просто наблюдай за тем, как развивается твоя колония! 📺

**Ключевая особенность:** Разные **AI-рассказчики** (AI Storytellers) с уникальными личностями и стилями игры - от осторожного стратега до безумного экспериментатора!

> 🎯 **Поддерживается RimWorld 1.6**

## Ключевые Возможности

- **🤖 ПОЛНАЯ автоматизация** - ИИ управляет ВСЕМИ аспектами колонии
- **👁️ Режим кинотеатра** - Просто сиди и наблюдай за развитием истории
- **🎭 AI-Рассказчики** - Выбирай личность ИИ: Осторожный / Агрессивный / Безумный / Случайный
- **🎮 Удобный игровой интерфейс** - Быстрое меню (Shift+R) для переключения автоматизаций
- **🧠 Умное принятие решений** - ИИ управляет всем: строительством, работой, боем, торговлей
- **⚙️ Гибкие переключатели** - Включай/выключай любую автоматизацию одним кликом
- **📊 Визуализация решений** - Видишь что делает ИИ в реальном времени
- **💾 Профили рассказчиков** - Сохраняй и делись своими настройками ИИ

## Функциональность ИИ

### 🏗️ Строительство и планирование
- Автоматическое планирование баз с учетом обороноспособности
- Размещение хранилищ, зон отдыха, мастерских
- Расширение колонии при росте населения
- Адаптация к климату и местности

### 👷 Управление работой
- Автоматическое назначение приоритетов работ
- Оптимизация расписания поселенцев
- Балансировка нагрузки между колонистами
- Динамическое перераспределение задач

### 🌾 Сельское хозяйство
- Планирование и посадка полей
- Управление животноводством
- Охота и сбор ресурсов
- Заготовка пищи на зиму

### ⚔️ Оборона и военные действия
- Автоматическая расстановка в бою
- Управление турелями и укреплениями
- Тактическое отступление при необходимости
- Координация обороны базы

### 🛒 Торговля и экономика
- Умное управление запасами
- Автоматические караванные экспедиции
- Оптимизация торговых сделок
- Производство товаров на продажу

### 🏥 Медицина и благополучие
- Приоритизация лечения раненых
- Управление настроением колонистов
- Планирование отдыха и развлечений
- Предотвращение психических срывов

## 🎭 AI-Рассказчики (AI Storytellers)

Каждый рассказчик имеет уникальную личность и стиль игры:

### 🛡️ Осторожный Стратег (Cautious Strategist)
- **Стиль:** Минимум риска, максимум планирования
- **Фокус:** Выживание и стабильность
- **Строительство:** Медленное, но продуманное
- **Бой:** Оборонительная тактика, избегание конфликтов
- **Торговля:** Консервативная, накопление запасов
- **Подходит для:** Хардкорных сценариев, обучения

### ⚖️ Сбалансированный Менеджер (Balanced Manager)
- **Стиль:** Золотая середина между риском и стабильностью
- **Фокус:** Равномерное развитие всех областей
- **Строительство:** Среднее, функциональное
- **Бой:** Адаптивная тактика
- **Торговля:** Умеренная, по ситуации
- **Подходит для:** Стандартной игры, первого опыта

### ⚔️ Агрессивный Завоеватель (Aggressive Conqueror)
- **Стиль:** Быстрое развитие, высокий риск
- **Фокус:** Богатство и экспансия
- **Строительство:** Быстрое, практичное
- **Бой:** Активная агрессия, рейды на врагов
- **Торговля:** Агрессивная, максимум прибыли
- **Подходит для:** Опытных игроков, экшена

### 🎲 Хаотичный Экспериментатор (Chaotic Experimenter)
- **Стиль:** Непредсказуемость и эксперименты
- **Фокус:** "Что будет, если попробовать это?"
- **Строительство:** Странное, креативное
- **Бой:** Безумная тактика
- **Торговля:** Случайная, рискованная
- **Подходит для:** Веселья и хаоса

### 🔀 Случайный Рассказчик (Random Storyteller)
- **Стиль:** Меняется каждый игровой день/неделю
- **Фокус:** Непредсказуемость
- **Все аспекты:** Случайные решения
- **Подходит для:** Максимального разнообразия

### 🎨 Кастомный Рассказчик (Custom Storyteller)
- **Стиль:** Твои собственные настройки
- **Настраивай:** Каждый аспект поведения ИИ
- **Сохраняй:** Создавай свои уникальные личности
- **Делись:** Экспортируй профили для сообщества

## Установка

### Требования
- **RimWorld 1.6** (поддерживается версия 1.6)
- **[Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)** (обязательно)

### Шаги
1. Подпишись в Steam Workshop *(скоро)*
2. Включи мод в списке модов (загружай после Harmony)
3. Настрой уровень автономности в настройках мода
4. Наслаждайся автоматической игрой!

## 🎮 Управление и Настройки

### Горячая клавиша для быстрого доступа

**Shift+R** - Открывает быстрое меню RimWatch прямо в игре для переключения автоматизаций

```
┌─────────────────────────────────────────────────┐
│ 🎭 RimWatch Settings Panel                     │
├─────────────────────────────────────────────────┤
│ Текущий рассказчик: ⚖️ Сбалансированный        │
│ [Сменить рассказчика ▼]                         │
├─────────────────────────────────────────────────┤
│ Категории автоматизации:                        │
│ 🏗️ Строительство     [✓]                       │
│ 👷 Работа            [✓]                       │
│ 🌾 Сельское хоз-во   [✓]                       │
│ ⚔️ Оборона           [✓]                       │
│ 🛒 Торговля          [✓]                       │
│ 🏥 Медицина          [✓]                       │
│ 👥 Социальное        [✓]                       │
│ 🔬 Исследования      [✓]                       │
└─────────────────────────────────────────────────┘
```

### Настройки мода (полный доступ)

**Esc → Options → Mod Settings → RimWatch**

Здесь доступны:
- ✅ Включение/выключение всех 8 категорий автоматизации
- ✅ Выбор AI-рассказчика (стиля автопилота)
- ✅ Интервал принятия решений ИИ
- ✅ Debug логирование
- ✅ Применение настроек к автопилоту

```
┌─────────────────────────────────────────────────┐
│ RimWatch - AI Autopilot Settings                │
├─────────────────────────────────────────────────┤
│ ═══ Automation Categories ═══                   │
│ 🏗️ Building Automation       [✓]               │
│ 👷 Work Automation           [✓]               │
│ 🌾 Farming Automation        [✓]               │
│ ⚔️ Defense Automation        [ ]               │
│ 💰 Trade Automation          [ ]               │
│ ⚕️ Medical Automation        [✓]               │
│ 👥 Social Automation         [ ]               │
│ 🔬 Research Automation       [✓]               │
│                                                  │
│ ═══ AI Storyteller (Autopilot Style) ═══        │
│ Current Style: [⚖️ Balanced Manager ▼]         │
│                                                  │
│ ═══ Advanced Settings ═══                       │
│ Enable Debug Logging         [ ]                │
│ AI Decision Interval: [60] ticks (~1.0s)       │
│                                                  │
│ [Apply Settings to Autopilot]                   │
│ [Reset to Defaults]                             │
│                                                  │
│ 💡 Tip: Press Shift+R in game for quick access │
└─────────────────────────────────────────────────┘
```

## Настройки (для каждого рассказчика)

### Категории автоматизации (8 основных)
Каждая категория имеет **основной переключатель** + **детальные настройки**:

#### 🏗️ Строительство
- Автопланирование базы
- Выбор материалов
- Приоритеты построек
- Украшения и комфорт
- Оборонительные сооружения

#### 👷 Работа и расписание
- Назначение приоритетов
- Управление расписанием
- Балансировка нагрузки
- Учет навыков и здоровья

#### 🌾 Сельское хозяйство
- Планирование полей
- Выбор культур
- Животноводство
- Охота и сбор

#### ⚔️ Оборона и военное дело
- Расстановка в бою
- Управление турелями
- Тактика боя
- Рейды (атака/защита)

#### 🛒 Торговля и экономика
- Управление запасами
- Караваны
- Торговые сделки
- Производство товаров

#### 🏥 Медицина
- Приоритизация лечения
- Операции
- Лекарства и запасы

#### 👥 Социальное и настроение
- Управление настроением
- Отдых и развлечения
- Разрешение конфликтов
- Тюрьма и рекрутинг

#### 🔬 Исследования
- Выбор технологий
- Приоритеты исследований
- Адаптация под ситуацию

## Система обучения ИИ

RimWatch использует **адаптивную систему принятия решений**:

1. **Анализ ситуации** - ИИ оценивает состояние колонии
2. **Определение приоритетов** - Выбор наиболее важных задач
3. **Планирование действий** - Построение оптимального плана
4. **Выполнение** - Реализация решений
5. **Обучение** - ИИ учится на результатах

> 🧠 **Будущая функция:** Машинное обучение на основе твоего стиля игры - ИИ будет копировать твои решения!

## 📊 Визуализация и отладка

### Кнопка Визуализации (F10)

Отдельная кнопка рядом с главной кнопкой RimWatch для отображения активности ИИ:

### Debug Overlay (F10)

Нажми **кнопку визуализации** или **F10** для отображения оверлея с информацией:

- **Текущий режим** - Активный режим автоматизации
- **Активные задачи** - Что сейчас делает ИИ
- **Следующие планы** - Будущие действия
- **Статистика решений** - Успешность действий ИИ
- **Производительность** - Влияние на FPS/TPS

## Совместимость

### ✅ Совместимо
- **RimAsync** - Рекомендуется для лучшей производительности
- Большинство модов на контент (оружие, животные, события)
- UI моды
- Quality of Life моды

### ⚠️ Возможны конфликты
- Моды, изменяющие ИИ поселенцев
- Моды с автоматизацией (может дублировать функционал)
- Моды, сильно модифицирующие игровой цикл

### ❌ Несовместимо
- Моды, полностью переписывающие систему приоритетов работ

## Дорожная карта

Авторитетное планирование и задачи ведутся в OpenSpec.

- **Primary planning source:** `openspec/planning/PLANNING_REGISTRY.md`
- **Implementation tasks source:** `openspec/changes/<change>/tasks.md`
- **Rule:** если задача не представлена в OpenSpec planning/change artifacts, она не считается запланированной
- **ROADMAP role:** стратегический индекс, приоритеты и исторический контекст (без authoritative task ownership)
- **Workflow:** `docs/OPENSPEC_WORKFLOW.md`

Исторический контекст по версиям и выполненным этапам по-прежнему доступен в `ROADMAP.md` (раздел `Legacy Historical Roadmap`).

## Разработка

RimWatch использует **Docker** для всех операций компиляции и тестирования.

### 📋 Правила разработки

**⚠️ ВАЖНО: Все логи должны быть на английском!**

```csharp
// ✅ ПРАВИЛЬНО
RimWatchLogger.Info("Autopilot enabled");
RimWatchLogger.Debug($"Processing {count} colonists");

// ❌ НЕПРАВИЛЬНО
RimWatchLogger.Info("Автопилот включен");
```

**Почему английский?**
- 🌍 Международная поддержка и отладка
- 🐛 Понятные отчеты об ошибках для всех
- 👥 Открытость для контрибьюторов со всего мира

**Что локализуется:**
- ✅ UI текст (кнопки, меню, тултипы) - будет в v1.5
- ✅ Описания и документация
- ❌ Логи и ошибки - **только английский!**

Подробнее: [DEVELOPMENT_GUIDELINES.md](DEVELOPMENT_GUIDELINES.md)

### Быстрые команды
```bash
# Полный цикл: сборка + установка
make deploy

# Только сборка
make build

# Только установка (требует предварительной сборки)
make install

# Запуск тестов
make test
```

**Важно для macOS (Steam)**: Мод автоматически устанавливается в правильную папку:
```
~/Library/Application Support/Steam/steamapps/common/RimWorld/RimWorldMac.app/Mods/
```

Для кастомного пути создай `.env` файл (см. `.env.example`).

## Поддержка

**Нашел проблему?** Сообщи с указанием:
- Версия RimWorld
- Список модов
- Логи ошибок (см. раздел "Где найти логи" ниже)
- Режим автоматизации

### 📋 Где найти логи

#### Основные логи RimWorld (включают RimWatch):
**macOS:**
```
~/Library/Logs/Ludeon Studios/RimWorld by Ludeon Studios/Player.log
```

**Windows:**
```
C:\Users\[ИМЯ_ПОЛЬЗОВАТЕЛЯ]\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log
```

**Linux:**
```
~/.config/unity3d/Ludeon Studios/RimWorld by Ludeon Studios/Player.log
```

#### Детальные логи RimWatch (если включено в настройках):
**macOS:**
```
~/Library/Application Support/RimWorld/by_Ludeon_Studios/RimWorld/RimWatch_Logs/
```

**Windows:**
```
C:\Users\[ИМЯ_ПОЛЬЗОВАТЕЛЯ]\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\RimWatch_Logs\
```

**Linux:**
```
~/.config/unity3d/Ludeon Studios/RimWorld by Ludeon Studios/RimWatch_Logs/
```

> 💡 **Совет:** В настройках RimWatch можно включить "File Logging" для создания отдельных детальных логов с timestamp

#### Как быстро открыть папку с логами:
- **macOS:** В Finder нажми `Cmd+Shift+G` и вставь путь: `~/Library/Logs/Ludeon Studios/RimWorld by Ludeon Studios/`
- **Windows:** Нажми `Win+R`, вставь `%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\` и нажми Enter
- **Linux:** Открой файловый менеджер и перейди в `~/.config/unity3d/Ludeon Studios/RimWorld by Ludeon Studios/`

## Философия мода

RimWatch создан для тех, кто:
- ❤️ Любит **наблюдать** за развитием колоний
- 🎮 Хочет **расслабиться**, не теряя интереса к игре
- 🧪 Любит **экспериментировать** с разными стратегиями
- ⏱️ Ценит **свое время**, но не хочет пропускать контент
- 🤖 Интересуется **ИИ** и автоматизацией

> "Не всегда нужно играть - иногда можно просто смотреть, как растет твоя история"

## Лицензия

Проект распространяется под лицензией MIT - см. [LICENSE](LICENSE).

## Благодарности

- **Ludeon Studios** - За RimWorld
- **Автор RimAsync (Ilya Volkov)** - За вдохновение и структуру проекта
- **Сообщество RimWorld** - За идеи и обратную связь

---

**Сделано для сообщества RimWorld с любовью к автоматизации** 🤖❤️

---

## FAQ

### Q: Это читерство?
**A:** Нет! ИИ использует только доступную игроку информацию и не дает преимуществ. Это скорее "ассистент", который играет вместо тебя.

### Q: Можно ли отключить мод во время игры?
**A:** Да! Можно переключаться между режимами в любой момент или полностью отключить автоматизацию.

### Q: ИИ будет делать глупости?
**A:** Возможно на ранних версиях 😅 Но система постоянно улучшается и учится. Сообщай о проблемах!

### Q: Совместимо с сохранениями?
**A:** Да! Мод можно добавлять и удалять из существующих игр без проблем.

### Q: Будет ли multiplayer поддержка?
**A:** Планируется в версии 2.0! Представь: несколько ИИ-колоний играют друг против друга.

### Q: Мод работает с [моё любимое DLC]?
**A:** RimWatch спроектирован для работы со всеми официальными DLC и большинством модов на контент.

