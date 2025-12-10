using RimWatch.AI;
using RimWatch.AI.Storytellers;
using RimWatch.Utils;
using System.Linq;
using Verse;

namespace RimWatch.Core
{
    /// <summary>
    /// Ядро RimWatch - центральная точка управления
    /// </summary>
    public static class RimWatchCore
    {
        // Статус автопилота
        public static bool AutopilotEnabled { get; set; } = false;

        // Активные категории автоматизации (v0.1 - только Work)
        public static bool WorkEnabled { get; set; } = false;
        public static bool BuildingEnabled { get; set; } = false;
        public static bool FarmingEnabled { get; set; } = false;
        public static bool ResourceEnabled { get; set; } = false; // v1.3.1: Resource gathering
        public static bool DefenseEnabled { get; set; } = false;
        public static bool TradeEnabled { get; set; } = false;
        public static bool MedicalEnabled { get; set; } = false;
        public static bool SocialEnabled { get; set; } = false;
        public static bool ResearchEnabled { get; set; } = false;

        // Текущий рассказчик
        public static AIStoryteller CurrentStoryteller { get; private set; }

        // Счетчик активных автоматизаций
        public static int ActiveAutomationsCount
        {
            get
            {
                int count = 0;
                if (WorkEnabled) count++;
                if (BuildingEnabled) count++;
                if (FarmingEnabled) count++;
                if (ResourceEnabled) count++; // v1.3.1
                if (DefenseEnabled) count++;
                if (TradeEnabled) count++;
                if (MedicalEnabled) count++;
                if (SocialEnabled) count++;
                if (ResearchEnabled) count++;
                return count;
            }
        }

        /// <summary>
        /// Переключить автопилот
        /// </summary>
        public static void ToggleAutopilot()
        {
            AutopilotEnabled = !AutopilotEnabled;
            
            if (AutopilotEnabled)
            {
                RimWatchLogger.Info("Autopilot ENABLED");
                // TODO: Запустить системы автоматизации
            }
            else
            {
                RimWatchLogger.Info("Autopilot DISABLED");
                // TODO: Остановить системы автоматизации
            }
        }

        /// <summary>
        /// Получить статус автопилота
        /// </summary>
        public static AutopilotStatus GetStatus()
        {
            if (!AutopilotEnabled)
            {
                return AutopilotStatus.Disabled;
            }

            // TODO: Добавить проверку на warnings (например, мало еды)
            // if (HasWarnings())
            // {
            //     return AutopilotStatus.ActiveWarning;
            // }

            return AutopilotStatus.ActiveGood;
        }

        /// <summary>
        /// Инициализация ядра
        /// </summary>
        public static void Initialize()
        {
            RimWatchLogger.Info("RimWatchCore initialized");
            
            // Создаем рассказчика по умолчанию (Balanced Manager)
            CurrentStoryteller = new BalancedStoryteller();
            RimWatchLogger.Info($"Default storyteller: {CurrentStoryteller.GetFullName()}");
            
            // v0.5: Применяем настройки из Settings
            // НЕ устанавливаем значения напрямую - они будут установлены из RimWatchSettings
            RimWatchLogger.Info("Core initialization complete - waiting for settings application");
            
            // v1.1.0: Run ML systems validation at startup
            try
            {
                RimWatchLogger.Info("ML Systems: Running integration validation...");
                var validationResult = RimWatch.ML.MLSystemsIntegration.ValidateIntegration();
                
                if (validationResult.AllPassed)
                {
                    RimWatchLogger.Info("✅ ML Systems: All integration checks passed!");
                }
                else
                {
                    int failedCount = validationResult.TotalChecks - validationResult.PassedChecks;
                    RimWatchLogger.Warning($"⚠️ ML Systems: {failedCount} checks failed:");
                    foreach (var check in validationResult.Checks.Where(c => !c.Value.Success))
                    {
                        RimWatchLogger.Warning($"  - {check.Key}: {check.Value.Message}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                RimWatchLogger.Error($"ML Systems validation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Главный тик ядра - вызывается каждый игровой тик
        /// </summary>
        public static void Tick()
        {
            if (!AutopilotEnabled) return;

            // Тик текущего рассказчика
            CurrentStoryteller?.Tick();
        }

        /// <summary>
        /// Меняет текущего AI-рассказчика
        /// </summary>
        public static void ChangeStoryteller(AIStoryteller newStoryteller)
        {
            if (newStoryteller == null)
            {
                RimWatchLogger.Warning("Attempted to set null storyteller!");
                return;
            }

            // Деактивируем старого
            if (CurrentStoryteller != null && AutopilotEnabled)
            {
                CurrentStoryteller.OnDeactivated();
            }

            // Активируем нового
            CurrentStoryteller = newStoryteller;
            RimWatchLogger.Info($"Storyteller changed to: {CurrentStoryteller.GetFullName()}");

            if (AutopilotEnabled)
            {
                CurrentStoryteller.OnActivated();
            }
        }
    }

    /// <summary>
    /// Статус автопилота для цветовой индикации
    /// </summary>
    public enum AutopilotStatus
    {
        ActiveGood,      // 🟢 Все хорошо
        ActiveWarning,   // 🟡 Есть предупреждения
        Disabled,        // 🔴 Выключен
        Inactive         // ⚫ Неактивен
    }
}

