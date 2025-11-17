# RimWatch - Development Guidelines

## 🌐 Logging Policy

### Critical Rule: English-Only Logs

**All log messages MUST be written in English.**

#### Why?
- **International Support**: Enables developers worldwide to help debug issues
- **Stack Trace Readability**: Mixed-language stack traces are confusing
- **GitHub Issues**: International community can understand bug reports
- **Professional Standard**: Industry best practice for open-source projects

#### Examples

✅ **CORRECT** (English logs):
```csharp
RimWatchLogger.Info("RimWatch initialization completed");
RimWatchLogger.Debug("Colony analyzer processing 15 pawns");
RimWatchLogger.Warning("Low TPS detected, adjusting AI priorities");
RimWatchLogger.Error("Failed to load storyteller config: file not found");
```

❌ **INCORRECT** (Russian logs):
```csharp
RimWatchLogger.Info("Инициализация RimWatch завершена");
RimWatchLogger.Debug("Анализатор колонии обрабатывает 15 поселенцев");
```

### Current Status

⚠️ **Temporary Exception**: Some existing logs use Russian for development convenience.

**Action Required**: All Russian logs will be converted to English before version 0.5 release.

### Migration Plan

1. **Phase 1** (v0.1 → v0.3): New code must use English logs only
2. **Phase 2** (v0.3 → v0.5): Convert existing Russian logs to English
3. **Phase 3** (v0.5+): Enforce English-only via code review

---

## 🌍 Localization Strategy

### UI Localization (v1.5+)

User-facing strings WILL be localized via RimWorld's standard XML system:

```xml
<!-- Languages/English/Keyed/UI.xml -->
<LanguageData>
  <RimWatch.UI.MainButton>RimWatch Autopilot</RimWatch.UI.MainButton>
  <RimWatch.Storyteller.Balanced>⚖️ Balanced Manager</RimWatch.Storyteller.Balanced>
  <RimWatch.Category.Work>Work Management</RimWatch.Category.Work>
</LanguageData>
```

```xml
<!-- Languages/Russian/Keyed/UI.xml -->
<LanguageData>
  <RimWatch.UI.MainButton>Автопилот RimWatch</RimWatch.UI.MainButton>
  <RimWatch.Storyteller.Balanced>⚖️ Сбалансированный Менеджер</RimWatch.Storyteller.Balanced>
  <RimWatch.Category.Work>Управление работой</RimWatch.Category.Work>
</LanguageData>
```

### Localized vs Non-Localized

| Content Type | Localized? | Language |
|--------------|-----------|----------|
| **Log messages** | ❌ NO | English only |
| **Exception messages** | ❌ NO | English only |
| **Code comments** | ❌ NO | English preferred |
| **UI text** | ✅ YES | All supported |
| **Tooltips** | ✅ YES | All supported |
| **Descriptions** | ✅ YES | All supported |
| **Documentation** | ✅ YES | All supported |

---

## 📝 Code Style

### Naming Conventions

```csharp
// Classes: PascalCase
public class RimWatchCore { }

// Methods: PascalCase
public void InitializeAutopilot() { }

// Private fields: camelCase with underscore
private readonly RimWatchLogger _logger;

// Properties: PascalCase
public bool AutopilotEnabled { get; set; }

// Constants: UPPER_SNAKE_CASE
private const string LOG_PREFIX = "[RimWatch]";
```

### Comments

Prefer English comments for better collaboration:

```csharp
// ✅ GOOD: English comment
// Initialize the AI storyteller with default balanced settings
private void InitializeStoryteller()
{
    // ...
}

// ❌ AVOID: Russian comment (unless temporary)
// Инициализирует AI-рассказчика с настройками по умолчанию
private void InitializeStoryteller()
{
    // ...
}
```

**Exception**: Temporary development notes in any language are acceptable, but must be removed or translated before PR/release.

---

## 🧪 Testing Guidelines

### Test Naming

