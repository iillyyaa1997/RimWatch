# ⚠️ Build Issue - RimWorld Libraries Not Found

**Дата:** 7 ноября 2025  
**Проблема:** Docker не может найти библиотеки RimWorld для Release сборки

---

## 🐛 Проблема

При запуске `make deploy` или `make build` получаем ошибку:

```
error CS0246: The type or namespace name 'Verse' could not be found
error CS0246: The type or namespace name 'RimWorld' could not be found
error CS0246: The type or namespace name 'UnityEngine' could not be found
```

**Причина:** Docker контейнер не может получить доступ к библиотекам RimWorld, которые нужны для Release сборки.

---

## 📍 Путь к библиотекам

**macOS:**
```
/Users/ilyavolkov/Library/Application Support/Steam/steamapps/common/RimWorld/RimWorldMac.app/Contents/Resources/Data/Managed/
```

**Нужные файлы:**
- `Assembly-CSharp.dll` - Основной код RimWorld
- `UnityEngine.CoreModule.dll` - Unity Engine
- `UnityEngine.InputLegacyModule.dll` - Unity Input

---

## 🔧 Решения

### Вариант 1: Скопировать библиотеки локально (рекомендуется)

```bash
cd RimWatch

# Создать директорию для библиотек
mkdir -p RimWorldLibs

# Скопировать нужные DLL
cp "/Users/ilyavolkov/Library/Application Support/Steam/steamapps/common/RimWorld/RimWorldMac.app/Contents/Resources/Data/Managed/Assembly-CSharp.dll" RimWorldLibs/
cp "/Users/ilyavolkov/Library/Application Support/Steam/steamapps/common/RimWorld/RimWorldMac.app/Contents/Resources/Data/Managed/UnityEngine.CoreModule.dll" RimWorldLibs/
cp "/Users/ilyavolkov/Library/Application Support/Steam/steamapps/common/RimWorld/RimWorldMac.app/Contents/Resources/Data/Managed/UnityEngine.InputLegacyModule.dll" RimWorldLibs/

# Обновить docker-compose.yml чтобы монтировать ./RimWorldLibs
# Заменить:
#   - "/Users/.../Managed:/app/RimWorldLibs:ro"
# На:
#   - ./RimWorldLibs:/app/RimWorldLibs:ro
```

### Вариант 2: Использовать Mock References (Debug только)

Mock references уже созданы в `Source/MockReferences/VerseMock.cs`, но они работают только для Debug сборки. Release сборке нужны настоящие библиотеки.

### Вариант 3: Собрать локально (без Docker)

Если .NET SDK установлен локально:

```bash
cd Source/RimWatch

# Установить переменную окружения с путём к RimWorld
export RimWorldInstallDir="/Users/ilyavolkov/Library/Application Support/Steam/steamapps/common/RimWorld/RimWorldMac.app/Contents/Resources"

# Собрать
dotnet build --configuration Release
```

---

## ✅ Временное решение

**Для продолжения разработки:**

1. Скопируй библиотеки локально (Вариант 1)
2. Обнови `docker-compose.yml`
3. Запусти `make build` снова

**Или:**

Используй Debug сборку с Mock references (пока не работает Runtime, только для проверки компиляции):
```bash
docker-compose run build bash -c "cd /app/Source/RimWatch && dotnet build --configuration Debug"
```

---

## 📝 TODO для следующей сессии

1. ✅ Создать скрипт для автоматического копирования библиотек
2. ✅ Обновить docker-compose.yml для использования локальных копий
3. ✅ Добавить проверку наличия библиотек в `build.sh`
4. ✅ Документировать процесс в README

---

## 💡 Почему это происходит?

RimWorld модам нужны референсы на игровые библиотеки для компиляции:
- `Assembly-CSharp.dll` - содержит Verse, RimWorld namespaces
- `UnityEngine.*.dll` - содержит Unity Engine классы

Docker не может напрямую монтировать пути с пробелами в macOS из-за ограничений Docker Desktop.

---

## 🚀 Действия

**СЕЙЧАС:**
Скопируем библиотеки локально и обновим конфигурацию.

**Последнее обновление:** 7 ноября 2025

