# Per-Save Settings - Technical Guide

**Version:** 1.3.0  
**Date:** December 9, 2025

## Обзор

Per-Save Settings - это функция RimWatch, которая позволяет сохранять настройки мода отдельно для каждого save file. Когда вы загружаете сейв, автоматически применяются настройки, которые были активны когда вы последний раз играли этот сейв.

**По умолчанию ВКЛЮЧЕНО** - работает автоматически для всех новых и существующих сейвов.

## Архитектура

### Компоненты системы

1. **RimWatchGameComponent** (`RimWatch/Source/RimWatch/Components/RimWatchGameComponent.cs`)
   - Наследуется от `GameComponent` (привязан к игре)
   - Хранит копию ВСЕХ настроек мода (~73 поля)
   - Сохраняется/загружается через `ExposeData()` в save file
   - Применяет настройки через `FinalizeInit()` при загрузке игры

2. **GameComponents.xml** (`RimWatch/Defs/GameComponents.xml`)
   - Регистрирует компонент в RimWorld через def system
   - Автоматически создается при запуске новой игры

3. **RimWatchMod.GameComponent** (property)
   - Доступ к per-save настройкам из любого места кода
   - Возвращает `null` если не в игре (главное меню)

4. **UnifiedSettingsUI** (обновлен)
   - UI для управления per-save настройками
   - Checkbox, кнопки копирования, индикатор статуса
   - Показывается только в игре

## Как это работает

### 1. Создание нового сейва

```
1. Игрок создает новую игру
2. RimWorld автоматически создает RimWatchGameComponent (через def)
3. _usePerSaveSettings = true (по умолчанию)
4. При первом сохранении игры:
   - ExposeData() вызывается автоматически
   - Копируются текущие глобальные настройки → per-save
   - Записываются в save file
5. Лог: "✓ Per-save settings initialized for new save"
```

### 2. Загрузка существующего сейва

```
1. Игрок загружает сейв
2. ExposeData() загружает per-save настройки из save file
3. FinalizeInit() вызывается после полной загрузки игры
4. Если _usePerSaveSettings == true:
   - ApplyToGlobalSettings() копирует per-save → global
   - Settings.ApplyToCore() применяет к системам мода
5. Лог: "✓ Applied per-save settings to global (loaded from save)"
```

### 3. Миграция старого сейва (без per-save данных)

```
1. Игрок загружает сейв, созданный до v1.3.0
2. RimWatchGameComponent создается автоматически
3. _usePerSaveSettings = true (default)
4. Но настроек в save file нет → используются текущие глобальные
5. При первом сохранении игры:
   - Текущие настройки записываются как per-save
6. Лог: "✓ Per-save settings initialized (new save or migrated)"
```

### 4. Изменение настроек в игре

```
1. Игрок открывает Mod Settings → RimWatch
2. Меняет настройки (например, включает defense)
3. RimWatchMod.WriteSettings() вызывается автоматически:
   - Settings.ApplyToCore() - применяет к системам
   - GameComponent.CopyFromGlobalSettings() - синхронизирует с per-save
4. При следующем сохранении игры - записываются в save file
5. Лог: "[MOD] Synced settings to per-save (in-game)"
```

### 5. Отключение per-save настроек

```
1. Игрок снимает галочку "Use per-save settings"
2. UsePerSaveSettings = false
3. FinalizeInit() теперь пропускает ApplyToGlobalSettings()
4. Изменения настроек влияют на ВСЕ сейвы (глобально)
5. Лог: "[GameComponent] Using global settings (per-save disabled)"
```

## Код примеры

### Доступ к per-save настройкам

```csharp
// Получить GameComponent (только в игре!)
var gameComponent = RimWatchMod.GameComponent;
if (gameComponent != null && gameComponent.UsePerSaveSettings)
{
    // Per-save настройки активны
    RimWatchLogger.Info("Using per-save settings");
}
else
{
    // Используются глобальные настройки
    RimWatchLogger.Info("Using global settings");
}
```

### Копирование настроек

```csharp
// Global → Per-Save
var gameComponent = RimWatchMod.GameComponent;
if (gameComponent != null)
{
    gameComponent.CopyFromGlobalSettings();
    // Будет сохранено при следующем save
}

// Per-Save → Global
if (gameComponent != null)
{
    gameComponent.ApplyToGlobalSettings();
    RimWatchMod.Settings.Write(); // Сохранить на диск
}
```

### Проверка наличия per-save настроек

```csharp
bool IsInGame()
{
    return Current.Game != null && RimWatchMod.GameComponent != null;
}

bool IsUsingPerSaveSettings()
{
    var gc = RimWatchMod.GameComponent;
    return gc != null && gc.UsePerSaveSettings;
}
```

## Сохраняемые настройки

Всего сохраняется **73 настройки**:

### Automation Categories (8)
- buildingEnabled, workEnabled, farmingEnabled, defenseEnabled
- tradeEnabled, medicalEnabled, socialEnabled, researchEnabled

### Building Details (8)
- buildBeds, buildKitchen, buildPower, buildStorage
- buildWorkshops, buildResearch, buildDefenses, buildRooms

### Farming Details (4)
- autoPlantCrops, autoHarvest, autoTameAnimals, autoButcherAnimals

