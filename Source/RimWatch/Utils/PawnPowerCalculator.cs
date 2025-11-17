using RimWorld;
using System;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimWatch.Utils
{
    /// <summary>
    /// v0.8.0: Power rating system for evaluating colonist capabilities.
    /// Used throughout automation systems for prioritization decisions.
    /// </summary>
    public static class PawnPowerCalculator
    {
        /// <summary>
        /// Calculates combat power rating (0-100).
        /// Higher = better fighter.
        /// </summary>
        public static float CalculateCombatPower(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.Downed) return 0f;

            float power = 0f;

            try
            {
                // 1. Shooting skill (0-30 points)
                if (pawn.skills != null)
                {
                    int shootingSkill = pawn.skills.GetSkill(SkillDefOf.Shooting)?.Level ?? 0;
                    power += shootingSkill * 1.5f; // Max 30 points

                    // 2. Melee skill (0-20 points)
                    int meleeSkill = pawn.skills.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
                    power += meleeSkill * 1.0f; // Max 20 points
                }

                // 3. Weapon DPS (0-25 points)
                if (pawn.equipment?.Primary != null)
                {
                    ThingWithComps weapon = pawn.equipment.Primary;
                    VerbProperties verbProps = weapon.def.Verbs?.FirstOrDefault();
                    if (verbProps != null)
                    {
                        float damage = verbProps.defaultProjectile?.projectile?.GetDamageAmount(weapon) ?? 0f;
                        float cooldown = verbProps.warmupTime + verbProps.defaultCooldownTime;
                        float dps = cooldown > 0 ? damage / cooldown : 0f;
                        power += Math.Min(dps * 2f, 25f); // Cap at 25 points
                    }
                }
                else
                {
                    // Unarmed penalty
                    power -= 10f;
                }

                // 4. Armor rating (0-15 points)
                float armorSharp = pawn.GetStatValue(StatDefOf.ArmorRating_Sharp, true);
                float armorBlunt = pawn.GetStatValue(StatDefOf.ArmorRating_Blunt, true);
                power += (armorSharp + armorBlunt) * 15f; // Max ~15 points with good armor

                // 5. Health penalties (-50 to 0)
                if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving))
                    power -= 50f; // Can't move = useless in combat
                
                if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
                    power -= 30f; // Can't use weapons properly

                float consciousnessLevel = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness);
                if (consciousnessLevel < 1f)
                    power *= consciousnessLevel; // Reduced effectiveness

                // 6. Trait bonuses/penalties
                if (pawn.story?.traits != null)
                {
                    if (pawn.story.traits.HasTrait(TraitDefOf.Brawler))
                        power += 20f; // Loves melee

                    if (pawn.story.traits.HasTrait(TraitDef.Named("Wimp")))
                        power -= 20f; // Awful fighter

                    if (pawn.story.traits.HasTrait(TraitDef.Named("Bloodlust")))
                        power += 15f; // Loves combat

                    if (pawn.story.traits.HasTrait(TraitDef.Named("Tough")))
                        power += 10f; // Takes damage better
                }

                // Clamp to 0-100 range
                return Mathf.Clamp(power, 0f, 100f);
            }
            catch (Exception ex)
            {
                RimWatchLogger.Warning($"PawnPowerCalculator: Error calculating combat power for {pawn?.LabelShort}: {ex.Message}");
                return 0f;
            }
        }

        /// <summary>
        /// Calculates work power rating (0-100).
        /// Higher = better worker (general productivity).
        /// </summary>
        public static float CalculateWorkPower(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.Downed) return 0f;

            float power = 0f;

            try
            {
                if (pawn.skills == null) return 0f;

                // 1. Average all skill levels (0-50 points)
                float avgSkill = 0f;
                int skillCount = 0;
                
                foreach (SkillDef skillDef in DefDatabase<SkillDef>.AllDefsListForReading)
                {
                    SkillRecord skill = pawn.skills.GetSkill(skillDef);
                    if (skill != null && !skill.TotallyDisabled)
                    {
                        avgSkill += skill.Level;
                        skillCount++;

                        // Passion bonuses
                        if (skill.passion == Passion.Minor)
                            power += 2f; // +2 per minor passion
                        else if (skill.passion == Passion.Major)
                            power += 5f; // +5 per major passion
                    }
                }

                if (skillCount > 0)
                {
                    avgSkill /= skillCount;
                    power += avgSkill * 2.5f; // Max 50 points for skill 20 average
                }

                // 2. Global work speed modifier (0-20 points)
                float workSpeed = pawn.GetStatValue(StatDefOf.WorkSpeedGlobal, true);
                power += (workSpeed - 1f) * 40f; // +40 points if 2x speed, -40 if 0x

                // 3. Trait bonuses/penalties
                if (pawn.story?.traits != null)
                {
                    if (pawn.story.traits.HasTrait(TraitDef.Named("Industrious")))
                        power += 15f; // Hard worker

                    if (pawn.story.traits.HasTrait(TraitDef.Named("Lazy")))
                        power -= 15f; // Slacker

                    if (pawn.story.traits.HasTrait(TraitDef.Named("QuickSleeper")))
                        power += 5f; // More waking hours
                    
                    // Note: SpeedOffset trait removed due to API compatibility
                }

                // 4. Age penalties (very young or very old)
                if (pawn.ageTracker != null)
                {
                    int age = pawn.ageTracker.AgeBiologicalYears;
                    if (age < 18)
                        power *= 0.7f; // Young and inexperienced
                    else if (age > 60)
                        power *= 0.85f; // Elderly, slower
                }

                // Clamp to 0-100 range
                return Mathf.Clamp(power, 0f, 100f);
            }
            catch (Exception ex)
            {
                RimWatchLogger.Warning($"PawnPowerCalculator: Error calculating work power for {pawn?.LabelShort}: {ex.Message}");
                return 0f;
            }
        }

        /// <summary>
        /// Calculates survival value rating (0-100).
        /// Higher = more valuable/irreplaceable to colony (rare skills, specialists).
        /// </summary>
        public static float CalculateSurvivalValue(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.Downed) return 0f;

            float value = 20f; // Base value - everyone matters

            try
            {
                if (pawn.skills == null) return value;

                // 1. Rare/critical skills (high-level specialists)
                int medicalSkill = pawn.skills.GetSkill(SkillDefOf.Medicine)?.Level ?? 0;
                if (medicalSkill >= 15)
                    value += 25f; // Master doctor - critical!

                int intellectualSkill = pawn.skills.GetSkill(SkillDefOf.Intellectual)?.Level ?? 0;
                if (intellectualSkill >= 15)
                    value += 20f; // Master researcher

                int socialSkill = pawn.skills.GetSkill(SkillDefOf.Social)?.Level ?? 0;
                if (socialSkill >= 15)
                    value += 15f; // Master diplomat/recruiter

                int animalsSkill = pawn.skills.GetSkill(SkillDefOf.Animals)?.Level ?? 0;
                if (animalsSkill >= 15)
                    value += 15f; // Master tamer

                int artSkill = pawn.skills.GetSkill(SkillDefOf.Artistic)?.Level ?? 0;
                if (artSkill >= 15)
                    value += 10f; // Master artist (wealth generation)

                // 2. Multiple high skills = versatile = valuable
                int skillsAbove10 = 0;
                foreach (SkillDef skillDef in DefDatabase<SkillDef>.AllDefsListForReading)
                {
                    SkillRecord skill = pawn.skills.GetSkill(skillDef);
                    if (skill != null && skill.Level >= 10)
                        skillsAbove10++;
                }

                value += skillsAbove10 * 3f; // +3 per skill above 10

                // 3. Special traits
                if (pawn.story?.traits != null)
                {
                    // Positive traits
                    if (pawn.story.traits.HasTrait(TraitDef.Named("NaturalMood")))
                        value += 10f; // Always happy = boosts colony mood

                    if (pawn.story.traits.HasTrait(TraitDef.Named("Psychopath")))
                        value += 5f; // Good for dark tasks (butchering, etc)

                    if (pawn.story.traits.HasTrait(TraitDef.Named("SuperImmune")))
                        value += 10f; // Never gets sick

                    // Negative traits reduce value
                    if (pawn.story.traits.HasTrait(TraitDef.Named("Pyromaniac")))
                        value -= 15f; // Dangerous!

                    if (pawn.story.traits.HasTrait(TraitDef.Named("ChemicalInterest")))
                        value -= 10f; // Drug addiction risk

                    if (pawn.story.traits.HasTrait(TraitDefOf.Abrasive))
                        value -= 5f; // Causes social fights
                }

                // 4. Nobility/titles (Royalty DLC)
                if (pawn.royalty != null && pawn.royalty.AllTitlesForReading.Any())
                {
                    value += 15f; // Noble = valuable for quests/trade
                }

                // Clamp to 0-100 range
                return Mathf.Clamp(value, 0f, 100f);
            }
            catch (Exception ex)
            {
                RimWatchLogger.Warning($"PawnPowerCalculator: Error calculating survival value for {pawn?.LabelShort}: {ex.Message}");
                return 20f; // Return base value on error
            }
        }

        /// <summary>
        /// Calculates overall colony value (composite of all metrics).
        /// Used for rescue priority, etc.
        /// </summary>
        public static float CalculateOverallValue(Pawn pawn)
        {
            if (pawn == null || pawn.Dead) return 0f;

            float combat = CalculateCombatPower(pawn);
            float work = CalculateWorkPower(pawn);
            float survival = CalculateSurvivalValue(pawn);

            // Weighted average: survival value matters most
            return (combat * 0.2f + work * 0.3f + survival * 0.5f);
        }
    }
}

