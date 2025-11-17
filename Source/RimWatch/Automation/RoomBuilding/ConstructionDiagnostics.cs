using RimWatch.Utils;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace RimWatch.Automation.RoomBuilding
{
    /// <summary>
    /// Диагностирует почему строительство не завершается.
    /// Проверяет colonists, их приоритеты, доступность материалов, reachability.
    /// </summary>
    public static class ConstructionDiagnostics
    {
        /// <summary>
        /// Проверяет почему комнаты не достраиваются и логирует проблемы.
        /// Вызывать раз в минуту для диагностики.
        /// </summary>
        public static void DiagnoseUnfinishedConstruction(Map map)
        {
            try
            {
                // v0.8.4: Early exit if no colonists to avoid spam
                List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned.ToList();
                if (colonists.Count == 0)
                {
                    RimWatchLogger.WarningThrottledByKey(
                        "construction_no_colonists",
                        "ConstructionDiagnostics: No colonists on map - skipping diagnostics");
                    return;
                }
                
                // 1. Проверка наличия незавершённого строительства
                List<Frame> frames = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame)
                    .OfType<Frame>()
                    .ToList();

                if (frames.Count == 0)
                {
                    RimWatchLogger.Debug("ConstructionDiagnostics: No unfinished frames found ✓");
                    return;
                }

                RimWatchLogger.Warning($"⚠️ ConstructionDiagnostics: Found {frames.Count} unfinished frames!");

                // 2. Проверка колонистов способных строить
                List<Pawn> canConstruct = colonists.Where(p => 
                    !p.Dead && 
                    !p.Downed && 
                    !p.InMentalState &&
                    !p.WorkTypeIsDisabled(WorkTypeDefOf.Construction)
                ).ToList();

                if (canConstruct.Count == 0)
                {
                    // This is a colony state issue, not a mod error – throttle it to avoid spam
                    RimWatchLogger.WarningThrottledByKey(
                        "construction_no_builders",
                        "❌ ConstructionDiagnostics: NO colonists can do Construction work!");
                    return;
                }

                RimWatchLogger.Info($"ConstructionDiagnostics: {canConstruct.Count}/{colonists.Count} colonists can construct");

                // 3. Проверка приоритетов Construction
                foreach (Pawn pawn in canConstruct)
                {
                    int constructionPriority = pawn.workSettings.GetPriority(WorkTypeDefOf.Construction);
                    if (constructionPriority == 0)
                    {
                        RimWatchLogger.Warning($"⚠️ {pawn.LabelShort}: Construction disabled (priority=0)");
                    }
                    else if (constructionPriority > 2)
                    {
                        RimWatchLogger.Warning($"⚠️ {pawn.LabelShort}: Construction low priority ({constructionPriority})");
                    }
                    else
                    {
                        RimWatchLogger.Debug($"✓ {pawn.LabelShort}: Construction priority={constructionPriority}");
                    }
                }

                // 4. Проверка 3 случайных frames детально
                int framesToCheck = System.Math.Min(3, frames.Count);
                for (int i = 0; i < framesToCheck; i++)
                {
                    Frame frame = frames[i];
                    DiagnoseSpecificFrame(map, frame, canConstruct);
                }
            }
            catch (System.Exception ex)
            {
                RimWatchLogger.Error("ConstructionDiagnostics: Error in DiagnoseUnfinishedConstruction", ex);
            }
        }

        /// <summary>
        /// Детальная диагностика конкретного frame.
        /// </summary>
        private static void DiagnoseSpecificFrame(Map map, Frame frame, List<Pawn> builders)
        {
            try
            {
                RimWatchLogger.Info($"📦 Frame: {frame.def.defName} at ({frame.Position.x}, {frame.Position.z})");

                // 1. Процент завершённости
                float workDone = frame.workDone;
                float workTotal = frame.def.entityDefToBuild.GetStatValueAbstract(StatDefOf.WorkToBuild);
                float percent = workTotal > 0 ? (workDone / workTotal) * 100f : 0f;
                RimWatchLogger.Info($"  Progress: {percent:F1}% ({workDone:F0}/{workTotal:F0} work)");

                // 2. Материалы - skip (API changed in RimWorld updates)
                // if (frame has incomplete materials) would show here

                // 3. Reachability - могут ли колонисты добраться?
                bool anyCanReach = false;
                foreach (Pawn builder in builders.Take(3)) // Проверим 3 колонистов
                {
                    bool canReach = builder.CanReach(frame, PathEndMode.Touch, Danger.Deadly);
                    if (canReach)
                    {
                        anyCanReach = true;
                        RimWatchLogger.Debug($"  ✓ {builder.LabelShort} can reach");
                    }
                    else
                    {
                        RimWatchLogger.Warning($"  ⚠️ {builder.LabelShort} CANNOT reach (blocked/trapped?)");
                    }
                }

                if (!anyCanReach)
                {
                    RimWatchLogger.Error($"  ❌ NO colonists can reach this frame!");
                }

                // 4. Проверка что frame не forbidden
                if (frame.IsForbidden(Faction.OfPlayer))
                {
                    RimWatchLogger.Error("  ❌ Frame is FORBIDDEN!");
                }

                // 5. Проверка designations
                Designation designation = map.designationManager.DesignationOn(frame);
                if (designation == null)
                {
                    RimWatchLogger.Warning("  ⚠️ No construction designation (might be waiting for materials)");
                }
                else
                {
                    RimWatchLogger.Debug($"  ✓ Has designation: {designation.def.defName}");
                }
            }
            catch (System.Exception ex)
            {
                RimWatchLogger.Error($"ConstructionDiagnostics: Error diagnosing frame {frame.def.defName}", ex);
            }
        }
    }
}