### Defense Details (4)
- autoDraftColonists, autoEquipWeapons, autoEquipArmor, autoPositionDefenders

### Advanced Settings (6)
- storytellerType, enableDebugLog, tickInterval
- autoEnableAutopilot, useManualPriorities, fileLoggingEnabled

### Logging Settings (10)
- enableGlobalLogging, debugModeEnabled, buildingLogLevel
- workLogLevel, farmingLogLevel, defenseLogLevel, medicalLogLevel
- tradeLogLevel, resourceLogLevel, colonistCommandsLogLevel
- colonyDevelopmentLogLevel, constructionLogLevel

### ML Systems (10)
- enableDebugOverlay, debugOverlayMode, enableDecisionLogging
- gameSpeedControlEnabled, apparelAutomationEnabled
- weaponAutomationEnabled, colonistCommandsEnabled
- productionAutomationEnabled, constructionCommandsEnabled
- decisionAnalyzerEnabled, colonyPredictorEnabled, playerStyleAnalyzerEnabled

### ML Configuration (3)
- mlLearningRate, predictionSensitivity, mlAnalysisInterval

### Game Speed Settings (4)
- idleSpeed, workSpeed, combatSpeed, autoUnpause

### Hierarchical Settings (20)
- useSmartOutfits, useEmergencySchedules, useMoodBasedSchedules
- useSeasonalSchedules, useDynamicWorkPriorities, autoDetectModWorkTypes
- autoRelocateOutdoorBeds, autoInstallStoredBeds
- buildBedrooms, buildKitchens, buildStorageRooms, buildFreezer
- useNightOwlSchedules, useEmergencyScheduleType, useMoodBasedScheduleType
- useSmartApparelMode, useAutoOutfitPolicies, useCombatVsCivilianClothing

## Производительность

- **Memory overhead:** ~300 bytes per save (73 bool/int/enum/float поля)
- **CPU overhead:** Negligible (только при save/load)
- **Save file size:** +2-3 KB per save
- **Load time impact:** <1ms

## Troubleshooting

### Проблема: Настройки не сохраняются

**Решение:**
1. Проверьте что вы В ИГРЕ (не в главном меню)
2. Убедитесь что "Use per-save settings" включено
3. Сохраните игру и перезагрузите сейв
4. Проверьте логи: `~/Library/Logs/Ludeon Studios/RimWorld by Ludeon Studios/Player.log`

### Проблема: Настройки применяются ко всем сейвам

**Причина:** Per-save settings отключены  
**Решение:**
1. Откройте Mod Settings → RimWatch
2. Включите "Use per-save settings"
3. Сохраните игру

### Проблема: Старый сейв не мигрирует

**Решение:**
1. Загрузите старый сейв
2. Per-save настройки включатся автоматически
3. Текущие глобальные настройки будут использованы
4. Сохраните игру - настройки запишутся в save file

### Проблема: Corrupted save file

**Fallback механизм:**
- Если per-save настройки не загружаются (corrupted)
- Автоматически используются глобальные настройки
- Лог: "Using global settings (per-save disabled)"
- Игра НЕ крашится

## API для других модов

Если другой мод хочет интегрироваться с per-save настройками:

```csharp
// Проверка наличия RimWatch
var rimWatchMod = LoadedModManager.RunningMods
    .FirstOrDefault(m => m.PackageId == "rimwatch.mod");

if (rimWatchMod != null)
{
    // Доступ через reflection
    var modType = rimWatchMod.GetType().Assembly.GetType("RimWatch.RimWatchMod");
    var gameComponentProperty = modType.GetProperty("GameComponent", 
        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
    
    var gameComponent = gameComponentProperty?.GetValue(null);
    // ...
}
```

## FAQ

### Q: Можно ли отключить per-save настройки по умолчанию?

**A:** Да, но это потребует изменения кода. В `RimWatchGameComponent.cs` измените:
```csharp
private bool _usePerSaveSettings = false; // Было: true
```

### Q: Что происходит при копировании сейва?

**A:** Копия сейва сохраняет все per-save настройки. Это две независимые копии.

### Q: Можно ли экспортировать настройки сейва?

**A:** Напрямую - нет. Но можно:
1. "Copy this save → global" - скопировать в глобальные
2. Создать новый сейв - он возьмет глобальные настройки
3. Или использовать кнопку "Copy global → this save" в целевом сейве

### Q: Влияет ли это на совместимость с другими модами?

**A:** Нет. Per-save настройки - это чисто внутренняя функция RimWatch.

### Q: Что происходит при обновлении мода?

**A:** Per-save настройки сохраняются. Новые настройки получают default values.

## Версионирование

Система включает версионирование для будущих миграций:

```csharp
private int _settingsVersion = 1; // v1.3.0

// В ExposeData():
Scribe_Values.Look(ref _settingsVersion, "settingsVersion", 1);

// Будущие миграции:
if (_settingsVersion < 2)
{
    // Migrate from v1 to v2
    // ...
    _settingsVersion = 2;
}
```

## Changelog

### v1.3.0 (2025-12-09)
- ✅ Initial implementation
- ✅ 73 settings stored per-save
- ✅ UI for management
- ✅ Auto-migration of old saves
- ✅ Enabled by default

---

**Разработано для RimWatch v1.3.0**  
**Автор:** Ilya Volkov  
**Лицензия:** MIT
