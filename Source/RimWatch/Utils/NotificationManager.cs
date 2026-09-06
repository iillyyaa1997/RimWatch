using System;
using System.Collections.Generic;
using System.Text;
using RimWatch.Settings;
using RimWorld;
using Verse;

namespace RimWatch.Utils
{
    /// <summary>
    /// Notification categories for RimWatch actions
    /// </summary>
    public enum NotificationCategory
    {
        Building,    // 🏗️ Construction, blueprints, rooms
        Work,        // 👷 Work priorities, schedules
        Farming,     // 🌾 Crops, animals, taming
        Resources,   // ⛏️ Mining, woodcutting, hunting
        Defense,     // ⚔️ Draft, equipment, positioning
        Medical,     // 🏥 Rescue, treatment, operations
        Trade,       // 💰 Forbid/allow, trading
        Social,      // 👥 Prisoners, mood, events
        Research     // 🔬 Research projects
    }

    /// <summary>
    /// Notification detail levels
    /// </summary>
    public enum NotificationLevel
    {
        Off = 0,        // No notifications
        Critical = 1,   // Life-threatening, raids, fires
        Important = 2,  // Blueprint, priority change, draft
        Moderate = 3,   // Hauling, planting, mining
        Verbose = 4,    // + coordinates, quality, materials
        Debug = 5       // Everything including execution time
    }

    /// <summary>
    /// Action importance for filtering
    /// </summary>
    public enum ActionImportance
    {
        Critical,   // Life-threatening, raids, fires
        Important,  // Blueprint, priority change, draft
        Moderate,   // Hauling, planting, mining
        Low         // Minor state changes
    }

    /// <summary>
    /// Manages in-game notifications for RimWatch actions.
    /// Shows only REAL actions (blueprints, jobs, designations), not internal AI decisions.
    /// </summary>
    public static class NotificationManager
    {
        private static RimWatchSettings Settings => RimWatchMod.Settings;

        // Category emoji mapping for visual identification
        private static readonly Dictionary<NotificationCategory, string> CategoryEmojis = new Dictionary<NotificationCategory, string>
        {
            { NotificationCategory.Building, "🏗️" },
            { NotificationCategory.Work, "👷" },
            { NotificationCategory.Farming, "🌾" },
            { NotificationCategory.Resources, "⛏️" },
            { NotificationCategory.Defense, "⚔️" },
            { NotificationCategory.Medical, "🏥" },
            { NotificationCategory.Trade, "💰" },
            { NotificationCategory.Social, "👥" },
            { NotificationCategory.Research, "🔬" }
        };

        // Throttling to prevent notification spam
        private static readonly Dictionary<string, int> _lastNotificationTick = new Dictionary<string, int>();
        private const int DefaultNotificationCooldown = 60; // 1 second = 60 ticks

        /// <summary>
        /// Send a notification about a real action performed by RimWatch.
        /// </summary>
        /// <param name="category">Automation category</param>
        /// <param name="importance">How important is this action</param>
        /// <param name="action">Brief action description (e.g., "Blueprint created")</param>
        /// <param name="details">Optional details dictionary for formatting</param>
        public static void SendNotification(
            NotificationCategory category,
            ActionImportance importance,
            string action,
            Dictionary<string, object>? details = null)
        {
            try
            {
                // 1. Check if notification system is enabled globally
                if (!Settings.notificationSystemEnabled)
                {
                    return;
                }

                // 2. Get notification level for this category
                NotificationLevel categoryLevel = GetCategoryLevel(category);
                if (categoryLevel == NotificationLevel.Off)
                {
                    return;
                }

                // 3. Check if this action should be shown at current level
                if (!ShouldNotify(categoryLevel, importance))
                {
                    return;
                }

                // 4. Throttle notifications to prevent spam
                string throttleKey = $"{category}:{action}";
                if (ShouldThrottle(throttleKey))
                {
                    return;
                }

                // 5. Format message based on level
                string message = FormatMessage(category, action, details, categoryLevel);

                // 6. Send to RimWorld Messages system
                Messages.Message(message, MessageTypeDefOf.SilentInput);

                // 7. Update throttle timestamp
                UpdateThrottle(throttleKey);
            }
            catch (Exception ex)
            {
                // Never let notification system crash the game
                RimWatchLogger.Error($"NotificationManager: Failed to send notification for {category}.{action}", ex);
            }
        }

        /// <summary>
        /// Get the notification level configured for a specific category
        /// </summary>
        private static NotificationLevel GetCategoryLevel(NotificationCategory category)
        {
            return category switch
            {
                NotificationCategory.Building => Settings.buildingNotificationLevel,
                NotificationCategory.Work => Settings.workNotificationLevel,
                NotificationCategory.Farming => Settings.farmingNotificationLevel,
                NotificationCategory.Resources => Settings.resourcesNotificationLevel,
                NotificationCategory.Defense => Settings.defenseNotificationLevel,
                NotificationCategory.Medical => Settings.medicalNotificationLevel,
                NotificationCategory.Trade => Settings.tradeNotificationLevel,
                NotificationCategory.Social => Settings.socialNotificationLevel,
                NotificationCategory.Research => Settings.researchNotificationLevel,
                _ => NotificationLevel.Off
            };
        }

