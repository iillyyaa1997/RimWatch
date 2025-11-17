# 🎯 Решение проблемы: Мод не отображался в RimWorld

## 🔍 Проблема
Мод RimWatch не появлялся в списке модов игры, несмотря на корректную сборку и установку.

## 🏆 Решение
**Мод должен быть установлен внутри .app bundle, а не в внешнюю папку!**

### ❌ Неправильный путь (не работал)
```
~/Library/Application Support/RimWorld/Mods/RimWatch
~/Library/Application Support/Steam/steamapps/common/RimWorld/Mods/RimWatch
```

### ✅ Правильный путь (работает)
```
~/Library/Application Support/Steam/steamapps/common/RimWorld/RimWorldMac.app/Mods/RimWatch
```

## 🔬 Как я нашёл проблему

1. **Сравнение с рабочим модом**: RimAsync отображался, RimWatch — нет
2. **Поиск RimAsync в системе**:
   ```bash
   mdfind -name RimAsync | head -20
   ```
3. **Поиск папки Mods**:
   ```bash
   find ~/Library/Application\ Support/Steam/steamapps/common/RimWorld -type d -name "Mods"
   ```
   Результат показал **ДВЕ** папки:
   - `RimWorld/Mods/` — внешняя (НЕ сканируется)
   - `RimWorld/RimWorldMac.app/Mods/` — **внутри бандла** (сканируется!)

4. **Проверка содержимого**:
   ```bash
   ls -la ~/Library/Application\ Support/Steam/steamapps/common/RimWorld/RimWorldMac.app/Mods/
   ```
   Там был файл `Place mods here.txt` и папка `RimAsync` ✅

## 🛠️ Исправления в проекте

### 1. Обновлён `Makefile`
Изменён дефолтный путь установки модов:

**Было**:
```makefile
RIMWORLD_MODS="$(HOME)/Library/Application Support/RimWorld/Mods"
```

**Стало**:
```makefile
RIMWORLD_MODS="$(HOME)/Library/Application Support/Steam/steamapps/common/RimWorld/RimWorldMac.app/Mods"
```

Добавлены автоматические фиксы при установке:
```makefile
chmod 644 "$$RIMWORLD_MODS/RimWatch/Assemblies"/*.dll 2>/dev/null || true
rm -f "$$RIMWORLD_MODS/RimWatch/About/Preview.png" 2>/dev/null || true
```

### 2. Создан `.env.example`
Шаблон для кастомизации пути установки с примерами для разных конфигураций.

### 3. Минимизирован `About.xml`
Удалён `<modIconPath>` (пустой Preview.png блокировал загрузку).

## 📝 Важные детали

### Права доступа
DLL файлы должны иметь права `644` (rw-r--r--), а не `600` (rw-------).
```bash
chmod 644 RimWatch.dll
```

### Структура мода (обратная совместимость)
RimWorld 1.6 поддерживает оба варианта:
- Новый: `1.6/Assemblies/RimWatch.dll`
- Старый: `Assemblies/RimWatch.dll` ✅ (используется)

Мы используем старый для максимальной совместимости.

### Файл Preview.png
Если указан `<modIconPath>` в About.xml, файл **не должен быть пустым**.
Лучше вообще удалить `<modIconPath>`, если изображения нет.

## 🚀 Использование

Теперь стандартный деплой работает из коробки:
```bash
cd RimWatch
make deploy
```

Для кастомного пути:
1. Создай `.env` файл
2. Скопируй из `.env.example`
3. Измени `RIMWORLD_MODS_PATH`

## 🎯 Тестирование

Проверь что мод виден:
1. Полностью выйди из RimWorld (Cmd+Q)
2. Запусти игру снова
3. Открой Mods
4. RimWatch должен быть в списке между R-S

## 🔗 См. также
- [RimWorld Mod Structure Documentation](https://rimworldwiki.com/wiki/Modding_Tutorials)
- [macOS .app Bundle Structure](https://developer.apple.com/library/archive/documentation/CoreFoundation/Conceptual/CFBundles/BundleTypes/BundleTypes.html)

