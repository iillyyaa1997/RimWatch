using RimWatch.Core;
using RimWatch.Utils;
using RimWorld;
using System.Linq;
using Verse;

namespace RimWatch.Automation
{
    /// <summary>
    /// 🔬 Research Automation - автоматическое управление исследованиями.
    /// Выбирает и управляет исследовательскими проектами.
    /// </summary>
    public static class ResearchAutomation
    {
        private static int _tickCounter = 0;
        private static bool _isEnabled = false;
        private const int UpdateInterval = 1800; // Обновление каждые 30 секунд (1800 тиков)

        /// <summary>
        /// Включена ли автоматизация исследований.
        /// </summary>
        public static bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                RimWatchLogger.Info($"ResearchAutomation: {(value ? "Enabled" : "Disabled")}");
            }
        }

        /// <summary>
        /// Вызывается каждый тик для обновления автоматизации.
        /// </summary>
        public static void Tick()
        {
            if (!IsEnabled) return;
            if (!RimWatchCore.AutopilotEnabled) return;

            _tickCounter++;
            if (_tickCounter >= UpdateInterval)
            {
                _tickCounter = 0;
                RimWatchLogger.Info("[ResearchAutomation] Tick! Checking research status...");
                ManageResearch();
            }
        }

        /// <summary>
        /// Manages research selection.
        /// </summary>
        private static void ManageResearch()
        {
            ResearchManager researchManager = Find.ResearchManager;
            if (researchManager == null) return;

            // Check current research
            ResearchProjectDef currentProject = researchManager.GetProject();
            
            if (currentProject != null)
            {
                float progress = currentProject.ProgressPercent;
                RimWatchLogger.Info($"ResearchAutomation: Currently researching '{currentProject.label}' ({progress:P0} complete)");
                return;
            }

            // Select new research if none active
            ResearchProjectDef nextProject = SelectNextResearch();
            if (nextProject != null)
            {
                Find.ResearchManager.SetCurrentProject(nextProject);
                RimWatchLogger.Info($"ResearchAutomation: ✓ Started new research: '{nextProject.label}'");
            }
            else
            {
                RimWatchLogger.Debug("ResearchAutomation: No available research projects");
            }
        }

        /// <summary>
        /// Selects the next research project based on priorities.
        /// </summary>
        private static ResearchProjectDef SelectNextResearch()
        {
            var availableProjects = DefDatabase<ResearchProjectDef>.AllDefsListForReading
                .Where(r => r.CanStartNow && !r.IsFinished)
                .ToList();

            if (availableProjects.Count == 0)
                return null;

            // Priority 1: Essential tech (electricity, medicine)
            var essentialResearch = availableProjects
                .FirstOrDefault(r => r.defName.ToLower().Contains("electric") || 
                                   r.defName.ToLower().Contains("medic"));
            if (essentialResearch != null)
                return essentialResearch;

            // Priority 2: Agriculture
            var agriResearch = availableProjects
                .FirstOrDefault(r => r.defName.ToLower().Contains("farm") || 
                                   r.defName.ToLower().Contains("grow"));
            if (agriResearch != null)
                return agriResearch;

            // Priority 3: First available by cost (easier first)
            return availableProjects.OrderBy(r => r.CostApparent).FirstOrDefault();
        }
    }
}