```csharp
// English test names for international readability
[Fact]
public void AutopilotToggle_WhenDisabled_ShouldStopAllAutomations()
{
    // Arrange
    var core = new RimWatchCore();
    core.AutopilotEnabled = true;
    
    // Act
    core.ToggleAutopilot();
    
    // Assert
    Assert.False(core.AutopilotEnabled);
    Assert.False(core.WorkEnabled);
}
```

### Test Output

All test assertions and failure messages must be in English:

```csharp
Assert.True(result, "Autopilot should be enabled after initialization");
// NOT: "Автопилот должен быть включен после инициализации"
```

---

## 📚 Documentation

### Code Documentation

Use XML documentation comments in English:

```csharp
/// <summary>
/// Initializes the RimWatch core system and all automation modules.
/// </summary>
/// <remarks>
/// This method should be called once during mod initialization.
/// It sets up the default Balanced Storyteller and enables core systems.
/// </remarks>
public void Initialize()
{
    // Implementation
}
```

### README and Guides

- **README.md**: Bilingual (English + Russian sections)
- **Technical docs**: English preferred
- **User guides**: Will be localized in v1.5

---

## 🔧 Development Tools

### Required Setup

1. **StyleCop**: Enforces C# coding standards
2. **EditorConfig**: Consistent formatting
3. **Docker**: Isolated build environment

### Pre-commit Checklist

- [ ] All new logs are in English
- [ ] No Russian comments in production code
- [ ] Code follows StyleCop rules
- [ ] Tests pass (`make test`)
- [ ] Build succeeds (`make build`)

---

## 🚀 Release Checklist

### Before Each Release

1. **Code Audit**:
   - [ ] All log messages are in English
   - [ ] No debug/temp comments in Russian
   - [ ] Exception messages are in English

2. **Documentation**:
   - [ ] CHANGELOG updated (English)
   - [ ] Release notes prepared (English + Russian)
   - [ ] README reflects new features

3. **Localization** (v1.5+):
   - [ ] All new UI strings have English keys
   - [ ] Existing translations updated
   - [ ] Fallbacks tested

---

## 🌟 Best Practices

### Logging Best Practices

```csharp
// ✅ GOOD: Structured, English, informative
RimWatchLogger.Info($"Autopilot initialized with storyteller: {storyteller.Name}");
RimWatchLogger.Debug($"Processing {pawnCount} pawns for work assignment");
RimWatchLogger.Warning($"Low TPS detected: {tps:F1}, reducing AI frequency");
RimWatchLogger.Error($"Failed to load config: {ex.Message}", ex);

// ❌ BAD: Vague, Russian, or missing context
RimWatchLogger.Info("Done");
RimWatchLogger.Info("Инициализация завершена");
RimWatchLogger.Error("Error occurred");
```

### User-Facing Messages

Always use localization keys for UI text:

```csharp
// ✅ GOOD: Localizable
string message = "RimWatch.Notification.AutopilotEnabled".Translate();

// ❌ BAD: Hardcoded text
string message = "Autopilot enabled";
string message = "Автопилот включен";
```

---

## 🤝 Contributing

### For International Contributors

- **Logs**: Must be in English (use Google Translate if needed)
- **Comments**: English preferred, but not strictly required for draft PRs
- **Commit messages**: English only
- **PR descriptions**: English required, additional languages welcome

### For Russian Contributors

Используйте английский для:
- Логов (обязательно)
- Сообщений об ошибках (обязательно)
- Commit messages (обязательно)

Можно использовать русский для:
- Черновых комментариев (будут переведены перед релизом)
- PR описаний (дополнительно к английскому)
- Обсуждений в Issues

---

## 📞 Questions?

If you're unsure whether to use English or Russian for something:
- **Ask in GitHub Discussions**
- **Default to English** when in doubt
- **Refer to this document** for guidelines

---

**Last Updated**: November 7, 2025  
**Version**: 1.0  
**Status**: 🟢 Active

