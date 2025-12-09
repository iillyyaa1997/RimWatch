using RimWatch.ML;
using RimWatch.Utils;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimWatch.UI
{
    /// <summary>
    /// Decision history viewer with filters, timeline, and analytics.
    /// Shows all AI decisions, their outcomes, and player overrides.
    /// v0.9.9: Decision history and analytics panel.
    /// </summary>
    public static class DecisionHistoryPanel
    {
        // UI Constants
        private const float WINDOW_WIDTH = 1000f;
        private const float WINDOW_HEIGHT = 700f;
        private const float SIDEBAR_WIDTH = 200f;
        private const float ROW_HEIGHT = 30f;
        private const float PADDING = 10f;
        private const int ENTRIES_PER_PAGE = 20;
        
        // Colors
        private static readonly Color COLOR_BG = new Color(0.15f, 0.15f, 0.2f, 1f);
        private static readonly Color COLOR_SIDEBAR = new Color(0.12f, 0.12f, 0.16f, 1f);
        private static readonly Color COLOR_ROW_EVEN = new Color(0.2f, 0.2f, 0.25f, 0.95f);
        private static readonly Color COLOR_ROW_ODD = new Color(0.18f, 0.18f, 0.22f, 0.95f);
        private static readonly Color COLOR_SUCCESS = new Color(0.2f, 0.8f, 0.2f, 1f);
        private static readonly Color COLOR_FAILURE = new Color(0.8f, 0.2f, 0.2f, 1f);
        private static readonly Color COLOR_NEUTRAL = new Color(0.6f, 0.6f, 0.6f, 1f);
        
        // State
        private static Vector2 _scrollPosition = Vector2.zero;
        private static string _categoryFilter = "All";
        private static bool _showSuccessOnly = false;
        private static bool _showFailuresOnly = false;
        private static int _currentPage = 0;
        private static DecisionViewMode _viewMode = DecisionViewMode.List;

        private static readonly string[] _categories = new[]
        {
            "All", "Building", "Production", "Farming", "Defense", "Trade", "Medical", "Social", "Research"
        };

        /// <summary>
        /// Draw decision history window.
        /// </summary>
        public static void DrawWindow(Rect inRect)
        {
            // Background
            Widgets.DrawBoxSolid(inRect, COLOR_BG);

            // Draw sidebar (filters)
            Rect sidebarRect = new Rect(0f, 0f, SIDEBAR_WIDTH, inRect.height);
            DrawSidebar(sidebarRect);

            // Draw main content
            Rect contentRect = new Rect(SIDEBAR_WIDTH + PADDING, 0f, inRect.width - SIDEBAR_WIDTH - PADDING, inRect.height);
            
            switch (_viewMode)
            {
                case DecisionViewMode.List:
                    DrawListView(contentRect);
                    break;
                case DecisionViewMode.Timeline:
                    DrawTimelineView(contentRect);
                    break;
                case DecisionViewMode.Analytics:
                    DrawAnalyticsView(contentRect);
                    break;
            }
        }

        /// <summary>
        /// Draw sidebar with filters and options.
        /// </summary>
        private static void DrawSidebar(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, COLOR_SIDEBAR);
            Rect contentRect = rect.ContractedBy(PADDING);
            
            float yOffset = 0f;

            // Title
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(new Rect(contentRect.x, contentRect.y + yOffset, contentRect.width, 30f), "📊 Фильтры");
            yOffset += 40f;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            // View mode selector
            Widgets.Label(new Rect(contentRect.x, contentRect.y + yOffset, contentRect.width, 25f), "Режим просмотра:");
            yOffset += 25f;

            foreach (DecisionViewMode mode in Enum.GetValues(typeof(DecisionViewMode)))
            {
                bool isSelected = _viewMode == mode;
                Color originalColor = GUI.color;
                
                if (isSelected)
                {
                    GUI.color = new Color(0.3f, 0.6f, 1f);
                }

                if (Widgets.ButtonText(new Rect(contentRect.x, contentRect.y + yOffset, contentRect.width, 25f), GetViewModeLabel(mode)))
                {
                    _viewMode = mode;
                }

                GUI.color = originalColor;
                yOffset += 28f;
            }

            yOffset += 10f;

            // Category filter
            Widgets.Label(new Rect(contentRect.x, contentRect.y + yOffset, contentRect.width, 25f), "Категория:");
            yOffset += 25f;

            foreach (string category in _categories)
            {
                bool isSelected = _categoryFilter == category;
                Color originalColor = GUI.color;
                
                if (isSelected)
                {
                    GUI.color = new Color(0.3f, 0.6f, 1f);
                }

                if (Widgets.ButtonText(new Rect(contentRect.x, contentRect.y + yOffset, contentRect.width, 25f), category))
                {
                    _categoryFilter = category;
                    _currentPage = 0;
                }

                GUI.color = originalColor;
                yOffset += 28f;
            }

            yOffset += 10f;

            // Success/Failure filters
            Widgets.CheckboxLabeled(new Rect(contentRect.x, contentRect.y + yOffset, contentRect.width, 25f), "Только успехи", ref _showSuccessOnly);
            yOffset += 28f;

            Widgets.CheckboxLabeled(new Rect(contentRect.x, contentRect.y + yOffset, contentRect.width, 25f), "Только провалы", ref _showFailuresOnly);
            yOffset += 28f;

            yOffset += 20f;

            // Export button
            if (Widgets.ButtonText(new Rect(contentRect.x, contentRect.y + yOffset, contentRect.width, 30f), "📤 Экспорт"))
            {
                ExportDecisionHistory();
            }
        }

        /// <summary>
        /// Draw list view of decisions.
        /// </summary>
        private static void DrawListView(Rect rect)
        {
            // Get decision analyzer summary
            var summary = DecisionAnalyzer.GetSummary();
            
            // Header
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.Label(new Rect(rect.x + PADDING, rect.y + PADDING, rect.width - PADDING * 2, 30f),
                $"📋 История решений ({summary.TotalDecisions} решений, {summary.SuccessfulDecisions} успешных)");
            
            Text.Font = GameFont.Small;

            // Table header
            float headerY = rect.y + 50f;
            DrawTableHeader(new Rect(rect.x + PADDING, headerY, rect.width - PADDING * 2, ROW_HEIGHT));

            // Scrollable content
            Rect scrollOuterRect = new Rect(rect.x + PADDING, headerY + ROW_HEIGHT + 5f, 
                rect.width - PADDING * 2, rect.height - headerY - ROW_HEIGHT - 70f);
            
            float contentHeight = ENTRIES_PER_PAGE * ROW_HEIGHT;
            Rect scrollViewRect = new Rect(0f, 0f, scrollOuterRect.width - 20f, contentHeight);

            Widgets.BeginScrollView(scrollOuterRect, ref _scrollPosition, scrollViewRect);

            // Draw decision rows (mock data for now)
            for (int i = 0; i < ENTRIES_PER_PAGE; i++)
            {
                float rowY = i * ROW_HEIGHT;
                Color rowColor = i % 2 == 0 ? COLOR_ROW_EVEN : COLOR_ROW_ODD;
                Rect rowRect = new Rect(0f, rowY, scrollViewRect.width, ROW_HEIGHT);
                
                DrawDecisionRow(rowRect, i, rowColor);
            }

            Widgets.EndScrollView();

            // Pagination
            DrawPagination(new Rect(rect.x + PADDING, rect.y + rect.height - 50f, rect.width - PADDING * 2, 40f));

            Text.Anchor = TextAnchor.UpperLeft;
        }

        /// <summary>
        /// Draw table header.
        /// </summary>
        private static void DrawTableHeader(Rect rect)
        {
            const float TIME_WIDTH = 100f;
            const float CATEGORY_WIDTH = 120f;
            const float ACTION_WIDTH = 200f;
            const float RESULT_WIDTH = 80f;
            
            float xOffset = 0f;

            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.8f);

            Widgets.Label(new Rect(rect.x + xOffset, rect.y, TIME_WIDTH, rect.height), "Время");
            xOffset += TIME_WIDTH;

            Widgets.Label(new Rect(rect.x + xOffset, rect.y, CATEGORY_WIDTH, rect.height), "Категория");
            xOffset += CATEGORY_WIDTH;

            Widgets.Label(new Rect(rect.x + xOffset, rect.y, ACTION_WIDTH, rect.height), "Действие");
            xOffset += ACTION_WIDTH;

            Widgets.Label(new Rect(rect.x + xOffset, rect.y, RESULT_WIDTH, rect.height), "Результат");

            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }

        /// <summary>
        /// Draw individual decision row.
        /// </summary>
        private static void DrawDecisionRow(Rect rect, int index, Color bgColor)
        {
            Widgets.DrawBoxSolid(rect, bgColor);

            const float TIME_WIDTH = 100f;
            const float CATEGORY_WIDTH = 120f;
            const float ACTION_WIDTH = 200f;
            const float RESULT_WIDTH = 80f;
            
            float xOffset = 5f;

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;

            // Time (mock data)
            string time = $"{index}h {(index * 13) % 60}m";
            Widgets.Label(new Rect(rect.x + xOffset, rect.y, TIME_WIDTH, rect.height), time);
            xOffset += TIME_WIDTH;

            // Category (mock data)
            string category = _categories[(index % (_categories.Length - 1)) + 1];
            Widgets.Label(new Rect(rect.x + xOffset, rect.y, CATEGORY_WIDTH, rect.height), category);
            xOffset += CATEGORY_WIDTH;

            // Action (mock data)
            string action = $"Decision #{index + 1}";
            Widgets.Label(new Rect(rect.x + xOffset, rect.y, ACTION_WIDTH, rect.height), action);
            xOffset += ACTION_WIDTH;

            // Result (mock data)
            bool success = (index % 3) != 0; // 66% success rate
            GUI.color = success ? COLOR_SUCCESS : COLOR_FAILURE;
            string result = success ? "✓ Успех" : "✗ Провал";
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(rect.x + xOffset, rect.y, RESULT_WIDTH, rect.height), result);
            GUI.color = Color.white;

            Text.Anchor = TextAnchor.UpperLeft;
        }

        /// <summary>
        /// Draw timeline view.
        /// </summary>
        private static void DrawTimelineView(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(rect.x, rect.y + rect.height / 2 - 30f, rect.width, 60f), 
                "📈 Временная шкала решений\n(В разработке)");
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }

        /// <summary>
        /// Draw analytics view.
        /// </summary>
        private static void DrawAnalyticsView(Rect rect)
        {
            var summary = DecisionAnalyzer.GetSummary();
            var topStrategies = DecisionAnalyzer.GetTopStrategies(5);
            var worstStrategies = DecisionAnalyzer.GetWorstStrategies(5);

            float yOffset = PADDING;

            // Title
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x + PADDING, rect.y + yOffset, rect.width - PADDING * 2, 30f), "📊 Аналитика решений");
            yOffset += 40f;

            Text.Font = GameFont.Small;

            // Overall stats
            Widgets.Label(new Rect(rect.x + PADDING, rect.y + yOffset, rect.width - PADDING * 2, 25f), 
                $"Всего решений: {summary.TotalDecisions}");
            yOffset += 25f;

            Widgets.Label(new Rect(rect.x + PADDING, rect.y + yOffset, rect.width - PADDING * 2, 25f), 
                $"Успешных: {summary.SuccessfulDecisions} ({summary.OverallSuccessRate:P1})");
            yOffset += 25f;

            Widgets.Label(new Rect(rect.x + PADDING, rect.y + yOffset, rect.width - PADDING * 2, 25f), 
                $"Отслеживаемых паттернов: {summary.TrackedPatterns}");
            yOffset += 35f;

            // Top strategies
            Widgets.Label(new Rect(rect.x + PADDING, rect.y + yOffset, rect.width - PADDING * 2, 30f), "✅ Топ-5 эффективных стратегий:");
            yOffset += 30f;

            foreach (var strategy in topStrategies)
            {
                GUI.color = COLOR_SUCCESS;
                Widgets.Label(new Rect(rect.x + PADDING + 20f, rect.y + yOffset, rect.width - PADDING * 2 - 20f, 25f), 
                    $"• {strategy.Category}/{strategy.Action}: {strategy.Effectiveness:P0} (уверенность: {strategy.Confidence:P0})");
                yOffset += 25f;
            }

            yOffset += 15f;
            GUI.color = Color.white;

            // Worst strategies
            Widgets.Label(new Rect(rect.x + PADDING, rect.y + yOffset, rect.width - PADDING * 2, 30f), "❌ Топ-5 неэффективных стратегий:");
            yOffset += 30f;

            foreach (var strategy in worstStrategies)
            {
                GUI.color = COLOR_FAILURE;
                Widgets.Label(new Rect(rect.x + PADDING + 20f, rect.y + yOffset, rect.width - PADDING * 2 - 20f, 25f), 
                    $"• {strategy.Category}/{strategy.Action}: {strategy.Effectiveness:P0} (уверенность: {strategy.Confidence:P0})");
                yOffset += 25f;
            }

            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        /// <summary>
        /// Draw pagination controls.
        /// </summary>
        private static void DrawPagination(Rect rect)
        {
            const float BUTTON_WIDTH = 100f;
            float centerX = rect.x + (rect.width - BUTTON_WIDTH * 2 - 100f) / 2f;

            // Previous button
            if (Widgets.ButtonText(new Rect(centerX, rect.y, BUTTON_WIDTH, 30f), "◀ Назад"))
            {
                _currentPage = UnityEngine.Mathf.Max(0, _currentPage - 1);
            }

            // Page number
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(centerX + BUTTON_WIDTH + 10f, rect.y, 80f, 30f), $"Стр. {_currentPage + 1}");
            Text.Anchor = TextAnchor.UpperLeft;

            // Next button
            if (Widgets.ButtonText(new Rect(centerX + BUTTON_WIDTH + 100f, rect.y, BUTTON_WIDTH, 30f), "Вперёд ▶"))
            {
                _currentPage++;
            }
        }

        /// <summary>
        /// Get label for view mode.
        /// </summary>
        private static string GetViewModeLabel(DecisionViewMode mode)
        {
            switch (mode)
            {
                case DecisionViewMode.List:
                    return "📋 Список";
                case DecisionViewMode.Timeline:
                    return "📈 Шкала";
                case DecisionViewMode.Analytics:
                    return "📊 Аналитика";
                default:
                    return mode.ToString();
            }
        }

        /// <summary>
        /// Export decision history to log.
        /// </summary>
        private static void ExportDecisionHistory()
        {
            string export = DecisionAnalyzer.ExportData();
            RimWatchLogger.Info("=== DECISION HISTORY EXPORT ===\n" + export);
            Messages.Message("История решений экспортирована в лог RimWatch", MessageTypeDefOf.PositiveEvent, false);
        }
    }

    /// <summary>
    /// Decision view mode enum.
    /// </summary>
    public enum DecisionViewMode
    {
        List,
        Timeline,
        Analytics
    }
}

