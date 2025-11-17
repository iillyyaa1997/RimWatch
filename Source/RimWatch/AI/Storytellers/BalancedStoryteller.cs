using RimWatch.Automation;
using RimWorld;
using Verse;

namespace RimWatch.AI.Storytellers
{
    /// <summary>
    /// ⚖️ Сбалансированный Менеджер (Balanced Manager)
    /// Оптимальная стратегия: баланс между всеми аспектами колонии.
    /// Идеально для новичков и стабильного развития.
    /// </summary>
    public class BalancedStoryteller : AIStoryteller
    {
        public override string Name => "Сбалансированный Менеджер";
        public override string Icon => "⚖️";
        public override string Description =>
            "Оптимальная стратегия: баланс между всеми аспектами колонии.\n" +
            "• Сбалансированное распределение работы\n" +
            "• Равномерное развитие колонии\n" +
            "• Приоритет на стабильность и рост\n" +
            "• Идеально для новичков";

        /// <summary>
        /// Определяет приоритет работы с учетом баланса всех потребностей.
        /// </summary>
        public override int DetermineWorkPriority(WorkTypeDef workType, Pawn colonist, ColonyNeeds needs)
        {
            string defName = workType.defName.ToLower();

            // 1. Критические работы - всегда высший приоритет
            if (IsCriticalWork(workType))
            {
                if (defName.Contains("doctor"))
                    return GetUrgencyBasedPriority(needs.MedicalUrgency);

                if (defName.Contains("cook") || defName.Contains("hunt"))
                    return GetUrgencyBasedPriority(needs.FoodUrgency);

                if (defName.Contains("firefight"))
                    return 1; // Всегда высший приоритет
            }

            // 2. Оборона - реагируем на угрозы
            if (defName.Contains("warden") || defName.Contains("handle"))
            {
                return GetUrgencyBasedPriority(needs.DefenseUrgency);
            }

            // 3. Строительство - важно для развития
            if (defName.Contains("construct"))
            {
                return GetUrgencyBasedPriority(needs.ConstructionUrgency);
            }

            // 4. Сельское хозяйство - стабильная еда
            if (defName.Contains("plant") || defName.Contains("grow"))
            {
                return GetUrgencyBasedPriority(needs.PlantUrgency);
            }

            // 5. Исследования - средний приоритет для развития
            if (defName.Contains("research"))
            {
                return GetUrgencyBasedPriority(needs.ResearchUrgency);
            }

            // 6. Производство - баланс между текущими нуждами
            if (IsProductionWork(workType))
            {
                // Если есть критические потребности, понижаем приоритет производства
                if (needs.FoodUrgency >= 3 || needs.MedicalUrgency >= 3)
                    return 4; // Низкий приоритет

                return 3; // Средний приоритет
            }

            // 7. Уборка и хозяйство - низкий приоритет, но не выключаем
            if (defName.Contains("clean") || defName.Contains("haul"))
            {
                return 4; // Низкий приоритет, но включено
            }

            // 8. Все остальное - средний приоритет
            return 3;
        }

        /// <summary>
        /// Тик рассказчика - периодически анализирует состояние колонии.
        /// </summary>
        public override void Tick()
        {
            // Balanced Manager не делает агрессивных изменений
            // Просто следит за балансом через приоритеты работы
            base.Tick();
        }

        /// <summary>
        /// Корректирует приоритет на основе навыков колониста.
        /// Сбалансированный менеджер учитывает способности колонистов.
        /// </summary>
        private int AdjustForSkills(int basePriority, Pawn colonist, WorkTypeDef workType)
        {
            if (colonist.skills == null) return basePriority;

            // Проверяем passion и уровень навыка
            foreach (SkillDef skill in workType.relevantSkills)
            {
                SkillRecord skillRecord = colonist.skills.GetSkill(skill);
                if (skillRecord == null) continue;

                // Если есть Major passion и хороший навык - повышаем приоритет
                if (skillRecord.passion == Passion.Major && skillRecord.Level >= 8)
                {
                    return System.Math.Max(1, basePriority - 1);
                }

                // Если есть Minor passion - небольшое повышение
                if (skillRecord.passion == Passion.Minor && skillRecord.Level >= 6)
                {
                    return System.Math.Max(1, basePriority - 1);
                }
            }

            return basePriority;
        }
    }
}

