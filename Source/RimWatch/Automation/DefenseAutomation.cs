using RimWatch.Core;
using RimWatch.Settings;
using RimWatch.Utils;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace RimWatch.Automation
{
    /// <summary>
    /// ⚔️ Defense Automation - автоматическое управление обороной колонии.
    /// Мониторит угрозы, управляет обороной и военным снаряжением.
    /// </summary>
    public static class DefenseAutomation
    {
        private static int _tickCounter = 0;
        private static bool _isEnabled = false;
        private const int UpdateIntervalCombat = 60; // v0.8.0: Update every 1 second during combat
        private const int UpdateIntervalPeace = 600; // v0.8.2: Update every 10 seconds during peace
        private static bool _lastCheckHadEnemies = false; // Track if last check found enemies
        private static int _lastEnemyCount = -1;
        private static bool _lastRaidInProgress = false;
        private static bool _lastNoTurretsWarning = false;
        private static bool _lastLowArmedWarning = false;
        
        // v0.8.0: Cooldowns to prevent spam
        private static int _lastRetreatWarningTick = -9999;
        private const int RetreatWarningCooldown = 60; // Only warn once per second
        
        // v0.8.4++: Cooldown для экипировки брони - НЕ СПАМИТЬ!
        private static int _lastEquipArmorTick = -9999;
        private const int EquipArmorCooldown = 1800; // 30 секунд между попытками экипировки

        /// <summary>
        /// Helper: current log level for DefenseAutomation.
        /// </summary>
        private static SystemLogLevel DefenseLogLevel
        {
            get
            {
                return RimWatchMod.Settings?.defenseLogLevel ?? SystemLogLevel.Moderate;
            }
        }

        /// <summary>
        /// Включена ли автоматизация обороны.
        /// </summary>
        public static bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                RimWatchLogger.Info($"DefenseAutomation: {(value ? "Enabled" : "Disabled")}");
            }
        }

        /// <summary>
        /// Вызывается каждый тик для обновления автоматизации.
        /// v0.8.2: Uses adaptive interval - faster during combat, slower during peace.
        /// </summary>
        public static void Tick()
        {
            if (!IsEnabled) return;
            if (!RimWatchCore.AutopilotEnabled) return;

            // v0.8.2: Adaptive interval based on threat level
            int currentInterval = _lastCheckHadEnemies ? UpdateIntervalCombat : UpdateIntervalPeace;
            
            _tickCounter++;
            if (_tickCounter >= currentInterval)
            {
                _tickCounter = 0;
                ManageDefense();
            }
        }

        /// <summary>
        /// Manages defense operations.
        /// </summary>
        private static void ManageDefense()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;
            
            // v0.8.3: Log execution start (only in Verbose/Debug)
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            if (DefenseLogLevel >= SystemLogLevel.Verbose)
            {
                RimWatchLogger.LogExecutionStart("DefenseAutomation", "ManageDefense", new Dictionary<string, object>
                {
                    { "map", map.uniqueID },
                    { "adaptiveMode", _lastCheckHadEnemies ? "Combat" : "Peace" }
                });
            }

            // Analyze threats
            DefenseStatus status = AnalyzeDefenseStatus(map);
            
            // v0.8.3: Log defense status analysis (always to JSON, Debug to RimWorld log only in Verbose/Debug)
            RimWatchLogger.LogDecision("DefenseAutomation", "DefenseStatusAnalysis", new Dictionary<string, object>
            {
                { "enemyCount", status.EnemyCount },
                { "raidInProgress", status.RaidInProgress },
                { "colonistCount", status.ColonistCount },
                { "armedColonists", status.ArmedColonists },
                { "turretCount", status.TurretCount }
            });
            
            // v0.8.2: Track enemy presence for adaptive interval
            _lastCheckHadEnemies = status.EnemyCount > 0;

            // Short-circuit if colony is dead – no sense in running heavy defense logic or spamming logs
            if (status.ColonistCount == 0)
            {
                if (DefenseLogLevel != SystemLogLevel.Off)
                {
                    RimWatchLogger.WarningThrottledByKey(
                        "defense_no_colonists",
                        "[DefenseAutomation] No colonists on map - skipping defense automation");
                }
            
                // v0.8.3: Log execution end and return early
                stopwatch.Stop();
                if (DefenseLogLevel >= SystemLogLevel.Verbose)
                {
                    RimWatchLogger.LogExecutionEnd("DefenseAutomation", "ManageDefense", true, stopwatch.ElapsedMilliseconds,
                        "No colonists on map");
                }
                return;
            }

            // v0.8.5: Report critical threats with improved state-based logging
            if (status.EnemyCount > 0 && DefenseLogLevel != SystemLogLevel.Off)
            {
                // Log enemy count changes
                if (status.EnemyCount != _lastEnemyCount && DefenseLogLevel >= SystemLogLevel.Minimal)
                {
                    RimWatchLogger.LogDecision("DefenseAutomation", "EnemyDetected", new Dictionary<string, object>
                    {
                        { "enemyCount", status.EnemyCount },
                        { "previousCount", _lastEnemyCount },
                        { "armedColonists", status.ArmedColonists }
                    });
                    RimWatchLogger.Info($"[DefenseAutomation] ⚠️ ENEMIES DETECTED: {status.EnemyCount} hostiles on map!");
                }
                
                // Log raid state changes
                if (status.RaidInProgress && !_lastRaidInProgress && DefenseLogLevel >= SystemLogLevel.Minimal)
                {
                    RimWatchLogger.LogStateChange("DefenseAutomation", "Peace", "Raid", "Raid in progress");
                    RimWatchLogger.Info("DefenseAutomation: 🚨 RAID IN PROGRESS!");
                }
            }
            else if (_lastEnemyCount > 0 && DefenseLogLevel >= SystemLogLevel.Minimal)
            {
                // Enemies cleared
                RimWatchLogger.LogStateChange("DefenseAutomation", "Combat", "Peace", "All enemies cleared");
                RimWatchLogger.Info("[DefenseAutomation] ✅ No enemies remaining on map");
            }

            // Check defenses (log only when state changes)
            bool noTurretsNow = status.TurretCount == 0 && status.ColonistCount > 3;
            if (noTurretsNow && !_lastNoTurretsWarning && DefenseLogLevel >= SystemLogLevel.Minimal)
            {
                RimWatchLogger.Info("DefenseAutomation: ℹ️ No turrets detected - consider building defenses");
            }
            _lastNoTurretsWarning = noTurretsNow;

            // Check weapons (log only when transitioning into low-armed state)
            bool lowArmedNow = status.ColonistCount > 0 && status.ArmedColonists < status.ColonistCount / 2;
            if (lowArmedNow && !_lastLowArmedWarning && DefenseLogLevel >= SystemLogLevel.Minimal)
            {
                RimWatchLogger.Info($"DefenseAutomation: ⚠️ Only {status.ArmedColonists}/{status.ColonistCount} colonists armed");
            }
            _lastLowArmedWarning = lowArmedNow;

            // All clear
            if (status.EnemyCount == 0 && _tickCounter == 0 && DefenseLogLevel >= SystemLogLevel.Verbose)
            {
                RimWatchLogger.Debug($"DefenseAutomation: Area secure - {status.TurretCount} turrets, {status.ArmedColonists} armed colonists ✓");
            }

            // **NEW: Execute defense actions**
            AutoDraftColonists(map, status);
            AutoEquipWeapons(map, status);
            AutoEquipArmor(map, status); // ✅ NEW: Auto-equip armor
            AutoAttackEnemies(map, status); // ✅ NEW: Active enemy engagement
            AutoPositionDefenders(map, status);
            
            // **v0.7 ADVANCED: Tactical defense and turret management**
            AutoFormDefensiveLines(map, status);
            AutoTacticalRetreat(map, status);
            AutoRepairTurrets(map);
            
            // v0.8.3: Log execution end
            stopwatch.Stop();
            if (DefenseLogLevel >= SystemLogLevel.Verbose)
            {
                RimWatchLogger.LogExecutionEnd("DefenseAutomation", "ManageDefense", true, stopwatch.ElapsedMilliseconds,
                    $"Enemies={status.EnemyCount}, Armed={status.ArmedColonists}/{status.ColonistCount}");
            }

            // Update last-known threat state for future state-based logging
            _lastEnemyCount = status.EnemyCount;
            _lastRaidInProgress = status.RaidInProgress;
        }

        /// <summary>
        /// Analyzes current defense status.
        /// v0.8.0: CRITICAL FIX - Count ALL combat-capable colonists, not just armed ones
        /// </summary>
        private static DefenseStatus AnalyzeDefenseStatus(Map map)
        {
            DefenseStatus status = new DefenseStatus();

            // Count ALL colonists
            status.ColonistCount = map.mapPawns.FreeColonistsSpawnedCount;

            // Count enemies
            status.EnemyCount = map.mapPawns.AllPawns
                .Count(p => p.HostileTo(Faction.OfPlayer) && !p.Dead && !p.Downed);

            // Check for active raid
            status.RaidInProgress = map.lordManager.lords
                .Any(l => l.faction != null && l.faction.HostileTo(Faction.OfPlayer));

            // Count turrets
            status.TurretCount = map.listerBuildings.allBuildingsColonist
                .Count(b => b.def.building?.IsTurret == true);

            // v0.8.0: Count COMBAT-CAPABLE colonists (not just armed - includes melee, drafted, etc)
            status.ArmedColonists = map.mapPawns.FreeColonistsSpawned
                .Count(p => !p.Downed && !p.Dead && 
                           p.health.capacities.CapableOf(PawnCapacityDefOf.Moving) &&
                           (p.equipment?.Primary != null || // Has weapon
                            p.drafter?.Drafted == true || // Or is drafted (can fight unarmed if needed)
                            IsCombatCapable(p))); // Or has combat skills

            return status;
        }

        /// <summary>
        /// Structure for defense status.
        /// </summary>
        private class DefenseStatus
        {
            public int ColonistCount { get; set; } = 0;
            public int EnemyCount { get; set; } = 0;
            public bool RaidInProgress { get; set; } = false;
            public int TurretCount { get; set; } = 0;
            public int ArmedColonists { get; set; } = 0;
        }

        // ==================== ACTION METHODS ====================

        /// <summary>
        /// Automatically drafts colonists when enemies are detected.
        /// </summary>
        private static void AutoDraftColonists(Map map, DefenseStatus status)
        {
            // v0.8.3: Log execution start
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            // Check if enemies are close enough to be a real threat
            const float DangerDistance = 30f; // Only draft if enemies within 30 tiles of any colonist
            
            List<Pawn> enemies = map.mapPawns.AllPawnsSpawned
                .Where(p => p.HostileTo(Faction.OfPlayer) && !p.Dead && !p.Downed)
                .ToList();
            
            bool hasCloseEnemies = false;
            float closestDistance = float.MaxValue;
            
            if (enemies.Count > 0)
            {
                List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned.ToList();
                foreach (Pawn enemy in enemies)
                {
                    foreach (Pawn colonist in colonists)
                    {
                        float dist = enemy.Position.DistanceTo(colonist.Position);
                        if (dist < closestDistance)
                        {
                            closestDistance = dist;
                        }
                        if (dist <= DangerDistance)
                        {
                            hasCloseEnemies = true;
                            break;
                        }
                    }
                    if (hasCloseEnemies) break;
                }
            }
            
            // Only draft if enemies are present AND close
            if (!hasCloseEnemies || status.EnemyCount == 0)
            {
                // Undraft colonists if no close enemies and they're drafted
                List<Pawn> draftedColonists = map.mapPawns.FreeColonistsSpawned
                    .Where(p => p.drafter != null && p.drafter.Drafted)
                    .ToList();

                if (draftedColonists.Count > 0)
                {
                    // v0.8.3: Log undraft decision
                    string reasonStr = status.EnemyCount == 0 ? "ThreatCleared" : "EnemyTooFar";
                    RimWatchLogger.LogDecision("DefenseAutomation", "UndraftColonists", new Dictionary<string, object>
                    {
                        { "count", draftedColonists.Count },
                        { "reason", reasonStr },
                        { "closestDistance", closestDistance },
                        { "dangerDistance", DangerDistance }
                    });
                    
                    List<string> undraftedNames = new List<string>();
                    foreach (Pawn colonist in draftedColonists)
                    {
                        colonist.drafter.Drafted = false;
                        undraftedNames.Add(colonist.LabelShort);
                    }
                    
                    string reason = status.EnemyCount == 0 ? "threat cleared" : $"enemies too far ({closestDistance:F0} tiles)";
                    
                    // v0.8.3: Log execution end
                    stopwatch.Stop();
                    RimWatchLogger.LogExecutionEnd("DefenseAutomation", "AutoDraftColonists", true, stopwatch.ElapsedMilliseconds,
                        $"Undrafted {draftedColonists.Count} colonists");
                    
                    RimWatchLogger.Info($"✅ DefenseAutomation: Undrafted {draftedColonists.Count} colonists ({reason})");
                    RimWatchLogger.Info($"   Released: {string.Join(", ", undraftedNames)}");
                }
                return;
            }

            // Find combat-capable colonists who are not drafted
            List<Pawn> colonistsToDraft = map.mapPawns.FreeColonistsSpawned
                .Where(p => p.drafter != null &&
                           !p.drafter.Drafted &&
                           !p.Downed && !p.Dead &&
                           p.health.capacities.CapableOf(PawnCapacityDefOf.Moving) &&
                           p.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) &&
                           !p.InMentalState && // Don't draft mentally broken colonists
                           IsCombatCapable(p)) // ✅ NEW: Only draft combat-capable colonists
                .OrderByDescending(p => GetCombatScore(p)) // ✅ NEW: Best fighters first
                .ToList();

            if (colonistsToDraft.Count == 0)
            {
                stopwatch.Stop();
                RimWatchLogger.LogExecutionEnd("DefenseAutomation", "AutoDraftColonists", true, stopwatch.ElapsedMilliseconds,
                    "No combat-capable colonists available");
                return; // No one to draft
            }

            // Draft colonists based on threat level
            int toDraft = Math.Min(colonistsToDraft.Count, Math.Max(2, status.EnemyCount)); // At least 2, up to enemy count

            int drafted = 0;
            List<string> draftedNames = new List<string>();
            
            for (int i = 0; i < toDraft; i++)
            {
                Pawn colonist = colonistsToDraft[i];
                
                // v0.8.3: Log draft decision for each colonist
                int shootingSkill = colonist.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0;
                string weaponName = colonist.equipment?.Primary?.LabelShort ?? "unarmed";
                float combatScore = GetCombatScore(colonist);
                
                RimWatchLogger.LogDecision("DefenseAutomation", "DraftColonist", new Dictionary<string, object>
                {
                    { "colonist", colonist.LabelShort },
                    { "combatScore", combatScore },
                    { "shootingSkill", shootingSkill },
                    { "weapon", colonist.equipment?.Primary?.def.defName ?? "none" },
                    { "closestEnemy", closestDistance },
                    { "threatLevel", status.EnemyCount }
                });
                
                colonist.drafter.Drafted = true;
                drafted++;
                
                draftedNames.Add($"{colonist.LabelShort} (Shooting: {shootingSkill}, {weaponName})");
            }

            if (drafted > 0)
            {
                // v0.8.3: Log execution end with results
                stopwatch.Stop();
                RimWatchLogger.LogExecutionEnd("DefenseAutomation", "AutoDraftColonists", true, stopwatch.ElapsedMilliseconds,
                    $"Drafted {drafted}/{colonistsToDraft.Count} colonists");
                
                RimWatchLogger.Info($"⚔️ DefenseAutomation: Drafted {drafted} colonists (enemies: {status.EnemyCount}, closest: {closestDistance:F0} tiles)");
                foreach (string info in draftedNames)
                {
                    RimWatchLogger.Info($"   🪖 {info}");
                }
            }
        }

        /// <summary>
        /// Automatically equips colonists with the best available weapons.
        /// </summary>
        private static void AutoEquipWeapons(Map map, DefenseStatus status)
        {
            // Only worry about weapons if enemies are nearby or we're low on armed colonists
            if (status.EnemyCount == 0 && status.ArmedColonists >= status.ColonistCount / 2)
            {
                return; // We're fine
            }

            // Find unarmed colonists
            // ✅ CRITICAL FIX: Check IsCombatCapable to prevent incapable of violence from getting weapons!
            List<Pawn> unarmedColonists = map.mapPawns.FreeColonistsSpawned
                .Where(p => p.equipment != null &&
                           p.equipment.Primary == null &&
                           !p.Downed && !p.Dead &&
                           p.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) &&
                           IsCombatCapable(p)) // ✅ NEW: Only equip combat-capable colonists
                .ToList();

            if (unarmedColonists.Count == 0)
            {
                return; // Everyone is armed
            }
            
            RimWatchLogger.Debug($"DefenseAutomation: Found {unarmedColonists.Count} unarmed colonists, searching for weapons...");

            // Find available weapons (on ground or in storage)
            // Check that weapon is not currently equipped by anyone
            List<Pawn> allColonists = map.mapPawns.FreeColonistsSpawned.ToList();
            List<Thing> equippedWeapons = allColonists
                .Where(p => p.equipment?.Primary != null)
                .Select(p => (Thing)p.equipment.Primary)
                .ToList();
            
            List<ThingWithComps> availableWeapons = map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon)
                .OfType<ThingWithComps>()
                .Where(w => w.def.IsWeapon &&
                           !w.def.IsStuff && // ✅ CRITICAL: Exclude materials (wood, steel, etc.)
                           (w.def.IsMeleeWeapon || w.def.IsRangedWeapon) && // ✅ Only real weapons
                           !w.IsForbidden(Faction.OfPlayer) &&
                           w.Spawned &&
                           !equippedWeapons.Contains(w)) // Not already equipped
                .OrderByDescending(w => w.GetStatValue(StatDefOf.RangedWeapon_DamageMultiplier))
                .ToList();

            if (availableWeapons.Count == 0)
            {
                // Log detailed info about why no weapons were found
                int totalWeapons = map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon).Count;
                int forbiddenWeapons = map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon)
                    .Count(w => w.IsForbidden(Faction.OfPlayer));
                    
                RimWatchLogger.Debug($"DefenseAutomation: No weapons available to equip (total: {totalWeapons}, forbidden: {forbiddenWeapons})");
                return; // No weapons available
            }
            
            RimWatchLogger.Debug($"DefenseAutomation: Found {availableWeapons.Count} available weapons: {string.Join(", ", availableWeapons.Take(5).Select(w => w.LabelShort))}");

            int equipped = 0;
            List<string> equipmentChanges = new List<string>();

            foreach (Pawn colonist in unarmedColonists)
            {
                if (availableWeapons.Count == 0) break;

                // Pick the best weapon for this colonist
                ThingWithComps weapon = availableWeapons[0];
                availableWeapons.RemoveAt(0);

                // Create a job to equip the weapon
                Job equipJob = JobMaker.MakeJob(JobDefOf.Equip, weapon);
                colonist.jobs.TryTakeOrderedJob(equipJob, JobTag.Misc);
                
                equipped++;
                
                // Get weapon stats
                float damage = weapon.GetStatValue(StatDefOf.RangedWeapon_DamageMultiplier);
                int shootingSkill = colonist.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0;
                equipmentChanges.Add($"{colonist.LabelShort} → {weapon.LabelShort} (DMG: {damage:F1}x, Shooting: {shootingSkill})");

                if (equipped >= 3) break; // Don't equip too many at once (performance)
            }

            if (equipped > 0)
            {
                RimWatchLogger.Info($"🗡️ DefenseAutomation: Equipped {equipped} colonists with weapons:");
                foreach (string change in equipmentChanges)
                {
                    RimWatchLogger.Info($"   • {change}");
                }
            }
        }

        /// <summary>
        /// Automatically equips colonists with the best available armor.
        /// Prioritizes combat-capable colonists and those going into danger.
        /// v0.8.4++: Добавлен cooldown - НЕ СПАМИТЬ экипировку каждую секунду!
        /// </summary>
        private static void AutoEquipArmor(Map map, DefenseStatus status)
        {
            try
            {
                // v0.8.4++: КРИТИЧНО - cooldown для экипировки!
                // НЕ проверять каждую секунду, это создаёт спам!
                int currentTick = Find.TickManager.TicksGame;
                if (currentTick - _lastEquipArmorTick < EquipArmorCooldown)
                {
                    return; // Слишком рано, ждём cooldown
                }
                
                _lastEquipArmorTick = currentTick;
                
                // Find colonists without armor or with poor armor
                List<Pawn> colonistsNeedingArmor = map.mapPawns.FreeColonistsSpawned
                    .Where(p => !p.Downed && !p.Dead &&
                               p.apparel != null &&
                               IsCombatCapable(p)) // Only equip combat-capable colonists
                    .OrderByDescending(p => GetCombatScore(p)) // Prioritize best fighters
                    .ToList();

                if (colonistsNeedingArmor.Count == 0)
                    return;

                // Find available armor on map
                List<Apparel> availableArmor = map.listerThings.ThingsInGroup(ThingRequestGroup.Apparel)
                    .OfType<Apparel>()
                    .Where(a => a.Spawned &&
                               IsArmorPiece(a) &&
                               !IsWornByAnyone(map, a))
                    .OrderByDescending(a => GetArmorRating(a))
                    .ToList();

                // ✅ CRITICAL: Unforbid armor BEFORE trying to equip
                foreach (Apparel armor in availableArmor)
                {
                    if (armor.IsForbidden(Faction.OfPlayer))
                    {
                        armor.SetForbidden(false, warnOnFail: false);
                    }
                }

                if (availableArmor.Count == 0)
                {
                    RimWatchLogger.Debug("DefenseAutomation: No armor available to equip");
                    return;
                }

                int equipped = 0;
                List<string> equipmentChanges = new List<string>();

                // IMPROVED: Equip ALL armor layers, not just first one
                foreach (Pawn colonist in colonistsNeedingArmor.Take(3)) // Max 3 colonists per cycle
                {
                    int colonistArmorCount = EquipAllArmorLayersForColonist(colonist, availableArmor, equipmentChanges);
                    equipped += colonistArmorCount;
                }

                if (equipped > 0)
                {
                    RimWatchLogger.Info($"🛡️ DefenseAutomation: Equipped {equipped} colonists with armor:");
                    foreach (string change in equipmentChanges)
                    {
                        RimWatchLogger.Info($"   • {change}");
                    }
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("DefenseAutomation: Error in AutoEquipArmor", ex);
            }
        }

        /// <summary>
        /// Checks if apparel is armor (provides protection).
        /// </summary>
        private static bool IsArmorPiece(Apparel apparel)
        {
            try
            {
                if (apparel.def.apparel == null)
                    return false;

                // Check if provides sharp or blunt armor
                StatDef sharpArmor = StatDefOf.ArmorRating_Sharp;
                StatDef bluntArmor = StatDefOf.ArmorRating_Blunt;

                float sharpRating = apparel.GetStatValue(sharpArmor);
                float bluntRating = apparel.GetStatValue(bluntArmor);

                return (sharpRating > 0.05f || bluntRating > 0.05f);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets overall armor rating for comparison.
        /// </summary>
        private static float GetArmorRating(Apparel apparel)
        {
            try
            {
                float sharp = apparel.GetStatValue(StatDefOf.ArmorRating_Sharp);
                float blunt = apparel.GetStatValue(StatDefOf.ArmorRating_Blunt);

                // Average of sharp and blunt
                return (sharp + blunt) / 2f;
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// Checks if armor is currently worn by any colonist.
        /// </summary>
        private static bool IsWornByAnyone(Map map, Apparel armor)
        {
            try
            {
                foreach (Pawn colonist in map.mapPawns.FreeColonistsSpawned)
                {
                    if (colonist.apparel?.WornApparel?.Contains(armor) == true)
                        return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Equips all available armor layers for a colonist (head, torso, legs, etc).
        /// </summary>
        private static int EquipAllArmorLayersForColonist(Pawn colonist, List<Apparel> availableArmor, List<string> equipmentChanges)
        {
            try
            {
                int equipped = 0;

                // Get ALL apparel layers from game and mods
                var allLayers = DefDatabase<ApparelLayerDef>.AllDefsListForReading;

                foreach (var layer in allLayers)
                {
                    // Find best armor for this specific layer
                    var bestArmorForLayer = availableArmor
                        .Where(a => a.def.apparel != null && 
                                   a.def.apparel.layers != null && 
                                   a.def.apparel.layers.Contains(layer))
                        .OrderByDescending(a => GetArmorRating(a))
                        .FirstOrDefault();

                    if (bestArmorForLayer == null) continue;

                    // v0.8.4++: КРИТИЧНО - проверить что УЖЕ НАДЕВАЕТ эту вещь!
                    // НЕ ПРЕРЫВАТЬ если уже надевает!
                    if (colonist.CurJob != null && colonist.CurJob.def == JobDefOf.Wear)
                    {
                        // Проверяем что надевает именно ЭТУ вещь
                        if (colonist.CurJob.targetA.Thing == bestArmorForLayer)
                        {
                            // Уже надевает эту броню - пропускаем!
                            continue;
                        }
                    }

                    // Check if already wearing better armor in this layer
                    Apparel? currentArmor = colonist.apparel?.WornApparel
                        ?.FirstOrDefault(a => a.def.apparel.layers.Contains(layer));

                    if (currentArmor != null && GetArmorRating(currentArmor) >= GetArmorRating(bestArmorForLayer))
                    {
                        continue; // Already has better armor in this layer
                    }
                    
                    // v0.8.4++: Если УЖЕ надел эту броню - не надевать снова!
                    if (currentArmor != null && currentArmor == bestArmorForLayer)
                    {
                        continue; // Already wearing this exact armor
                    }

                    // Check if colonist can wear this without dropping other items
                    if (colonist.apparel.CanWearWithoutDroppingAnything(bestArmorForLayer.def))
                    {
                        // Create job to wear armor
                        Job wearJob = JobMaker.MakeJob(JobDefOf.Wear, bestArmorForLayer);
                        colonist.jobs.TryTakeOrderedJob(wearJob, JobTag.Misc);

                        availableArmor.Remove(bestArmorForLayer);
                        equipped++;

                        float armorRating = GetArmorRating(bestArmorForLayer);
                        equipmentChanges.Add($"{colonist.LabelShort} → {bestArmorForLayer.LabelShort} [{layer.defName}] (Armor: {armorRating:F1})");
                    }
                }

                return equipped;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error($"DefenseAutomation: Error equipping armor layers for {colonist.LabelShort}", ex);
                return 0;
            }
        }

        /// <summary>
        /// Finds best available armor piece for a colonist.
        /// </summary>
        private static Apparel? FindBestArmorFor(Pawn colonist, List<Apparel> availableArmor)
        {
            try
            {
                // Get layers colonist doesn't have or has weak coverage
                foreach (Apparel armor in availableArmor)
                {
                    if (colonist.apparel.CanWearWithoutDroppingAnything(armor.def))
                    {
                        return armor;
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Actively commands drafted colonists to attack enemies.
        /// Takes into account weapon range, retreat logic, and smart tactics.
        /// </summary>
        private static void AutoAttackEnemies(Map map, DefenseStatus status)
        {
            try
            {
                if (status.EnemyCount == 0) return;

                // Get all drafted, combat-ready colonists
                List<Pawn> fighters = map.mapPawns.FreeColonistsSpawned
                    .Where(p => p.drafter != null &&
                               p.drafter.Drafted &&
                               !p.Downed && !p.Dead &&
                               IsCombatCapable(p))
                    .ToList();

                if (fighters.Count == 0) return;

                // Get all hostile enemies
                List<Pawn> enemies = map.mapPawns.AllPawnsSpawned
                    .Where(p => p.HostileTo(Faction.OfPlayer) && !p.Dead && !p.Downed)
                    .ToList();

                if (enemies.Count == 0) return;

                int commandsIssued = 0;

                foreach (Pawn fighter in fighters)
                {
                    // Skip if already in combat or has a combat job
                    if (fighter.CurJob != null && 
                        (fighter.CurJob.def == JobDefOf.AttackMelee ||
                         fighter.CurJob.def == JobDefOf.AttackStatic ||
                         fighter.CurJob.def == JobDefOf.Wait_Combat))
                    {
                        continue; // Already fighting
                    }

                    // Get weapon info
                    ThingWithComps weapon = fighter.equipment?.Primary;
                    bool isMelee = weapon == null || weapon.def.IsMeleeWeapon;
                    bool isRanged = weapon != null && weapon.def.IsRangedWeapon;
                    float weaponRange = isRanged ? weapon.def.Verbs[0].range : 2f; // Melee = 2 tiles

                    // Find closest enemy
                    Pawn closestEnemy = enemies.OrderBy(e => e.Position.DistanceTo(fighter.Position)).FirstOrDefault();
                    if (closestEnemy == null) continue;

                    float distanceToEnemy = fighter.Position.DistanceTo(closestEnemy.Position);

                    // ✅ SMART TACTICS: Different behavior for melee vs ranged
                    if (isMelee)
                    {
                        // ✅ MELEE: Charge forward like a tank!
                        if (distanceToEnemy > 2f)
                        {
                            Job attackJob = JobMaker.MakeJob(JobDefOf.AttackMelee, closestEnemy);
                            fighter.jobs.TryTakeOrderedJob(attackJob, JobTag.Misc);
                            commandsIssued++;
                            RimWatchLogger.Debug($"⚔️ DefenseAutomation: {fighter.LabelShort} charging enemy (melee)");
                        }
                    }
                    else if (isRanged)
                    {
                        // ✅ RANGED: Maintain optimal distance
                        float optimalRange = Math.Min(weaponRange * 0.8f, 25f); // 80% of max range, max 25 tiles
                        float minSafeRange = 5f; // Don't let enemies get too close

                        if (distanceToEnemy < minSafeRange)
                        {
                            // ✅ TOO CLOSE: Retreat while shooting (kite backwards)
                            IntVec3 retreatPos = FindRetreatPosition(map, fighter, closestEnemy, optimalRange);
                            if (retreatPos.IsValid)
                            {
                                Job retreatJob = JobMaker.MakeJob(JobDefOf.Goto, retreatPos);
                                fighter.jobs.TryTakeOrderedJob(retreatJob, JobTag.Misc);
                                commandsIssued++;
                                RimWatchLogger.Debug($"🏃 DefenseAutomation: {fighter.LabelShort} retreating (enemy too close: {distanceToEnemy:F1} tiles)");
                            }
                        }
                        else if (distanceToEnemy > weaponRange)
                        {
                            // ✅ TOO FAR: Move closer to attack range
                            Job attackJob = JobMaker.MakeJob(JobDefOf.AttackStatic, closestEnemy);
                            fighter.jobs.TryTakeOrderedJob(attackJob, JobTag.Misc);
                            commandsIssued++;
                            RimWatchLogger.Debug($"🎯 DefenseAutomation: {fighter.LabelShort} engaging enemy (range: {weaponRange:F0} tiles)");
                        }
                        else
                        {
                            // ✅ GOOD RANGE: Attack from current position
                            Job attackJob = JobMaker.MakeJob(JobDefOf.AttackStatic, closestEnemy);
                            fighter.jobs.TryTakeOrderedJob(attackJob, JobTag.Misc);
                            commandsIssued++;
                            RimWatchLogger.Debug($"🎯 DefenseAutomation: {fighter.LabelShort} attacking from optimal range ({distanceToEnemy:F1} tiles)");
                        }
                    }
                }

                if (commandsIssued > 0)
                {
                    RimWatchLogger.Info($"⚔️ DefenseAutomation: Issued {commandsIssued} combat commands");
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("DefenseAutomation: Error in AutoAttackEnemies", ex);
            }
        }

        /// <summary>
        /// Finds a safe retreat position away from the enemy.
        /// </summary>
        private static IntVec3 FindRetreatPosition(Map map, Pawn fighter, Pawn enemy, float targetDistance)
        {
            try
            {
                // Calculate direction AWAY from enemy
                IntVec3 directionAway = fighter.Position - enemy.Position;
                if (directionAway == IntVec3.Zero)
                {
                    // Fallback: move in any direction
                    directionAway = new IntVec3(1, 0, 1);
                }

                // Try to find a valid cell in the retreat direction
                for (int dist = 5; dist <= 15; dist += 2)
                {
                    IntVec3 retreatPos = fighter.Position + (directionAway.ToVector3() * dist).ToIntVec3();
                    
                    if (retreatPos.InBounds(map) &&
                        retreatPos.Standable(map) &&
                        retreatPos.GetFirstBuilding(map) == null &&
                        fighter.CanReach(retreatPos, PathEndMode.OnCell, Danger.Deadly))
                    {
                        return retreatPos;
                    }
                }

                // Fallback: current position
                return fighter.Position;
            }
            catch
            {
                return fighter.Position;
            }
        }

        /// <summary>
        /// Automatically positions drafted defenders to intercept enemies.
        /// Simple strategy: Position defenders between enemies and colony center.
        /// </summary>
        private static void AutoPositionDefenders(Map map, DefenseStatus status)
        {
            // Only position if there are close enemies and drafted colonists
            if (status.EnemyCount == 0) return;
            
            // Get drafted colonists
            List<Pawn> draftedColonists = map.mapPawns.FreeColonistsSpawned
                .Where(p => p.drafter != null && p.drafter.Drafted && !p.Downed && !p.Dead)
                .ToList();
            
            if (draftedColonists.Count == 0) return;
            
            // Get enemies
            List<Pawn> enemies = map.mapPawns.AllPawnsSpawned
                .Where(p => p.HostileTo(Faction.OfPlayer) && !p.Dead && !p.Downed)
                .ToList();
            
            if (enemies.Count == 0) return;
            
            // Find colony center (average position of player buildings or colonists)
            IntVec3 colonyCenter = FindColonyCenter(map);
            
            // Find enemy center (average enemy position)
            IntVec3 enemyCenter = new IntVec3(
                (int)enemies.Average(e => e.Position.x),
                0,
                (int)enemies.Average(e => e.Position.z)
            );
            
            // Calculate defensive line: midpoint between colony and enemies, closer to colony
            IntVec3 defensiveLine = new IntVec3(
                (colonyCenter.x * 3 + enemyCenter.x) / 4, // 75% toward colony, 25% toward enemies
                0,
                (colonyCenter.z * 3 + enemyCenter.z) / 4
            );
            
            // Only reposition if defenders are too close to enemies or too far from defensive line
            int repositioned = 0;
            List<string> movements = new List<string>();
            
            foreach (Pawn defender in draftedColonists)
            {
                float distToEnemies = enemies.Min(e => e.Position.DistanceTo(defender.Position));
                float distToDefensiveLine = defender.Position.DistanceTo(defensiveLine);
                
                // Reposition if:
                // 1. Too close to enemies (<10 tiles)
                // 2. Too far from defensive line (>15 tiles)
                bool shouldReposition = (distToEnemies < 10f || distToDefensiveLine > 15f);
                
                if (shouldReposition)
                {
                    // Find position near defensive line
                    IntVec3 targetPos = FindDefensivePosition(map, defensiveLine, defender, enemies);
                    
                    if (targetPos != IntVec3.Invalid && targetPos != defender.Position)
                    {
                        // Order defender to move to position
                        Job gotoJob = JobMaker.MakeJob(JobDefOf.Goto, targetPos);
                        defender.jobs.TryTakeOrderedJob(gotoJob, JobTag.Misc);
                        
                        repositioned++;
                        movements.Add($"{defender.LabelShort} → ({targetPos.x}, {targetPos.z}) [dist from enemies: {distToEnemies:F0}]");
                    }
                }
            }
            
            // Log positioning actions
            if (repositioned > 0)
            {
                RimWatchLogger.Info($"🛡️ DefenseAutomation: Repositioned {repositioned} defender(s):");
                foreach (string move in movements.Take(5)) // Limit to 5 logs
                {
                    RimWatchLogger.Info($"   • {move}");
                }
            }
        }

        /// <summary>
        /// Finds the colony center (average position of buildings or colonists).
        /// </summary>
        private static IntVec3 FindColonyCenter(Map map)
        {
            // Try to find center based on player buildings
            List<Building> buildings = map.listerBuildings.allBuildingsColonist;
            
            if (buildings.Count > 0)
            {
                return new IntVec3(
                    (int)buildings.Average(b => b.Position.x),
                    0,
                    (int)buildings.Average(b => b.Position.z)
                );
            }
            
            // Fallback: use colonist positions
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned.ToList();
            if (colonists.Count > 0)
            {
                return new IntVec3(
                    (int)colonists.Average(c => c.Position.x),
                    0,
                    (int)colonists.Average(c => c.Position.z)
                );
            }
            
            // Final fallback: map center
            return map.Center;
        }

        /// <summary>
        /// Finds a good defensive position near the defensive line.
        /// </summary>
        private static IntVec3 FindDefensivePosition(Map map, IntVec3 defensiveLine, Pawn defender, List<Pawn> enemies)
        {
            // Search for standable position near defensive line
            for (int radius = 0; radius < 10; radius++)
            {
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(defensiveLine, radius, true))
                {
                    if (!cell.InBounds(map)) continue;
                    if (!cell.Standable(map)) continue;
                    if (cell.GetFirstPawn(map) != null) continue; // Don't stack colonists
                    
                    // Check if position has good line of sight to enemies
                    bool hasLineOfSight = enemies.Any(e => GenSight.LineOfSight(cell, e.Position, map));
                    
                    // Prefer positions with cover
                    Thing cover = cell.GetCover(map);
                    bool hasCover = cover != null;
                    
                    // Check distance to nearest enemy
                    float distToNearestEnemy = enemies.Min(e => cell.DistanceTo(e.Position));
                    
                    // Good defensive position:
                    // - Line of sight to enemies
                    // - Not too close (>15 tiles)
                    // - Preferably has cover
                    if (hasLineOfSight && distToNearestEnemy > 15f)
                    {
                        return cell;
                    }
                    
                    // Acceptable position without cover if needed
                    if (hasLineOfSight && distToNearestEnemy > 10f && radius > 5)
                    {
                        return cell;
                    }
                }
            }
            
            // If no good position found, return defensive line itself
            return defensiveLine.Standable(map) ? defensiveLine : IntVec3.Invalid;
        }

        /// <summary>
        /// Checks if a colonist is combat-capable based on skills, traits, and backstory.
        /// </summary>
        private static bool IsCombatCapable(Pawn pawn)
        {
            try
            {
                // Check if incapable of violence
                if (pawn.WorkTagIsDisabled(WorkTags.Violent))
                {
                    RimWatchLogger.Debug($"DefenseAutomation: {pawn.LabelShort} incapable of violence");
                    return false;
                }

                // Check traits that prevent combat
                if (pawn.story?.traits != null)
                {
                    // Check for pacifist-like traits
                    foreach (Trait trait in pawn.story.traits.allTraits)
                    {
                        if (trait.def.defName == "Wimp" || 
                            trait.def.defName == "NonViolent")
                        {
                            RimWatchLogger.Debug($"DefenseAutomation: {pawn.LabelShort} has non-combat trait: {trait.LabelCap}");
                            return false;
                        }
                    }
                }

                // Check minimum combat skills (Shooting OR Melee >= 1)
                int shootingSkill = pawn.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0;
                int meleeSkill = pawn.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0;

                if (shootingSkill == 0 && meleeSkill == 0)
                {
                    RimWatchLogger.Debug($"DefenseAutomation: {pawn.LabelShort} has no combat skills (Shooting: 0, Melee: 0)");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Debug($"DefenseAutomation: Error checking combat capability for {pawn?.LabelShort}: {ex.Message}");
                return false; // Assume not combat-capable if error
            }
        }

        /// <summary>
        /// Calculates combat effectiveness score for a colonist.
        /// Higher score = better fighter.
        /// </summary>
        private static float GetCombatScore(Pawn pawn)
        {
            try
            {
                float score = 0f;

                // Skills (Shooting is primary, Melee is backup)
                int shootingSkill = pawn.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0;
                int meleeSkill = pawn.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0;

                score += shootingSkill * 10f; // Shooting is primary
                score += meleeSkill * 5f;     // Melee is secondary

                // Weapon bonus
                if (pawn.equipment?.Primary != null)
                {
                    float weaponDmg = pawn.equipment.Primary.GetStatValue(StatDefOf.RangedWeapon_DamageMultiplier);
                    score += weaponDmg * 20f; // Having a weapon is important
                }
                else
                {
                    score -= 50f; // Penalty for being unarmed
                }

                // Combat traits
                if (pawn.story?.traits != null)
                {
                    foreach (Trait trait in pawn.story.traits.allTraits)
                    {
                        string traitName = trait.def.defName;

                        // Positive combat traits
                        if (traitName == "Brawler") score += 30f;
                        if (traitName == "Tough") score += 20f;
                        if (traitName == "Bloodlust") score += 15f;
                        if (traitName == "Trigger-Happy") score += 10f;

                        // Negative combat traits
                        if (traitName == "Wimp") score -= 100f;
                        if (traitName == "NonViolent") score -= 200f;
                        if (traitName == "Pyromaniac") score -= 10f; // Unreliable
                    }
                }

                // Health penalties
                if (pawn.health.hediffSet.PainTotal > 0.3f)
                {
                    score -= 30f; // In pain
                }

                if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Sight))
                {
                    score -= 80f; // Can't see well
                }

                return score;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Debug($"DefenseAutomation: Error calculating combat score for {pawn?.LabelShort}: {ex.Message}");
                return 0f; // Low score on error
            }
        }

        // ==================== v0.7 ADVANCED FEATURES ====================

        private static int lastDefensivePositioningTick = -9999;
        private const int DefensivePositioningCooldown = 180; // 3 seconds (important for combat)
        
        private static int lastTurretRepairTick = -9999;
        private const int TurretRepairCooldown = 600; // 10 seconds

        /// <summary>
        /// Forms defensive lines with colonists behind cover.
        /// NEW in v0.7 - Tactical positioning system.
        /// </summary>
        private static void AutoFormDefensiveLines(Map map, DefenseStatus status)
        {
            try
            {
                // Only run during active combat
                if (status.EnemyCount == 0) return;

                // Cooldown check
                int currentTick = Find.TickManager.TicksGame;
                if (currentTick - lastDefensivePositioningTick < DefensivePositioningCooldown)
                {
                    return;
                }

                // Get drafted colonists
                List<Pawn> defenders = map.mapPawns.FreeColonistsSpawned
                    .Where(p => p.drafter != null && p.drafter.Drafted &&
                               !p.Downed && !p.Dead &&
                               IsCombatCapable(p))
                    .ToList();

                if (defenders.Count == 0) return;

                // Find base center for defensive perimeter
                IntVec3 baseCenter = Automation.BuildingPlacement.BaseZoneCache.BaseCenter != IntVec3.Invalid 
                    ? Automation.BuildingPlacement.BaseZoneCache.BaseCenter 
                    : map.Center;

                // Find enemies
                List<Pawn> enemies = map.mapPawns.AllPawnsSpawned
                    .Where(p => p.HostileTo(Faction.OfPlayer) && !p.Dead && !p.Downed)
                    .ToList();

                if (enemies.Count == 0) return;

                // Calculate enemy approach direction
                IntVec3 enemyCentroid = CalculateCentroid(enemies.Select(e => e.Position).ToList());
                IntVec3 defenseDirection = baseCenter - enemyCentroid;

                // Find defensive positions (with cover)
                List<IntVec3> defensivePositions = FindDefensivePositions(map, baseCenter, defenseDirection, defenders.Count);

                if (defensivePositions.Count == 0)
                {
                    RimWatchLogger.Debug("DefenseAutomation: No defensive positions found");
                    return;
                }

                // Assign defenders to positions
                int positionsAssigned = 0;
                for (int i = 0; i < Math.Min(defenders.Count, defensivePositions.Count); i++)
                {
                    Pawn defender = defenders[i];
                    IntVec3 position = defensivePositions[i];

                    // Check if defender is already near this position
                    if (defender.Position.DistanceTo(position) < 3f)
                    {
                        continue; // Already in good position
                    }

                    // Order defender to move to position
                    Job gotoJob = JobMaker.MakeJob(JobDefOf.Goto, position);
                    gotoJob.playerForced = true;
                    
                    if (defender.jobs.TryTakeOrderedJob(gotoJob, JobTag.Misc))
                    {
                        positionsAssigned++;
                        RimWatchLogger.Debug($"🛡️ DefenseAutomation: Positioned {defender.LabelShort} at defensive line ({position.x}, {position.z})");
                    }
                }

                if (positionsAssigned > 0)
                {
                    RimWatchLogger.Info($"🛡️ DefenseAutomation: Formed defensive line with {positionsAssigned} colonist(s)");
                }

                lastDefensivePositioningTick = currentTick;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("DefenseAutomation: Error in AutoFormDefensiveLines", ex);
            }
        }

        /// <summary>
        /// Calculates centroid (average position) of a list of positions.
        /// </summary>
        private static IntVec3 CalculateCentroid(List<IntVec3> positions)
        {
            if (positions.Count == 0) return IntVec3.Invalid;

            int sumX = 0, sumZ = 0;
            foreach (IntVec3 pos in positions)
            {
                sumX += pos.x;
                sumZ += pos.z;
            }

            return new IntVec3(sumX / positions.Count, 0, sumZ / positions.Count);
        }

        /// <summary>
        /// Finds defensive positions with cover.
        /// </summary>
        private static List<IntVec3> FindDefensivePositions(Map map, IntVec3 baseCenter, IntVec3 direction, int count)
        {
            List<IntVec3> positions = new List<IntVec3>();

            try
            {
                // Search for positions between base and enemy (defensive line)
                int searchRadius = 15;

                foreach (IntVec3 cell in GenRadial.RadialCellsAround(baseCenter, searchRadius, true))
                {
                    if (!cell.InBounds(map)) continue;
                    if (!cell.Standable(map)) continue;
                    if (map.fogGrid.IsFogged(cell)) continue;

                    // Check for cover (walls, sandbags, etc.)
                    bool hasCover = false;
                    foreach (Thing thing in cell.GetThingList(map))
                    {
                        if (thing.def.Fillage == FillCategory.Partial || 
                            thing.def.Fillage == FillCategory.Full)
                        {
                            hasCover = true;
                            break;
                        }
                    }

                    // Prefer positions with cover
                    if (hasCover || positions.Count < count)
                    {
                        positions.Add(cell);

                        if (positions.Count >= count) break;
                    }
                }

                return positions;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("DefenseAutomation: Error in FindDefensivePositions", ex);
                return positions;
            }
        }

        /// <summary>
        /// Orders tactical retreat when overwhelmed.
        /// NEW in v0.7 - Smart retreat logic.
        /// </summary>
        private static void AutoTacticalRetreat(Map map, DefenseStatus status)
        {
            try
            {
                // v0.8.3: Log execution start
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                // Only check if enemies present
                if (status.EnemyCount == 0) return;

                // v0.8.0: CRITICAL FIX - Count combat-capable colonists (not just drafted)
                int draftedColonists = map.mapPawns.FreeColonistsSpawned
                    .Count(p => p.drafter != null && p.drafter.Drafted);

                // v0.8.0: CRITICAL FIX - Don't retreat if we have NO drafted colonists (no fight happening yet)
                if (draftedColonists == 0) return;

                // Retreat only if enemy count is MORE THAN 3x our drafted colonists AND we're actually losing
                bool overwhelming = status.EnemyCount > draftedColonists * 3;
                
                if (!overwhelming) return;

                // v0.8.0: CRITICAL FIX - Cooldown to prevent spam
                int currentTick = Find.TickManager.TicksGame;
                if (currentTick - _lastRetreatWarningTick < RetreatWarningCooldown) return;
                _lastRetreatWarningTick = currentTick;
                
                // v0.8.3: Log retreat decision
                RimWatchLogger.LogDecision("DefenseAutomation", "TacticalRetreat", new Dictionary<string, object>
                {
                    { "enemyCount", status.EnemyCount },
                    { "draftedColonists", draftedColonists },
                    { "outnumberedRatio", (float)status.EnemyCount / draftedColonists },
                    { "thresholdRatio", 3.0f }
                });

                RimWatchLogger.Warning($"⚠️ DefenseAutomation: OVERWHELMING ENEMY FORCE ({status.EnemyCount} vs {draftedColonists})! Ordering tactical retreat!");

                // Find base center (safe zone)
                IntVec3 baseCenter = Automation.BuildingPlacement.BaseZoneCache.BaseCenter != IntVec3.Invalid 
                    ? Automation.BuildingPlacement.BaseZoneCache.BaseCenter 
                    : map.Center;

                // Order all drafted colonists to retreat to base
                List<Pawn> retreaters = map.mapPawns.FreeColonistsSpawned
                    .Where(p => p.drafter != null && p.drafter.Drafted &&
                               !p.Downed && !p.Dead)
                    .ToList();

                int retreatOrders = 0;
                foreach (Pawn colonist in retreaters)
                {
                    // Find safe position near base center
                    IntVec3 safePos = CellFinder.RandomClosewalkCellNear(baseCenter, map, 5);

                    if (safePos.IsValid)
                    {
                        Job retreatJob = JobMaker.MakeJob(JobDefOf.Goto, safePos);
                        retreatJob.playerForced = true;

                        if (colonist.jobs.TryTakeOrderedJob(retreatJob, JobTag.Misc))
                        {
                            retreatOrders++;
                            RimWatchLogger.Debug($"🏃 DefenseAutomation: {colonist.LabelShort} retreating to base");
                        }
                    }
                }

                if (retreatOrders > 0)
                {
                    // v0.8.3: Log execution end
                    stopwatch.Stop();
                    RimWatchLogger.LogExecutionEnd("DefenseAutomation", "AutoTacticalRetreat", true, stopwatch.ElapsedMilliseconds,
                        $"Ordered {retreatOrders} colonists to retreat");
                    
                    RimWatchLogger.Warning($"🏃 DefenseAutomation: {retreatOrders} colonist(s) retreating to safety!");
                }
                else
                {
                    // v0.8.3: Log execution end (failed to issue orders)
                    stopwatch.Stop();
                    RimWatchLogger.LogFailure("DefenseAutomation", "AutoTacticalRetreat", "Failed to issue retreat orders",
                        new Dictionary<string, object>
                        {
                            { "retreaters", retreaters.Count },
                            { "baseCenterValid", baseCenter != IntVec3.Invalid }
                        });
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("DefenseAutomation: Error in AutoTacticalRetreat", ex);
            }
        }

        /// <summary>
        /// Automatically repairs damaged turrets.
        /// NEW in v0.7 - Turret maintenance.
        /// </summary>
        private static void AutoRepairTurrets(Map map)
        {
            try
            {
                // Cooldown check
                int currentTick = Find.TickManager.TicksGame;
                if (currentTick - lastTurretRepairTick < TurretRepairCooldown)
                {
                    return;
                }

                // Find damaged turrets
                List<Building> damagedTurrets = map.listerBuildings.allBuildingsColonist
                    .Where(b => b.def.building?.turretGunDef != null &&
                               b.HitPoints < b.MaxHitPoints * 0.7f) // Less than 70% HP
                    .OrderBy(b => (float)b.HitPoints / b.MaxHitPoints)
                    .ToList();

                if (damagedTurrets.Count == 0)
                {
                    lastTurretRepairTick = currentTick;
                    return;
                }

                // Get repair designation def
                DesignationDef repairDef = DefDatabase<DesignationDef>.GetNamedSilentFail("Repair");
                if (repairDef == null)
                {
                    RimWatchLogger.Debug("DefenseAutomation: Repair designation not found");
                    return;
                }
                
                int repairsScheduled = 0;

                foreach (Building turret in damagedTurrets.Take(3)) // Limit to 3
                {
                    // Check if already designated for repair
                    if (map.designationManager.DesignationOn(turret, repairDef) != null)
                    {
                        continue;
                    }

                    // Add repair designation
                    Designation repairDesignation = new Designation(turret, repairDef);
                    map.designationManager.AddDesignation(repairDesignation);

                    repairsScheduled++;

                    float hpPercent = (float)turret.HitPoints / turret.MaxHitPoints;
                    RimWatchLogger.Debug($"🔧 DefenseAutomation: Scheduled turret repair ({hpPercent:P0} HP) at ({turret.Position.x}, {turret.Position.z})");
                }

                if (repairsScheduled > 0)
                {
                    RimWatchLogger.Info($"🔧 DefenseAutomation: Scheduled {repairsScheduled} turret(s) for repair");
                }

                lastTurretRepairTick = currentTick;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("DefenseAutomation: Error in AutoRepairTurrets", ex);
            }
        }

        // ==================== v0.8.0: COMBAT POSITIONING SYSTEM ====================

        /// <summary>
        /// v0.8.0: Finds optimal combat position for a colonist based on weapon range, LoS, and cover.
        /// </summary>
        private static IntVec3 FindOptimalCombatPosition(Pawn colonist, Pawn target, Map map)
        {
            try
            {
                // Get weapon range
                float range = GetWeaponRange(colonist);
                float optimalDistance = range * 0.7f; // 70% of max range

                // Search in ring around target at optimal distance
                List<IntVec3> candidates = GenRadial.RadialCellsAround(target.Position, (int)optimalDistance, true)
                    .Where(c => c.InBounds(map) && c.Standable(map))
                    .Take(20) // Limit search for performance
                    .ToList();

                // Score each position
                IntVec3 bestPos = colonist.Position;
                float bestScore = -9999f;

                foreach (IntVec3 candidate in candidates)
                {
                    float score = ScoreCombatPosition(candidate, colonist, target, map);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestPos = candidate;
                    }
                }

                return bestPos;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Warning($"DefenseAutomation: Error finding combat position: {ex.Message}");
                return colonist.Position;
            }
        }

        /// <summary>
        /// v0.8.0: Scores a combat position based on LoS, cover, distance, and spacing.
        /// </summary>
        private static float ScoreCombatPosition(IntVec3 pos, Pawn shooter, Pawn target, Map map)
        {
            float score = 0f;

            try
            {
                // 1. Line of Sight (CRITICAL - can't shoot without it!)
                if (!GenSight.LineOfSight(pos, target.Position, map, true))
                    return -1000f; // Useless position

                // 2. Cover rating (0-50 points)
                Thing cover = pos.GetCover(map);
                if (cover != null)
                {
                    float coverValue = cover.def.fillPercent;
                    score += coverValue * 50f; // Max +50 for full cover
                }

                // 3. Distance optimization (prefer 70% of weapon range)
                float distance = pos.DistanceTo(target.Position);
                float weaponRange = GetWeaponRange(shooter);
                float optimalDist = weaponRange * 0.7f;
                float distPenalty = Math.Abs(distance - optimalDist) * 2f;
                score -= distPenalty;

                // 4. Spacing (don't cluster with friendlies)
                int nearbyFriendlies = map.mapPawns.FreeColonistsSpawned
                    .Count(p => p.Position.DistanceTo(pos) < 3);
                score -= nearbyFriendlies * 10f;

                // 5. Avoid being in the open (prefer near walls/obstacles)
                bool hasAdjacentCover = GenAdj.CardinalDirections
                    .Any(dir => (pos + dir).GetCover(map) != null);
                if (hasAdjacentCover)
                    score += 10f;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Warning($"DefenseAutomation: Error scoring position: {ex.Message}");
                return -999f;
            }

            return score;
        }

        /// <summary>
        /// Gets weapon range for a pawn (used in positioning).
        /// </summary>
        private static float GetWeaponRange(Pawn pawn)
        {
            if (pawn?.equipment?.Primary != null)
            {
                ThingWithComps weapon = pawn.equipment.Primary;
                VerbProperties verbProps = weapon.def.Verbs?.FirstOrDefault();
                return verbProps?.range ?? 2f;
            }
            return 2f; // Melee range
        }
        
        // ==================== v0.8.0: WEAPON AUTOMATION ====================
        
        private static int _lastWeaponCheckTick = 0;
        private const int WeaponCheckInterval = 3600; // 1 minute
        
        /// <summary>
        /// v0.8.0: Auto-upgrade colonist weapons from storage.
        /// </summary>
        public static void AutoUpgradeWeapons(Map map)
        {
            try
            {
                // v0.8.1: Check if enabled in settings
                if (!RimWatchMod.Settings.weaponAutomationEnabled) return;
                
                int currentTick = Find.TickManager.TicksGame;
                if (currentTick - _lastWeaponCheckTick < WeaponCheckInterval) return;
                _lastWeaponCheckTick = currentTick;
                
                // v0.8.1: CRITICAL FIX - Create a copy to avoid "Collection was modified" error
                List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned.ToList();
                
                foreach (Pawn colonist in colonists)
                {
                    if (colonist.Dead || colonist.Downed || colonist.equipment == null) continue;
                    CheckAndUpgradeWeapon(colonist, map);
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error($"DefenseAutomation: Error in AutoUpgradeWeapons: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Check and upgrade weapon for a colonist.
        /// </summary>
        private static void CheckAndUpgradeWeapon(Pawn colonist, Map map)
        {
            try
            {
                // Find all available weapons
                var weapons = map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon)
                    .OfType<ThingWithComps>()
                    .Where(w => !w.IsForbidden(Faction.OfPlayer) && w.def.IsWeapon)
                    .ToList();
                
                if (weapons.Count == 0) return;
                
                // Score each weapon
                float currentScore = colonist.equipment.Primary != null 
                    ? ScoreWeapon(colonist.equipment.Primary, colonist) 
                    : 0f;
                
                var best = weapons.OrderByDescending(w => ScoreWeapon(w, colonist)).FirstOrDefault();
                
                if (best != null && ScoreWeapon(best, colonist) > currentScore * 1.3f) // 30% better
                {
                    Job equipJob = JobMaker.MakeJob(JobDefOf.Equip, best);
                    colonist.jobs.TryTakeOrderedJob(equipJob, JobTag.Misc);
                    RimWatchLogger.Info($"🔫 DefenseAutomation: {colonist.LabelShort} equipping {best.Label}");
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Warning($"DefenseAutomation: Error checking weapon for {colonist.LabelShort}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Score weapon quality for a colonist.
        /// </summary>
        private static float ScoreWeapon(ThingWithComps weapon, Pawn user)
        {
            float score = 0f;
            
            // DPS calculation
            VerbProperties verb = weapon.def.Verbs?.FirstOrDefault();
            if (verb != null)
            {
                float damage = verb.defaultProjectile?.projectile?.GetDamageAmount(weapon) ?? 1f;
                float cooldown = verb.warmupTime + verb.defaultCooldownTime;
                float dps = cooldown > 0 ? damage / cooldown : 0f;
                score += dps * 10f;
                
                // Range bonus
                score += verb.range * 2f;
                
                // Accuracy bonus
                score += verb.accuracyLong * 20f;
            }
            
            // Quality
            if (weapon.TryGetQuality(out QualityCategory quality))
            {
                score += (int)quality * 15f;
            }
            
            // Skill match
            int shootingSkill = user.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0;
            int meleeSkill = user.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
            
            if (weapon.def.IsRangedWeapon && shootingSkill > meleeSkill)
                score += 50f;
            else if (weapon.def.IsMeleeWeapon && meleeSkill > shootingSkill)
                score += 50f;
            
            return score;
        }
    }
}
