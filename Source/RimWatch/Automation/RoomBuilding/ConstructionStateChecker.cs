using RimWatch.Utils;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimWatch.Automation.RoomBuilding
{
    /// <summary>
    /// Утилиты для подсчёта и проверки состояния стен, дверей и построек.
    /// Проверяет ВСЕ состояния: blueprints, frames, и построенные объекты.
    /// </summary>
    public static class ConstructionStateChecker
    {
        /// <summary>
        /// Проверяет есть ли стена в любом состоянии на клетке.
        /// </summary>
        public static bool HasWallInAnyState(Map map, IntVec3 cell)
        {
            // 1. Проверка построенной стены
            Building building = cell.GetFirstBuilding(map);
            if (building != null && IsWall(building.def))
                return true;

            // 2. Проверка blueprint
            List<Thing> things = cell.GetThingList(map);
            foreach (Thing thing in things)
            {
                if (thing is Blueprint bp && IsWall(bp.def.entityDefToBuild as ThingDef))
                    return true;

                // 3. Проверка frame
                if (thing is Frame frame && IsWall(frame.def.entityDefToBuild as ThingDef))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Проверяет есть ли дверь в любом состоянии на клетке.
        /// </summary>
        public static bool HasDoorInAnyState(Map map, IntVec3 cell)
        {
            // 1. Проверка построенной двери
            Building building = cell.GetFirstBuilding(map);
            if (building != null && IsDoor(building.def))
                return true;

            // 2. Проверка blueprint
            List<Thing> things = cell.GetThingList(map);
            foreach (Thing thing in things)
            {
                if (thing is Blueprint bp && IsDoor(bp.def.entityDefToBuild as ThingDef))
                    return true;

                // 3. Проверка frame
                if (thing is Frame frame && IsDoor(frame.def.entityDefToBuild as ThingDef))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Подсчитывает стены во ВСЕХ состояниях для списка клеток.
        /// </summary>
        public static WallStateCount CountWalls(Map map, List<IntVec3> wallCells)
        {
            WallStateCount count = new WallStateCount();

            if (wallCells == null || wallCells.Count == 0)
                return count;

            foreach (IntVec3 cell in wallCells)
            {
                // Проверка построенной стены
                Building building = cell.GetFirstBuilding(map);
                if (building != null && !(building is Blueprint) && !(building is Frame))
                {
                    if (IsWall(building.def))
                    {
                        count.Built++;
                        continue;
                    }
                }

                // Проверка blueprints и frames
                List<Thing> things = cell.GetThingList(map);
                bool foundBlueprint = false;
                bool foundFrame = false;

                foreach (Thing thing in things)
                {
                    if (thing is Blueprint bp && IsWall(bp.def.entityDefToBuild as ThingDef))
                    {
                        count.Blueprints++;
                        foundBlueprint = true;
                        break;
                    }

                    if (thing is Frame frame && IsWall(frame.def.entityDefToBuild as ThingDef))
                    {
                        count.Frames++;
                        foundFrame = true;
                        break;
                    }
                }

                // Если ничего не найдено - клетка пустая
                if (!foundBlueprint && !foundFrame && (building == null || !IsWall(building.def)))
                {
                    count.Empty++;
                }
            }

            return count;
        }

        /// <summary>
        /// Подсчитывает двери во ВСЕХ состояниях для списка клеток.
        /// </summary>
        public static WallStateCount CountDoors(Map map, List<IntVec3> doorCells)
        {
            WallStateCount count = new WallStateCount();

            if (doorCells == null || doorCells.Count == 0)
                return count;

            foreach (IntVec3 cell in doorCells)
            {
                // Проверка построенной двери
                Building building = cell.GetFirstBuilding(map);
                if (building != null && !(building is Blueprint) && !(building is Frame))
                {
                    if (IsDoor(building.def))
                    {
                        count.Built++;
                        continue;
                    }
                }

                // Проверка blueprints и frames
                List<Thing> things = cell.GetThingList(map);
                bool foundBlueprint = false;
                bool foundFrame = false;

                foreach (Thing thing in things)
                {
                    if (thing is Blueprint bp && IsDoor(bp.def.entityDefToBuild as ThingDef))
                    {
                        count.Blueprints++;
                        foundBlueprint = true;
                        break;
                    }

                    if (thing is Frame frame && IsDoor(frame.def.entityDefToBuild as ThingDef))
                    {
                        count.Frames++;
                        foundFrame = true;
                        break;
                    }
                }

                if (!foundBlueprint && !foundFrame && (building == null || !IsDoor(building.def)))
                {
                    count.Empty++;
                }
            }

            return count;
        }

        /// <summary>
        /// Проверяет является ли ThingDef стеной.
        /// </summary>
        private static bool IsWall(ThingDef def)
        {
            if (def == null)
                return false;

            return def.defName.Contains("Wall") || 
                   (def.building != null && def.building.isNaturalRock);
        }

        /// <summary>
        /// Проверяет является ли ThingDef дверью.
        /// </summary>
        private static bool IsDoor(ThingDef def)
        {
            if (def == null)
                return false;

            return def.defName.Contains("Door") || 
                   (def.thingClass != null && def.thingClass.Name.Contains("Door"));
        }

        /// <summary>
        /// Структура для хранения состояния стен/дверей.
        /// </summary>
        public struct WallStateCount
        {
            public int Built;       // Полностью построенные
            public int Frames;      // В процессе строительства (frames)
            public int Blueprints;  // Запланированные (blueprints)
            public int Empty;       // Пустые клетки

            /// <summary>
            /// Всего стен/дверей во всех состояниях (кроме пустых).
            /// </summary>
            public int Total => Built + Frames + Blueprints;

            /// <summary>
            /// Процент завершённости (от 0.0 до 1.0).
            /// </summary>
            public float CompletionPercent
            {
                get
                {
                    int totalCells = Built + Frames + Blueprints + Empty;
                    if (totalCells == 0)
                        return 0f;

                    return (float)Built / totalCells;
                }
            }

            /// <summary>
            /// Есть ли хоть что-то (blueprint, frame или построенное).
            /// </summary>
            public bool HasAny => Total > 0;

            public override string ToString()
            {
                return $"Built={Built}, Frames={Frames}, Blueprints={Blueprints}, Empty={Empty} (Total={Total}, {CompletionPercent:P0})";
            }
        }
    }
}

