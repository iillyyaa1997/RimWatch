using RimWatch.Utils;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimWatch.Automation.BuildingPlacement
{
    /// <summary>
    /// Lays power conduits from a source cell to the nearest existing power grid.
    /// Keeps path short and logs actions in debug.
    /// </summary>
    public static class PowerPlanner
    {
        /// <summary>
        /// Connects given cell to the nearest power grid by placing conduits along a simple path.
        /// </summary>
        public static bool ConnectToNearestGrid(Map map, IntVec3 fromCell, int maxRadius, string logLevel = "Moderate")
        {
            try
            {
                if (!fromCell.InBounds(map)) return false;

                // Find nearest grid anchor (conduit or powered building)
                IntVec3? target = FindNearestPowerAnchor(map, fromCell, maxRadius);
                if (target == null)
                {
                    if (logLevel == "Debug")
                        RimWatchLogger.Debug($"PowerPlanner: No power grid found within {maxRadius} cells");
                    return false;
                }

                // Lay conduits along a Manhattan path (first X, then Z)
                ThingDef conduitDef = DefDatabase<ThingDef>.GetNamedSilentFail("PowerConduit");
                if (conduitDef == null)
                {
                    RimWatchLogger.Warning("PowerPlanner: PowerConduit ThingDef not found");
                    return false;
                }

                int steps = 0;
                IntVec3 cur = fromCell;
                while (cur != target.Value && steps < (maxRadius * 2))
                {
                    int dx = target.Value.x - cur.x;
                    int dz = target.Value.z - cur.z;

                    IntVec3 next = cur;
                    if (Math.Abs(dx) >= Math.Abs(dz))
                    {
                        next = new IntVec3(cur.x + Math.Sign(dx), cur.y, cur.z);
                    }
                    else
                    {
                        next = new IntVec3(cur.x, cur.y, cur.z + Math.Sign(dz));
                    }

                    if (!next.InBounds(map))
                        break;

                    // Skip if there is already a conduit
                    Building? b = next.GetFirstBuilding(map);
                    if (b == null || b.def.defName != "PowerConduit")
                    {
                        // Place conduit blueprint (rotation does not matter for 1x1)
                        BuildPlacer.TryPlaceWithBestRotation(map, conduitDef, next, null, logLevel);
                    }

                    cur = next;
                    steps++;
                }

                if (logLevel == "Debug")
                    RimWatchLogger.Debug($"PowerPlanner: Planned conduits in {steps} steps to ({target.Value.x}, {target.Value.z})");

                return true;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("PowerPlanner: Error while connecting to power grid", ex);
                return false;
            }
        }

        private static IntVec3? FindNearestPowerAnchor(Map map, IntVec3 fromCell, int maxRadius)
        {
            IntVec3? best = null;
            float bestDist = float.MaxValue;

            foreach (IntVec3 c in GenRadial.RadialCellsAround(fromCell, maxRadius, true))
            {
                if (!c.InBounds(map)) continue;
                Building? building = c.GetFirstBuilding(map);
                if (building == null) continue;

                bool isAnchor =
                    building.def.defName == "PowerConduit" ||
                    (building.def.comps != null && building.def.comps.Any(comp =>
                        comp.compClass?.Name == "CompPowerTrader" || comp.compClass?.Name == "CompPower"));

                if (!isAnchor) continue;

                float d = fromCell.DistanceTo(c);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = c;
                }
            }

            return best;
        }
    }
}


