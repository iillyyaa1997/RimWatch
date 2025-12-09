# 🧠 RimWatch Machine Learning System - Объяснение

## 📚 Содержание
1. [Что такое ML система](#что-такое-ml-система)
2. [Компоненты системы](#компоненты-системы)
3. [Как это работает](#как-это-работает)
4. [Примеры использования](#примеры-использования)
5. [Как увидеть ML в действии](#как-увидеть-ml-в-действии)

---

## 🤖 Что такое ML система?

**ML (Machine Learning)** в RimWatch - это **система обучения AI**, которая:
- 📊 **Анализирует решения** AI за всю игру
- 🎯 **Предсказывает будущие нужды** колонии
- 🎓 **Учится у игрока** из ручных изменений
- 🔄 **Адаптирует стратегию** на основе опыта

**Важно:** Это НЕ нейронные сети! Это статистический анализ и адаптация на основе правил.

---

## 🏗️ Компоненты системы

### 1. **DecisionAnalyzer** 📊
**Что делает:** Анализирует все решения AI

**Местоположение:** `Source/RimWatch/ML/DecisionAnalyzer.cs`

**Функции:**
```csharp
// Записывает каждое решение AI
public static void RecordDecision(string system, string decision, Dictionary<string, object> context)

// Анализирует паттерны за последний игровой день
public static DecisionPattern AnalyzePatterns(string system)

// Находит наиболее часто принимаемые решения
public static List<FrequentDecision> GetFrequentDecisions(string system, int minCount = 10)
```

**Пример данных:**
```json
{
  "system": "BuildingAutomation",
  "decision": "NeedsBeds",
  "context": {
    "colonists": 5,
    "existingBeds": 3,
    "needCount": 2
  },
  "timestamp": "2025-11-18 15:30:45",
  "frequency": 47
}
```

**Что анализирует:**
- ✅ Частота решений за день (например, "BuildBeds" - 47 раз)
- ✅ Изменение частоты со временем (тренды)
- ✅ Корреляции между решениями (например, BuildKitchen → BuildStorage)

---

### 2. **ColonyPredictor** 🔮
**Что делает:** Предсказывает будущие нужды колонии

**Местоположение:** `Source/RimWatch/ML/ColonyPredictor.cs`

**Функции:**
```csharp
// Предсказывает когда закончится ресурс
public static PredictionResult PredictResourceDepletion(Map map, string resourceDefName)

// Предсказывает рост населения
public static int PredictColonistCount(Map map, int daysAhead)

// Предсказывает нужды в постройках
public static List<BuildingNeed> PredictBuildingNeeds(Map map, int daysAhead)
```

**Как работает:**
1. **Анализ трендов** - смотрит на изменение ресурсов за последние 5 дней
2. **Линейная экстраполяция** - продолжает тренд в будущее
3. **Проверка стабильности** - игнорирует случайные всплески

**Пример предсказания:**
```
📉 Wood: 150 units → Depleting at -30/day → Will run out in 5 days
📈 Colonists: 5 → Growing at +0.2/day → Will be 6 in 5 days
🏗️ Buildings needed: +2 beds, +1 kitchen (based on predicted growth)
```

**Что предсказывает:**
- ✅ Истощение дерева, металла, еды
- ✅ Рост населения (прибытие новых колонистов)
- ✅ Потребности в новых постройках
- ✅ Пиковые нагрузки на кухню/склады

---

### 3. **PlayerStyleAnalyzer** 👤
**Что делает:** Учится из ручных действий игрока

**Местоположение:** `Source/RimWatch/ML/PlayerStyleAnalyzer.cs`

**Функции:**
```csharp
// Записывает когда игрок вручную изменил что-то
public static void RecordOverride(string category, string aiDecision, string playerDecision)

// Анализирует предпочтения игрока
public static PlayerPreferences AnalyzePlayerPreferences()

// Получает предпочитаемый материал игрока
public static ThingDef GetPreferredBuildingStuff(BuildableDef buildingDef)
```

**Что запоминает:**
1. **Материалы построек** - если игрок вручную меняет камень → дерево
2. **Типы зданий** - если игрок предпочитает двуспальные кровати вместо одиночных
3. **Размещение** - если игрок перемещает постройки AI
4. **Приоритеты** - если игрок отменяет задачи AI

**Пример обучения:**
```
AI: "Build beds from stone"
Player: [Manually changes to wood] ✋
PlayerStyleAnalyzer: "Player prefers WOOD for beds!" 
Next time: AI will use wood automatically
```

**История override:**
```json
{
  "category": "BuildingMaterial",
  "aiDecision": "BlocksGranite",
  "playerDecision": "WoodLog",
  "timestamp": "2025-11-18 15:45:00",
  "frequency": 8
}
```

**Адаптация:**
- ✅ После 3+ manual override → AI меняет стратегию
- ✅ После 10+ manual override → AI считает это правилом
- ✅ Учитывает время суток, стадию развития, наличие ресурсов

---

## ⚙️ Как это работает?

### Цикл обучения ML:

```
┌──────────────┐
│ AI принимает │
│   решение    │
└──────┬───────┘
       │
       ▼
┌──────────────┐
│ DecisionLogger│ ──→ decisions_YYYY-MM-DD.json (7,400+ решений/день)
│  записывает  │
└──────┬───────┘
       │
       ▼
┌──────────────┐
│DecisionAnalyzer│ ──→ Анализ: частота, тренды, паттерны
│  анализирует │
└──────┬───────┘
       │
       ▼
┌──────────────┐
│ColonyPredictor│ ──→ Предсказание: дерево кончится через 5 дней!
│ предсказывает│
└──────┬───────┘
       │
       ▼
┌──────────────┐
│      AI      │ ──→ "Нужно больше деревьев! Увеличиваю радиус поиска!"
│ адаптируется │
└──────────────┘
       │
       │  [Player override]
       ├────────────────────→ PlayerStyleAnalyzer
       │                           │
       │                           ▼
       │                     "Игрок предпочитает X!"
       └───────────────────────────┘
                 │
                 ▼
          AI учится и адаптирует
          будущие решения!
```

### Фазы обучения:

#### **Фаза 1: Сбор данных (первые 1-2 часа игры)**
- DecisionLogger записывает ВСЕ решения AI
- Файлы `decisions_*.json` растут
- Пока нет достаточно данных → ML неактивен

#### **Фаза 2: Анализ паттернов (после 3+ часов игры)**
- DecisionAnalyzer находит частые решения
- Например: "BuildBeds вызывается каждые 10 минут"
- Определяет критические моменты (кризисы ресурсов)

#### **Фаза 3: Предсказание (после 5+ часов игры)**
- ColonyPredictor начинает видеть тренды
- Предсказывает истощение ресурсов
- AI начинает действовать **заранее**

#### **Фаза 4: Адаптация под игрока (постоянно)**
- PlayerStyleAnalyzer учится с первой минуты
- Каждый manual override → запись в историю
- После 3+ override → изменение стратегии

---

## 🎮 Примеры использования

### Пример 1: Предсказание нехватки дерева

**Ситуация:**
- Day 1: Wood = 500
- Day 2: Wood = 400
- Day 3: Wood = 300
- Day 4: Wood = 200

**Анализ ColonyPredictor:**
```
Trend: -100 wood/day
Prediction: Wood will run out in 2 days!
```

**Действие AI:**
```
BuildingAutomation: Increasing tree cutting from 10 → 20 trees
ResourceAutomation: Extending search radius from 40 → 60
```

**Результат:**
✅ AI заранее подготовился → нехватка предотвращена!

---

### Пример 2: Обучение на материалах

**AI решение (Day 1):**
```
BuildingAutomation: Building bed from stone (BlocksGranite)
```

**Player action:**
```
[Player cancels stone bed]
[Player builds wood bed manually]
```

**PlayerStyleAnalyzer записал:**
```json
{
  "override": {
    "category": "BedMaterial",
    "ai": "BlocksGranite",
    "player": "WoodLog",
    "count": 1
  }
}
```

**AI решение (Day 2):**
```
AI: "Try stone again..."
[Player cancels again] ✋
PlayerStyleAnalyzer: count = 2
```

**AI решение (Day 3):**
```
AI: "Player clearly prefers wood!"
BuildingAutomation: Building bed from WOOD (WoodLog)
[Player does not cancel] ✅
PlayerStyleAnalyzer: "Confirmed! Wood is the rule!"
```

**Результат:**
✅ AI научился → больше не тратит время на отмену

---

### Пример 3: Предсказание роста населения

**ColonyPredictor анализ:**
```
Day 1: 3 colonists
Day 5: 4 colonists (refugee joined)
Day 10: 5 colonists (quest reward)
Day 15: 6 colonists (wanderer joined)

Average growth: +0.2 colonists/day
Prediction (Day 20): 7 colonists expected
```

**AI реакция:**
```
BuildingAutomation: "Need 7 beds by Day 20"
BuildingAutomation: Planning +2 beds in advance
BuildingAutomation: Expanding kitchen (current stove capacity: 5)
```

**Результат:**
✅ Кровати готовы ДО прибытия новых колонистов!

---

## 🔍 Как увидеть ML в действии?

### 1. **Decision Logs** (файлы решений)

**Где:** `~/Library/Application Support/RimWorld/RimWatch_Logs/decisions_*.json`

**Что искать:**
```bash
# Сколько решений за день?
wc -l decisions_2025-11-18.json
# Результат: 7,400+ строк!

# Какие решения самые частые?
grep "BuildingAutomation" decisions_2025-11-18.json | wc -l
# Результат: ~200 решений

# Найти предсказания
grep "ColonyPredictor" decisions_2025-11-18.json
```

### 2. **Логи игры** (RimWorld Player.log)

**Где:** `~/Library/Logs/Ludeon Studios/RimWorld by Ludeon Studios/Player.log`

**Что искать:**
```bash
# ML анализ паттернов
grep "DecisionAnalyzer" Player.log

# Предсказания ресурсов
grep "ColonyPredictor" Player.log

# Обучение на игроке
grep "PlayerStyleAnalyzer" Player.log
```

### 3. **UI Dashboard** (Shift+R)

**Что смотреть:**
- **Tab "Recent Decisions"** - последние 10 решений AI
- **Tab "Statistics"** - частота решений по категориям
- **Tab "Alerts"** - предупреждения о нехватке ресурсов

### 4. **Признаки активности ML:**

#### DecisionAnalyzer:
```
[DEBUG] DecisionAnalyzer: Analyzed 1,234 decisions from last day
[INFO] DecisionAnalyzer: Frequent pattern detected - BuildBeds every 600 ticks
```

#### ColonyPredictor:
```
[WARN] ColonyPredictor: Wood depletion predicted in 3 days!
[INFO] ColonyPredictor: Colony growth trend: +0.15 colonists/day
```

#### PlayerStyleAnalyzer:
```
[INFO] PlayerStyleAnalyzer: Override recorded - Player prefers WoodLog for Bed
[INFO] PlayerStyleAnalyzer: After 5 overrides, adapting AI strategy...
```

---

## ⏱️ Почему ML не виден сразу?

### Причина 1: **Недостаточно данных**
ML системе нужно **минимум 3-5 игровых часов** для накопления паттернов.

**Решение:** Играй дольше! ML становится активнее со временем.

---

### Причина 2: **DecisionAnalyzer работает "в фоне"**
Система анализирует решения **без явных логов**, чтобы не спамить консоль.

**Решение:** Включи Debug Mode в настройках:
```
RimWatch Settings → Debug & Logging → Debug Mode: ON
```

---

### Причина 3: **ColonyPredictor требует трендов**
Для предсказания нужны **изменения** за 5+ дней, а не статичная ситуация.

**Что вызывает активность:**
- ✅ Растущее население (+1-2 колониста за 5 дней)
- ✅ Истощение ресурсов (дерево уменьшается)
- ✅ Изменение стиля игры (от defensive → aggressive)

**Решение:** Играй более динамично! Принимай новых колонистов, стройся, развивайся.

---

### Причина 4: **PlayerStyleAnalyzer ждет ручных действий**
AI не может учиться, если игрок **не делает manual override**.

**Что считается override:**
- ✅ Отмена постройки AI и замена её на другую
- ✅ Изменение материала постройки вручную
- ✅ Перемещение мебели, которую поставил AI
- ✅ Отмена задач, которые назначил AI

**Решение:** Если не нравится решение AI → измени его вручную! AI запомнит.

---

## 🎯 Резюме

| Компонент | Что делает | Когда активен | Где видно |
|-----------|-----------|---------------|-----------|
| **DecisionLogger** | Записывает все решения | С первой секунды | `decisions_*.json` (7,400+ строк) |
| **DecisionAnalyzer** | Анализирует паттерны | После 1-3 часов игры | Debug logs (если включен) |
| **ColonyPredictor** | Предсказывает нужды | После 3-5 часов игры | Logs + Alerts tab |
| **PlayerStyleAnalyzer** | Учится у игрока | Мгновенно (с первого override) | Logs (после 3+ override) |

### Как ускорить обучение ML:

1. ✅ **Играй дольше** - минимум 3-5 игровых часов
2. ✅ **Делай override** - меняй решения AI вручную
3. ✅ **Включи Debug Mode** - чтобы видеть ML логи
4. ✅ **Динамичная игра** - растущее население, изменение ресурсов
5. ✅ **Читай decision logs** - там ВСЯ история AI

---

**ML система RimWatch - это долгосрочная инвестиция!**  
Чем дольше играешь, тем умнее становится AI! 🧠✨

