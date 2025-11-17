using RimWatch.Automation;
using RimWatch.Utils;
using RimWorld;
using Verse;

namespace RimWatch.AI
{
    /// <summary>
    /// Базовый класс для AI-рассказчиков (стилей игры автопилота).
    /// Это НЕ замена игровых storytellers RimWorld!
    /// Это разные СТИЛИ РАБОТЫ автопилота: Осторожный, Агрессивный, Сбалансированный и т.д.
    /// Каждый стиль имеет свою логику принятия решений и управления колонией.
    /// </summary>
    public abstract class AIStoryteller
    {
        /// <summary>
        /// Название рассказчика.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Иконка рассказчика (эмодзи).
        /// </summary>
        public abstract string Icon { get; }

        /// <summary>
        /// Описание личности рассказчика.
        /// </summary>
        public abstract string Description { get; }

        /// <summary>
        /// Определяет приоритет работы для колониста на основе потребностей колонии.
        /// Manual Priorities: 1-4 (1 = высший, 4 = низший)
        /// Simple Checkboxes: возвращает 1 (enabled) или 0 (disabled)
        /// </summary>
        /// <param name="workType">Тип работы.</param>
        /// <param name="colonist">Колонист.</param>
        /// <param name="needs">Текущие потребности колонии.</param>
        /// <returns>Приоритет работы (1-4 для Manual, 0=disabled/1=enabled для Simple).</returns>
        public abstract int DetermineWorkPriority(WorkTypeDef workType, Pawn colonist, ColonyNeeds needs);

        /// <summary>
        /// Вызывается при активации рассказчика.
        /// </summary>
        public virtual void OnActivated()
        {
            RimWatchLogger.Info($"AIStoryteller: {Icon} {Name} activated");
        }

        /// <summary>
        /// Вызывается при деактивации рассказчика.
        /// </summary>
        public virtual void OnDeactivated()
        {
            RimWatchLogger.Info($"AIStoryteller: {Icon} {Name} deactivated");
        }

        /// <summary>
        /// Тик рассказчика - вызывается периодически для принятия глобальных решений.
        /// </summary>
        public virtual void Tick()
        {
            // Базовая реализация - ничего не делает
            // Подклассы могут переопределить для своей логики
        }

        /// <summary>
        /// Возвращает полное описание рассказчика с иконкой и названием.
        /// </summary>
        public string GetFullName()
        {
            return $"{Icon} {Name}";
        }

        /// <summary>
        /// Вспомогательный метод: определяет базовый приоритет на основе потребностей.
        /// </summary>
        protected int GetUrgencyBasedPriority(int urgency)
        {
            return urgency switch
            {
                3 => 1, // Критично - высший приоритет
                2 => 2, // Средняя срочность - второй приоритет
                _ => 3  // Низкая срочность - средний приоритет
            };
        }

        /// <summary>
        /// Вспомогательный метод: проверяет, является ли работа критически важной (еда/медицина).
        /// </summary>
        protected bool IsCriticalWork(WorkTypeDef workType)
        {
            string defName = workType.defName.ToLower();
            return defName.Contains("doctor") ||
                   defName.Contains("cook") ||
                   defName.Contains("hunt") ||
                   defName.Contains("firefight");
        }

        /// <summary>
        /// Вспомогательный метод: проверяет, является ли работа связанной с производством.
        /// </summary>
        protected bool IsProductionWork(WorkTypeDef workType)
        {
            string defName = workType.defName.ToLower();
            return defName.Contains("craft") ||
                   defName.Contains("smith") ||
                   defName.Contains("tailor") ||
                   defName.Contains("construct");
        }

        /// <summary>
        /// Вспомогательный метод: проверяет, является ли работа связанной с развитием.
        /// </summary>
        protected bool IsDevelopmentWork(WorkTypeDef workType)
        {
            string defName = workType.defName.ToLower();
            return defName.Contains("research") ||
                   defName.Contains("art");
        }
    }
}

