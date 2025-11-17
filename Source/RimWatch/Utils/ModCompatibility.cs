using RimWatch.Utils;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimWatch.Utils
{
    /// <summary>
    /// Mod compatibility detection and management.
    /// </summary>
    public static class ModCompatibility
    {
        private static List<ModContentPack> _loadedMods = null;
        private static Dictionary<string, List<WorkTypeDef>> _modWorkTypes = new Dictionary<string, List<WorkTypeDef>>();

        /// <summary>
        /// Gets all loaded mods.
        /// </summary>
        public static List<ModContentPack> GetLoadedMods()
        {
            if (_loadedMods == null)
            {
                _loadedMods = LoadedModManager.RunningMods.ToList();
                RimWatchLogger.Info($"ModCompatibility: Detected {_loadedMods.Count} loaded mods");
            }
            return _loadedMods;
        }

        /// <summary>
        /// Checks if a specific mod is loaded.
        /// </summary>
        public static bool IsModLoaded(string packageId)
        {
            return ModsConfig.IsActive(packageId);
        }

        /// <summary>
        /// Gets work types added by a specific mod.
        /// </summary>
        public static List<WorkTypeDef> GetModWorkTypes(string packageId)
        {
            if (_modWorkTypes.TryGetValue(packageId, out List<WorkTypeDef> cached))
            {
                return cached;
            }

            var workTypes = DefDatabase<WorkTypeDef>.AllDefsListForReading
                .Where(w => w.modContentPack?.PackageId == packageId)
                .ToList();

            _modWorkTypes[packageId] = workTypes;
            return workTypes;
        }

        /// <summary>
        /// Logs all mod-added work types for diagnostics.
        /// </summary>
        public static void LogModWorkTypes()
        {
            RimWatchLogger.Info("=== Mod Work Types Detection ===");

            var allWorkTypes = DefDatabase<WorkTypeDef>.AllDefsListForReading;
            var groupedByMod = allWorkTypes.GroupBy(w => w.modContentPack?.PackageId ?? "vanilla");

            foreach (var group in groupedByMod)
            {
                string modName = group.First().modContentPack?.Name ?? "Vanilla";
                RimWatchLogger.Info($"[{modName}] ({group.Key}):");
                
                foreach (var workType in group)
                {
                    RimWatchLogger.Info($"  - {workType.defName} ({workType.labelShort})");
                }
            }

            RimWatchLogger.Info("================================");
        }

        /// <summary>
        /// Detects common mod frameworks.
        /// </summary>
        public static void DetectCommonMods()
        {
            RimWatchLogger.Info("=== Common Mod Detection ===");

            CheckMod("prison.labor", "Prison Labor");
            CheckMod("androic tiers", "Android Tiers");
            CheckMod("misc.robots", "Misc. Robots");
            CheckMod("orion.hospitality", "Hospitality");
            CheckMod("dubsbadhygiene", "Dubs Bad Hygiene");

            RimWatchLogger.Info("============================");
        }

        private static void CheckMod(string packageIdPart, string modName)
        {
            var mods = GetLoadedMods();
            bool found = mods.Any(m => m.PackageId.ToLower().Contains(packageIdPart.ToLower()));
            
            if (found)
            {
                RimWatchLogger.Info($"  ✓ {modName} detected");
            }
        }

        /// <summary>
        /// Clears cached data.
        /// </summary>
        public static void ClearCache()
        {
            _loadedMods = null;
            _modWorkTypes.Clear();
        }
    }
}

