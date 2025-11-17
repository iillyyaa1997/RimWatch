# RimWorld 1.6 Prisoner API Test

## Проблема
Не можем найти правильное свойство для установки режима взаимодействия с заключенными.

## Тестируемые варианты:

### 1. `prisoner.guest.InteractionMode` (PascalCase)
```csharp
prisoner.guest.InteractionMode = PrisonerInteractionModeDefOf.AttemptRecruit;
```
**Статус:** ❌ Ошибка компиляции - свойство не найдено

### 2. `prisoner.guest.interactionMode` (camelCase)
```csharp
prisoner.guest.interactionMode = PrisonerInteractionModeDefOf.AttemptRecruit;
```
**Статус:** ❌ Ошибка компиляции - свойство не найдено

### 3. Возможные альтернативы:
- `prisoner.guest.SetInteractionMode()`
- `prisoner.guest.PrisonerInteractionMode`
- `prisoner.guest.hostileInteractionMode`

## Нужно проверить:
1. Реальную структуру `Pawn_GuestTracker` в RimWorld 1.6
2. Изменилось ли API по сравнению с предыдущими версиями
3. Может быть нужен Harmony патч для этого функционала

## Временное решение:
Сейчас SocialAutomation работает в режиме "рекомендаций" - только логирует, что нужно сделать.

## TODO:
- [ ] Изучить DLL RimWorld 1.6 через decompiler
- [ ] Проверить примеры из других модов
- [ ] Создать минимальный тест в игре

