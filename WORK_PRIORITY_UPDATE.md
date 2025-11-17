# 🎯 Work Priority System Update

## ✅ Реализованные фичи

### 1. **Переключение режима приоритетов**

Добавлена настройка **"Use Manual Priorities (Точная настройка)"** в настройках мода.

**Как работает:**
- ✅ **Включено (по умолчанию)**: **Manual Priorities** - точная настройка работы (цифры 1-4)
  - **1** = Высший приоритет
  - **2** = Высокий приоритет
  - **3** = Средний приоритет
  - **4** = Низкий приоритет
  - **Пустое поле** = Работа выключена
  
- ✅ **Выключено**: **Simple Checkboxes** - простые галочки (✓ = включено, пустое = выключено)

**Автоматическое переключение:**
```csharp
// WorkAutomation автоматически переключает режим при каждом обновлении
bool useManualPriorities = RimWatchMod.Settings?.useManualPriorities ?? true;
Current.Game.playSettings.useWorkPriorities = useManualPriorities;
```

**Лог при переключении:**
```
🔄 WorkAutomation: Switched to Manual Priorities (1-4)
🔄 WorkAutomation: Switched to Simple Checkboxes
```

---

### 2. **Детальное логирование изменений**

Автопилот теперь логирует **каждое изменение** приоритетов работ для каждого колониста.

**Пример логов:**

```
👷 WorkAutomation: Cait - Changed 3 priorities:
   • Cooking: 3 → 1
   • Construction: 2 → 3
   • Growing: 4 → 2

👷 WorkAutomation: Seven - Changed 5 priorities:
   • Patient: DISABLED → 1
   • Doctor: 3 → 1
   • Firefight: 2 → 1
   • Hunting: 3 → 2
   • Cooking: 1 → DISABLED

🏹 FarmingAutomation: Designated Muffalo for hunting
🛒 TradeAutomation: 🚫 Forbade 12 items (combat in progress)
🛒 TradeAutomation: ✅ Allowed 8 valuable items, ❌ Forbade 3 junk items
⚔️ DefenseAutomation: 🪖 Drafted 2 colonists for combat
⚔️ DefenseAutomation: ✅ Undrafted 2 colonists (no threats)
```

**Детали реализации:**
- Логируется **каждое изменение** приоритета: `WorkType: OldValue → NewValue`
- Логируются действия всех automation модулей:
  - ✅ WorkAutomation - изменения приоритетов
  - ✅ FarmingAutomation - охота, забой, приручение
  - ✅ TradeAutomation - forbid/allow предметов
  - ✅ DefenseAutomation - драфт, экипировка
  - ✅ SocialAutomation - рекомендации по заключенным

---

## 📊 Технические детали

### Измененные файлы:

1. **`RimWatchSettings.cs`**
   - Добавлено поле: `public bool useManualPriorities = true;`
   - Добавлено сохранение/загрузка: `Scribe_Values.Look(ref useManualPriorities, "useManualPriorities", true);`

2. **`RimWatchMod.cs`** (UI)
   - Добавлена секция "Work Priority Settings" с чекбоксом
   - Описание: "Enable to use 1-4 priority numbers. Disable for simple checkboxes."

3. **`WorkAutomation.cs`**
   - Автоматическое переключение режима в `UpdateWorkPriorities()`
   - Детальное логирование в `AssignWorkPriorities()`
   - Собирается список изменений `List<string> changes`
   - Выводится детальный лог для каждого колониста

### Алгоритм работы:

```
1. Каждые ~4 секунды (250 тиков):
   ↓
2. Проверить настройку useManualPriorities
   ↓
3. Переключить Current.Game.playSettings.useWorkPriorities если нужно
   ↓
4. Для каждого колониста:
   ↓
5. Для каждого типа работы:
   - Получить старый приоритет
   - Рассчитать новый приоритет (AI + потребности)
   - Если изменился → применить + записать в лог
   ↓
6. Вывести детальный лог всех изменений
```

---

## 🎮 Как использовать:

### Способ 1: Настройки мода
1. В игре: **Esc → Options → Mod Settings → RimWatch**
2. Найти секцию **"Work Priority Settings"**
3. Включить/выключить **"Use Manual Priorities"**
4. Нажать **"Apply Settings to Autopilot"**

### Способ 2: Quick Menu (Shift+R)
1. Нажать **Shift+R** в игре
2. Изменить настройку в быстром меню (если добавлена)

---

## 📝 Примеры логов в игре

### При включенном автопилоте:
```
[WorkAutomation] Tick! Interval reached, running work priority update...
🔄 WorkAutomation: Switched to Manual Priorities (1-4)
ColonyNeeds: Food=3, Construction=2, Research=2, Plants=1, Medical=1, Defense=1

👷 WorkAutomation: Cait - Changed 3 priorities:
   • Cooking: 3 → 1
   • Construction: 2 → 3
   • Growing: 4 → 2

👷 WorkAutomation: Seven - Changed 2 priorities:
   • Doctor: 3 → 1
   • Firefight: 2 → 1

FarmingAutomation: 🏹 Designated Muffalo for hunting
TradeAutomation: ✅ Allowed 5 valuable items
```

---

## ✅ Статус: **ЗАВЕРШЕНО**

- ✅ Настройка `useManualPriorities` добавлена
- ✅ UI галочка добавлена в настройки
- ✅ Автоматическое переключение режима реализовано
- ✅ Детальное логирование всех изменений реализовано
- ✅ Код скомпилирован без ошибок
- ✅ Мод задеплоен в RimWorld

**Готово к тестированию в игре!** 🎉

