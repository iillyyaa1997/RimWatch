# 📝 Check RimWatch Logs

Проверить логи RimWatch из файлов и показать анализ.

## 🎯 Что делает команда

1. **Проверяет наличие папки логов**
2. **Находит последний файл лога** (по timestamp в имени)
3. **Показывает последние 100 строк** из файла
4. **Ищет ERROR и WARNING сообщения**
5. **Выводит summary** найденных проблем

## 📋 Порядок выполнения

### 1. Определить OS и найти папку логов
- **macOS:** `~/Library/Application Support/RimWorld/RimWatch_Logs/`
- **Windows:** `C:\Users\<Username>\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\RimWatch_Logs\`
- **Linux:** `~/.config/unity3d/Ludeon Studios/RimWorld by Ludeon Studios/RimWatch_Logs/`

### 2. Показать список файлов логов
```bash
ls -lht "$LOG_DIR" | head -10
```
- Отсортировано по дате (новые сверху)
- Показать размер файлов

### 3. Прочитать последний файл
```bash
LATEST_LOG=$(ls -t "$LOG_DIR"/RimWatch_*.log 2>/dev/null | head -1)
```

### 4. Показать последние 100 строк
```bash
tail -100 "$LATEST_LOG"
```

### 5. Подсчитать ошибки и предупреждения
```bash
ERROR_COUNT=$(grep -c "\[ERROR\]" "$LATEST_LOG")
WARNING_COUNT=$(grep -c "\[WARNING\]" "$LATEST_LOG")
DEBUG_COUNT=$(grep -c "\[DEBUG\]" "$LATEST_LOG")
INFO_COUNT=$(grep -c "\[INFO\]" "$LATEST_LOG")
```

### 6. Показать последние ERROR
```bash
grep "\[ERROR\]" "$LATEST_LOG" | tail -10
```

### 7. Показать последние WARNING
```bash
grep "\[WARNING\]" "$LATEST_LOG" | tail -10
```

## 📊 Ожидаемый вывод

```
📂 RimWatch Logs Directory
Path: ~/Library/Application Support/RimWorld/RimWatch_Logs/
Status: ✅ Found

📁 Log Files (newest first):
-rw-r--r--  1 user  staff   1.2M Nov  7 15:30 RimWatch_2025-11-07_15-30-45.log
-rw-r--r--  1 user  staff   856K Nov  7 14:15 RimWatch_2025-11-07_14-15-20.log
-rw-r--r--  1 user  staff   2.1M Nov  7 10:05 RimWatch_2025-11-07_10-05-12.log

📝 Latest Log: RimWatch_2025-11-07_15-30-45.log (1.2 MB)

📊 Log Statistics:
- INFO:     1,234 messages
- DEBUG:    5,678 messages
- WARNING:     45 messages
- ERROR:        3 messages

⚠️ Last 10 WARNINGS:
[15:35:12.456] [WARNING] BuildingAutomation: Could not find suitable location for kitchen
[15:36:23.789] [WARNING] DefenseAutomation: No weapons available to equip (total: 8, forbidden: 8)
...

❌ Last 10 ERRORS:
[15:40:45.123] [ERROR] FarmingAutomation: Failed to create growing zone: System.NullReferenceException
[15:42:10.456] [ERROR] TradeAutomation: Trader not found on map
...

🔍 Analysis:
✅ Debug Mode: ENABLED (5,678 debug messages)
✅ File Logging: WORKING (last log 5 minutes ago)
⚠️ Warnings: 45 (mostly location finding issues)
❌ Errors: 3 (need investigation)

💡 Recommendations:
1. Check FarmingAutomation error on line [15:40:45.123]
2. Review DefenseAutomation weapon availability logic
3. BuildingAutomation needs better location finding
```

## 🔧 Дополнительные опции

### Показать весь файл (не только последние 100 строк)
```bash
cat "$LATEST_LOG"
```

### Поиск по ключевому слову
```bash
grep -i "DefenseAutomation" "$LATEST_LOG"
```

### Показать только ошибки с контекстом
```bash
grep -B 2 -A 2 "\[ERROR\]" "$LATEST_LOG"
```

### Статистика по категориям автоматизации
```bash
grep -o "\[.*Automation\]" "$LATEST_LOG" | sort | uniq -c | sort -rn
```

## 📝 Примечания

1. **Если папка не найдена:**
   - Убедись что File Logging включен в настройках
   - Запусти игру хотя бы раз с включенным File Logging
   - Проверь путь для своей OS

2. **Если файл слишком большой:**
   - Используй `tail -N` вместо `cat` (где N = количество строк)
   - Рассмотри сжатие старых логов: `gzip RimWatch_*.log`

3. **Если много DEBUG логов:**
   - Это нормально при включенном Debug Mode
   - Выключи Debug Mode если не нужна детальная диагностика

## 🚀 Быстрые команды

### Открыть папку логов
```bash
# macOS
open "$HOME/Library/Application Support/RimWorld/RimWatch_Logs"

# Linux
xdg-open "$HOME/.config/unity3d/Ludeon Studios/RimWorld by Ludeon Studios/RimWatch_Logs"

# Windows (PowerShell)
explorer "C:\Users\$env:USERNAME\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\RimWatch_Logs"
```

### Удалить старые логи (>7 дней)
```bash
find "$LOG_DIR" -name "RimWatch_*.log" -mtime +7 -delete
```

### Объединить все логи в один файл
```bash
cat "$LOG_DIR"/RimWatch_*.log > all_logs.txt
```

## 🎯 Пример использования

**User:** `@check-logs`

**Assistant:** 
1. Определяет OS (macOS)
2. Проверяет папку `~/Library/Application Support/RimWorld/RimWatch_Logs/`
3. Находит последний файл: `RimWatch_2025-11-07_15-30-45.log`
4. Читает файл и показывает последние 100 строк
5. Подсчитывает ошибки: 3 ERROR, 45 WARNING
6. Выводит последние ошибки и предупреждения
7. Дает рекомендации по исправлению

