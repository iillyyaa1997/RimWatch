namespace RimWatch.Automation.ColonyDevelopment
{
    /// <summary>
    /// Представляет задачу развития колонии.
    /// </summary>
    public class ColonyTask
    {
        /// <summary>
        /// Описание задачи.
        /// </summary>
        public string Description { get; set; }
        
        /// <summary>
        /// Приоритет задачи (0-100, где 100 - наивысший).
        /// </summary>
        public int Priority { get; set; }
        
        /// <summary>
        /// Конструктор.
        /// </summary>
        public ColonyTask(string description, int priority)
        {
            Description = description;
            Priority = priority;
        }
    }
}

