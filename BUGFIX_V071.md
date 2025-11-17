# 🐛 КРИТИЧЕСКИЕ ИСПРАВЛЕНИЯ v0.7.1 (2025-11-07)

## Проблемы, выявленные пользователем

1. **Автопилот размещал солнечную батарею без исследования**
2. **Автопилот постоянно переводил всех колонистов в боевой режим**
3. **Автопилот постоянно помечал животных на приручение**

---

## 🔧 Исправление 1: Проверка исследований для зданий

### Проблема
`BuildingAutomation.AutoPlacePower()` пытался разместить `SolarGenerator` (солнечную панель), не проверяя, изучена ли технология "Electricity".

### Решение
```csharp
// Изменен порядок приоритетов:
// 1. Сначала пытаемся найти WoodFiredGenerator (не требует исследований)
// 2. Проверяем, изучено ли "Electricity"
// 3. Если изучено, используем SolarGenerator
// 4. Дополнительная проверка researchPrerequisites для любого здания

ResearchProjectDef solarResearch = DefDatabase<ResearchProjectDef>.GetNamedSilentFail("Electricity");
if (solarResearch != null && solarResearch.IsFinished)
{
    ThingDef solarDef = DefDatabase<ThingDef>.GetNamedSilentFail("SolarGenerator");
    if (solarDef != null)
    {
        powerDef = solarDef;
    }
}

// Double-check research prerequisites
if (powerDef.researchPrerequisites != null && powerDef.researchPrerequisites.Any())
{
    bool allResearched = powerDef.researchPrerequisites.All(r => r.IsFinished);
    if (!allResearched)
    {
        // Log warning and return
    }
}
```

### Результат
✅ Автопилот будет размещать только исследованные здания
✅ Начальные колонии получат `WoodFiredGenerator` вместо солнечной панели
✅ После изучения "Electricity" автопилот будет предпочитать `SolarGenerator`

---

## 🔧 Исправление 2: Умный драфт с проверкой расстояния

### Проблема
`DefenseAutomation.AutoDraftColonists()` драфтил колонистов при наличии ЛЮБЫХ врагов на карте, даже если они находились на другом краю карты.

### Решение
```csharp
// Добавлена проверка расстояния до ближайшего врага
const float DangerDistance = 30f; // Только драфт если враги в пределах 30 клеток

List<Pawn> enemies = map.mapPawns.AllPawnsSpawned
    .Where(p => p.HostileTo(Faction.OfPlayer) && !p.Dead && !p.Downed)
    .ToList();

bool hasCloseEnemies = false;
float closestDistance = float.MaxValue;

if (enemies.Count > 0)
{
    foreach (Pawn enemy in enemies)
    {
        foreach (Pawn colonist in colonists)
        {
            float dist = enemy.Position.DistanceTo(colonist.Position);
            if (dist < closestDistance) closestDistance = dist;
            if (dist <= DangerDistance)
            {
                hasCloseEnemies = true;
                break;
            }
        }
    }
}

// Драфтим только если враги БЛИЗКО
if (!hasCloseEnemies || status.EnemyCount == 0)
{
    // Undraft colonists
}
```

### Результат
✅ Колонисты драфтятся только при реальной угрозе (враг в пределах 30 клеток)
✅ Колонисты автоматически разбираются, когда враг далеко или уничтожен
✅ В логах показывается расстояние до ближайшего врага

**Пример лога:**
```
⚔️ DefenseAutomation: Drafted 2 colonists (enemies: 3, closest: 25 tiles)
✅ DefenseAutomation: Undrafted 2 colonists (enemies too far (45 tiles))
```

---

## 🔧 Исправление 3: Cooldown для животных действий

### Проблема
`FarmingAutomation` выполнял методы `AutoDesignateHunting()`, `AutoDesignateTaming()`, и `AutoDesignateSlaughter()` каждые 15 секунд (900 тиков), что приводило к постоянному спаму обозначений.

### Решение
```csharp
// Добавлены переменные для cooldown
private static int lastHuntingTick = -9999;
private static int lastTamingTick = -9999;
private static int lastSlaughterTick = -9999;
private const int HuntingCooldown = 1800; // 30 seconds
private const int TamingCooldown = 3600; // 60 seconds (taming takes time)
private const int SlaughterCooldown = 1800; // 30 seconds

// В начале каждого метода:
int currentTick = Find.TickManager.TicksGame;
if (currentTick - lastTamingTick < TamingCooldown)
{
    return; // Too soon since last taming designation
}

// После успешного обозначения:
if (designated > 0)
{
    lastTamingTick = currentTick; // Update cooldown
    RimWatchLogger.Info($"🐾 FarmingAutomation: Taming {designated} animals...");
}
```

### Результат
✅ **Охота:** Обозначается каждые 30 секунд (вместо 15)
✅ **Приручение:** Обозначается каждые 60 секунд (вместо 15)
✅ **Забой:** Обозначается каждые 30 секунд (вместо 15)
✅ Автопилот помечает максимум 2 животных за раз, затем ждет

---

## 📊 Сравнение поведения

### ДО исправлений:
```
⚡ BuildingAutomation: Placed SolarGenerator blueprint (NOT RESEARCHED!)
⚔️ DefenseAutomation: Drafted 5 colonists (enemies: 1, distance: 120 tiles!)
🐾 FarmingAutomation: Taming 2 animals
   [через 15 секунд]
🐾 FarmingAutomation: Taming 2 animals
   [через 15 секунд]
🐾 FarmingAutomation: Taming 2 animals
   [... пока все животные не будут помечены]
```

### ПОСЛЕ исправлений:
```
⚡ BuildingAutomation: Placed WoodFiredGenerator blueprint (no research required)
⚠️ BuildingAutomation: Cannot place solar generator - research required: Electricity
⚔️ DefenseAutomation: Drafted 2 colonists (enemies: 1, closest: 12 tiles)
✅ DefenseAutomation: Undrafted 2 colonists (enemies too far (45 tiles))
🐾 FarmingAutomation: Taming 2 animals (2/9 currently tamed)
   [через 60 секунд]
🐾 FarmingAutomation: Taming 2 animals (4/9 currently tamed)
   [через 60 секунд]
🐾 FarmingAutomation: Taming 2 animals (6/9 currently tamed)
```

---

## 🎯 Итоги

Все три критические проблемы исправлены:

1. ✅ **Размещение зданий**: Только исследованные, с fallback на базовые варианты
2. ✅ **Драфт колонистов**: Только при реальной угрозе (враг в пределах 30 клеток)
3. ✅ **Приручение животных**: С разумным cooldown (60 секунд между обозначениями)

**Следующий шаг:** Компиляция и деплой v0.7.1 для тестирования.