        /// <summary>
        /// Determine if an action should be shown at the current notification level
        /// </summary>
        private static bool ShouldNotify(NotificationLevel categoryLevel, ActionImportance importance)
        {
            // Map importance to minimum required level
            int requiredLevel = importance switch
            {
                ActionImportance.Critical => (int)NotificationLevel.Critical,
                ActionImportance.Important => (int)NotificationLevel.Important,
                ActionImportance.Moderate => (int)NotificationLevel.Moderate,
                ActionImportance.Low => (int)NotificationLevel.Verbose,
                _ => (int)NotificationLevel.Debug
            };

            // Show if category level is >= required level
            return (int)categoryLevel >= requiredLevel;
        }

        /// <summary>
        /// Check if notification should be throttled
        /// </summary>
        private static bool ShouldThrottle(string key)
        {
            if (!_lastNotificationTick.ContainsKey(key))
            {
                return false;
            }

            int currentTick = Find.TickManager.TicksGame;
            int ticksSinceLastNotification = currentTick - _lastNotificationTick[key];
            return ticksSinceLastNotification < DefaultNotificationCooldown;
        }

        /// <summary>
        /// Update throttle timestamp
        /// </summary>
        private static void UpdateThrottle(string key)
        {
            _lastNotificationTick[key] = Find.TickManager.TicksGame;
        }

        /// <summary>
        /// Format notification message based on detail level
        /// </summary>
        private static string FormatMessage(
            NotificationCategory category,
            string action,
            Dictionary<string, object>? details,
            NotificationLevel level)
        {
            StringBuilder sb = new StringBuilder();

            // Prefix with "RimWatch"
            sb.Append("RimWatch");

            // Add emoji if enabled
            if (Settings.useEmojisInNotifications && CategoryEmojis.TryGetValue(category, out string emoji))
            {
                sb.Append(" ");
                sb.Append(emoji);
            }

            // Add category name
            sb.Append(" [");
            sb.Append(category.ToString());
            sb.Append("]: ");

            // Add action
            sb.Append(action);

            // Add details based on level
            if (details != null && details.Count > 0)
            {
                switch (level)
                {
                    case NotificationLevel.Critical:
                    case NotificationLevel.Important:
                        FormatSimpleDetails(sb, details);
                        break;

                    case NotificationLevel.Moderate:
                        FormatModerateDetails(sb, details);
                        break;

                    case NotificationLevel.Verbose:
                        FormatVerboseDetails(sb, details);
                        break;

                    case NotificationLevel.Debug:
                        FormatDebugDetails(sb, details);
                        break;
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Format simple details (Critical/Important level)
        /// Example: "Blueprint created - Bed at (120, 45)"
        /// </summary>
        private static void FormatSimpleDetails(StringBuilder sb, Dictionary<string, object> details)
        {
            // Show only essential info in one line
            if (details.TryGetValue("type", out var type))
            {
                sb.Append(" - ");
                sb.Append(type);
            }

            if (Settings.showCoordinates && details.TryGetValue("position", out var pos))
            {
                sb.Append(" at ");
                sb.Append(pos);
            }
        }

        /// <summary>
        /// Format moderate details (Moderate level)
        /// Example: "Blueprint created - Bed (Normal quality) at (120, 45) for John"
        /// </summary>
        private static void FormatModerateDetails(StringBuilder sb, Dictionary<string, object> details)
        {
            if (details.TryGetValue("type", out var type))
            {
                sb.Append(" - ");
                sb.Append(type);
            }

            if (details.TryGetValue("quality", out var quality))
            {
                sb.Append(" (");
                sb.Append(quality);
                sb.Append(")");
            }

            if (Settings.showCoordinates && details.TryGetValue("position", out var pos))
            {
                sb.Append(" at ");
                sb.Append(pos);
            }

            if (Settings.showPawnNames && details.TryGetValue("for_pawn", out var pawn))
            {
                sb.Append(" for ");
                sb.Append(pawn);
            }
            else if (Settings.showPawnNames && details.TryGetValue("pawn", out var pawnName))
            {
                sb.Append(" - ");
                sb.Append(pawnName);
            }
        }

        /// <summary>
        /// Format verbose details (Verbose level)
        /// Multi-line format with all important details
        /// </summary>
        private static void FormatVerboseDetails(StringBuilder sb, Dictionary<string, object> details)
        {
            sb.AppendLine();

            foreach (var kvp in details)
            {
                // Skip internal fields
                if (kvp.Key.StartsWith("_")) continue;

                // Format key-value pair
                sb.Append("  ");
                sb.Append(FormatKey(kvp.Key));
                sb.Append(": ");
                sb.Append(kvp.Value?.ToString() ?? "null");
                sb.AppendLine();
            }
        }

        /// <summary>
        /// Format debug details (Debug level)
        /// Full details including execution time and internal state
        /// </summary>
        private static void FormatDebugDetails(StringBuilder sb, Dictionary<string, object> details)
        {
            sb.AppendLine();

            foreach (var kvp in details)
            {
                // Show everything including internal fields
                sb.Append("  ");
                sb.Append(FormatKey(kvp.Key));
                sb.Append(": ");
                sb.Append(kvp.Value?.ToString() ?? "null");
                sb.AppendLine();
            }
        }

        /// <summary>
        /// Format dictionary key for display (convert snake_case to Title Case)
        /// </summary>
        private static string FormatKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return key;

            // Replace underscores with spaces and capitalize
            string[] parts = key.Split('_');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length > 0)
                {
                    parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1);
                }
            }

            return string.Join(" ", parts);
        }

        /// <summary>
        /// Clear all notification throttles (for debugging)
        /// </summary>
        public static void ClearThrottles()
        {
            int count = _lastNotificationTick.Count;
            _lastNotificationTick.Clear();
            RimWatchLogger.Debug($"NotificationManager: Cleared {count} throttle entries");
        }
    }
}
