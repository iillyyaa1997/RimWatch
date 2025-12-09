using RimWatch.AI;
using RimWatch.AI.Storytellers;
using RimWatch.Core;
using RimWatch.Utils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimWatch.UI
{
    /// <summary>
    /// Beautiful storyteller selection panel with cards, preview, and comparison.
    /// </summary>
    public static class StorytellerSelectionPanel
    {
        // UI Constants - No hardcoded values!
        private const float WINDOW_WIDTH = 900f;
        private const float WINDOW_HEIGHT = 700f;
        private const float CARD_WIDTH = 260f;
        private const float CARD_HEIGHT = 180f;
        private const float CARD_SPACING = 15f;
        private const float PREVIEW_WIDTH = 400f;
        private const float PREVIEW_HEIGHT = 500f;
        private const float HEADER_HEIGHT = 60f;
        private const float FOOTER_HEIGHT = 80f;
        private const float PADDING = 20f;
        private const float PERSONALITY_BAR_HEIGHT = 20f;
        private const float PERSONALITY_BAR_SPACING = 8f;
        
        // Colors
        private static readonly Color COLOR_HEADER_BG = new Color(0.15f, 0.15f, 0.2f, 1f);
        private static readonly Color COLOR_CARD_BG = new Color(0.2f, 0.2f, 0.25f, 0.95f);
        private static readonly Color COLOR_CARD_SELECTED = new Color(0.3f, 0.5f, 0.7f, 1f);
        private static readonly Color COLOR_CARD_HOVER = new Color(0.25f, 0.35f, 0.45f, 1f);
        private static readonly Color COLOR_PREVIEW_BG = new Color(0.18f, 0.18f, 0.22f, 1f);
        private static readonly Color COLOR_ACCENT = new Color(0.4f, 0.7f, 1f, 1f);
        private static readonly Color COLOR_TEXT_TITLE = new Color(1f, 1f, 1f, 1f);
        private static readonly Color COLOR_TEXT_DESC = new Color(0.8f, 0.8f, 0.85f, 1f);
        
        // State
        private static AIStoryteller _selectedStoryteller = null;
        private static AIStoryteller _hoveredStoryteller = null;
        private static Vector2 _scrollPosition = Vector2.zero;
        private static bool _comparisonMode = false;
        
        // Available storytellers
        private static List<AIStoryteller> _availableStorytellers = new List<AIStoryteller>
        {
            new BalancedStoryteller(),
            new CautiousStoryteller(),
            new AggressiveStoryteller(),
            new ChaoticStoryteller(),
            new RandomStoryteller(),
            new CustomStoryteller()
        };

        /// <summary>
        /// Draw the storyteller selection window.
        /// </summary>
        public static void DrawWindow(Rect inRect)
        {
            // Ensure we have a selection
            if (_selectedStoryteller == null)
            {
                _selectedStoryteller = RimWatchCore.CurrentStoryteller ?? _availableStorytellers[0];
            }

            // Draw header
            Rect headerRect = new Rect(0f, 0f, inRect.width, HEADER_HEIGHT);
            DrawHeader(headerRect);

            // Draw content area
            Rect contentRect = new Rect(0f, HEADER_HEIGHT, inRect.width, inRect.height - HEADER_HEIGHT - FOOTER_HEIGHT);
            DrawContent(contentRect);

            // Draw footer
            Rect footerRect = new Rect(0f, inRect.height - FOOTER_HEIGHT, inRect.width, FOOTER_HEIGHT);
            DrawFooter(footerRect);
        }

        /// <summary>
        /// Draw header with title and mode toggle.
        /// </summary>
        private static void DrawHeader(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, COLOR_HEADER_BG);

            // Title
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect titleRect = new Rect(rect.x + PADDING, rect.y, rect.width * 0.6f, rect.height);
            Widgets.Label(titleRect, "🎭 Выбор AI Рассказчика");

            // Comparison mode toggle
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleRight;
            Rect toggleRect = new Rect(rect.x + rect.width - PADDING - 200f, rect.y + (rect.height - 30f) / 2f, 200f, 30f);
            
            string toggleLabel = _comparisonMode ? "📊 Режим сравнения" : "🎴 Режим карточек";
            if (Widgets.ButtonText(toggleRect, toggleLabel))
            {
                _comparisonMode = !_comparisonMode;
                RimWatchLogger.Debug($"StorytellerSelectionPanel: Switched to {(_comparisonMode ? "Comparison" : "Card")} mode");
            }

            Text.Anchor = TextAnchor.UpperLeft;
        }

        /// <summary>
        /// Draw main content area.
        /// </summary>
        private static void DrawContent(Rect rect)
        {
            if (_comparisonMode)
            {
                DrawComparisonView(rect);
            }
            else
            {
                DrawCardView(rect);
            }
        }

        /// <summary>
        /// Draw card selection view.
        /// </summary>
        private static void DrawCardView(Rect rect)
        {
            float cardsAreaWidth = rect.width - PREVIEW_WIDTH - PADDING * 3;
            
            // Cards area (left side)
            Rect cardsRect = new Rect(rect.x + PADDING, rect.y + PADDING, cardsAreaWidth, rect.height - PADDING * 2);
            DrawCards(cardsRect);

            // Preview area (right side)
            Rect previewRect = new Rect(rect.x + cardsAreaWidth + PADDING * 2, rect.y + PADDING, PREVIEW_WIDTH, rect.height - PADDING * 2);
            DrawPreview(previewRect);
        }

        /// <summary>
        /// Draw storyteller cards.
        /// </summary>
        private static void DrawCards(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, COLOR_PREVIEW_BG);

            // Calculate grid layout
            int columns = Mathf.FloorToInt((rect.width - PADDING) / (CARD_WIDTH + CARD_SPACING));
            int rows = Mathf.CeilToInt((float)_availableStorytellers.Count / columns);
            float totalHeight = rows * (CARD_HEIGHT + CARD_SPACING) + PADDING;

            Rect viewRect = new Rect(0f, 0f, rect.width - 20f, totalHeight);
            Rect scrollRect = rect.ContractedBy(PADDING);

            Widgets.BeginScrollView(scrollRect, ref _scrollPosition, viewRect, true);

            int index = 0;
            foreach (var storyteller in _availableStorytellers)
            {
                int row = index / columns;
                int col = index % columns;

                float cardX = col * (CARD_WIDTH + CARD_SPACING) + PADDING;
                float cardY = row * (CARD_HEIGHT + CARD_SPACING) + PADDING;

                Rect cardRect = new Rect(cardX, cardY, CARD_WIDTH, CARD_HEIGHT);
                DrawCard(cardRect, storyteller);

                index++;
            }

            Widgets.EndScrollView();
        }

        /// <summary>
        /// Draw individual storyteller card.
        /// </summary>
        private static void DrawCard(Rect rect, AIStoryteller storyteller)
        {
            bool isSelected = storyteller == _selectedStoryteller;
            bool isHovered = Mouse.IsOver(rect);

            // Background
            Color bgColor = isSelected ? COLOR_CARD_SELECTED : (isHovered ? COLOR_CARD_HOVER : COLOR_CARD_BG);
            Widgets.DrawBoxSolid(rect, bgColor);
            Widgets.DrawBox(rect, isSelected ? 2 : 1);

            // Icon and Name
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperCenter;
            Rect iconRect = new Rect(rect.x, rect.y + 10f, rect.width, 40f);
            Widgets.Label(iconRect, $"{storyteller.Icon}");

            Text.Font = GameFont.Small;
            Rect nameRect = new Rect(rect.x + 10f, rect.y + 55f, rect.width - 20f, 30f);
            Widgets.Label(nameRect, storyteller.Name);

            // Short description
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperLeft;
            Rect descRect = new Rect(rect.x + 10f, rect.y + 90f, rect.width - 20f, rect.height - 100f);
            string shortDesc = storyteller.Description.Split('\n')[0]; // First line only
            Widgets.Label(descRect, shortDesc);

            // Handle click
            if (Widgets.ButtonInvisible(rect))
            {
                _selectedStoryteller = storyteller;
                RimWatchLogger.Info($"StorytellerSelectionPanel: Selected {storyteller.Name}");
            }

            // Track hover
            if (isHovered)
            {
                _hoveredStoryteller = storyteller;
            }

            Text.Anchor = TextAnchor.UpperLeft;
        }

        /// <summary>
        /// Draw storyteller preview panel.
        /// </summary>
        private static void DrawPreview(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, COLOR_PREVIEW_BG);
            Widgets.DrawBox(rect, 1);

            AIStoryteller previewStoryteller = _hoveredStoryteller ?? _selectedStoryteller;
            if (previewStoryteller == null) return;

            Rect contentRect = rect.ContractedBy(PADDING);
            float yOffset = 0f;

            // Icon
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperCenter;
            Rect iconRect = new Rect(contentRect.x, contentRect.y + yOffset, contentRect.width, 50f);
            Widgets.Label(iconRect, previewStoryteller.Icon);
            yOffset += 60f;

            // Name
            Text.Font = GameFont.Medium;
            GUI.color = COLOR_TEXT_TITLE;
            Rect nameRect = new Rect(contentRect.x, contentRect.y + yOffset, contentRect.width, 30f);
            Widgets.Label(nameRect, previewStoryteller.Name);
            yOffset += 40f;
            GUI.color = Color.white;

            // Description
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = COLOR_TEXT_DESC;
            Rect descRect = new Rect(contentRect.x, contentRect.y + yOffset, contentRect.width, 120f);
            Widgets.Label(descRect, previewStoryteller.Description);
            yOffset += 130f;
            GUI.color = Color.white;

            // Personality traits
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(contentRect.x, contentRect.y + yOffset, contentRect.width, 25f), "🎯 Личность:");
            yOffset += 30f;

            // Get personality (with fallback for storytellers without GetPersonality method)
            var personality = GetStorytellerPersonality(previewStoryteller);
            if (personality != null)
            {
                DrawPersonalityBar(contentRect, ref yOffset, "⚠️ Риск", personality.RiskTolerance, Color.red);
                DrawPersonalityBar(contentRect, ref yOffset, "🏗️ Строительство", personality.BuildingSpeed, Color.yellow);
                DrawPersonalityBar(contentRect, ref yOffset, "💰 Торговля", personality.TradeAggressiveness, Color.green);
                DrawPersonalityBar(contentRect, ref yOffset, "🛡️ Оборона", personality.DefenseStyle, Color.cyan);
                DrawPersonalityBar(contentRect, ref yOffset, "🔬 Исследования", personality.ResearchPriority, Color.magenta);
                DrawPersonalityBar(contentRect, ref yOffset, "😊 Социализация", personality.SocialFocus, new Color(1f, 0.5f, 1f));
            }

            Text.Anchor = TextAnchor.UpperLeft;
        }

        /// <summary>
        /// Get personality from storyteller using reflection (supports both old and new storytellers).
        /// </summary>
        private static StorytellerPersonality GetStorytellerPersonality(AIStoryteller storyteller)
        {
            var method = storyteller.GetType().GetMethod("GetPersonality");
            if (method != null)
            {
                return method.Invoke(storyteller, null) as StorytellerPersonality;
            }

            // Fallback: Return balanced personality for old storytellers
            return new StorytellerPersonality
            {
                RiskTolerance = 0.5f,
                BuildingSpeed = 0.5f,
                TradeAggressiveness = 0.5f,
                DefenseStyle = 0.5f,
                ResearchPriority = 0.5f,
                SocialFocus = 0.5f
            };
        }

        /// <summary>
        /// Draw personality trait bar.
        /// </summary>
        private static void DrawPersonalityBar(Rect parentRect, ref float yOffset, string label, float value, Color color)
        {
            const float LABEL_WIDTH = 140f;
            const float BAR_WIDTH = 180f;

            Text.Font = GameFont.Tiny;
            Rect labelRect = new Rect(parentRect.x, parentRect.y + yOffset, LABEL_WIDTH, PERSONALITY_BAR_HEIGHT);
            Widgets.Label(labelRect, label);

            Rect barBgRect = new Rect(parentRect.x + LABEL_WIDTH, parentRect.y + yOffset, BAR_WIDTH, PERSONALITY_BAR_HEIGHT);
            Widgets.DrawBoxSolid(barBgRect, new Color(0.1f, 0.1f, 0.1f, 0.8f));

            Rect barRect = new Rect(barBgRect.x, barBgRect.y, barBgRect.width * value, barBgRect.height);
            Widgets.DrawBoxSolid(barRect, color);

            // Value label
            Text.Anchor = TextAnchor.MiddleRight;
            Rect valueRect = new Rect(parentRect.x + LABEL_WIDTH + BAR_WIDTH + 5f, parentRect.y + yOffset, 40f, PERSONALITY_BAR_HEIGHT);
            Widgets.Label(valueRect, $"{(value * 100f):F0}%");
            Text.Anchor = TextAnchor.UpperLeft;

            yOffset += PERSONALITY_BAR_HEIGHT + PERSONALITY_BAR_SPACING;
        }

        /// <summary>
        /// Draw comparison view.
        /// </summary>
        private static void DrawComparisonView(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, COLOR_PREVIEW_BG);
            Rect contentRect = rect.ContractedBy(PADDING);

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(new Rect(contentRect.x, contentRect.y, contentRect.width, 30f), "📊 Сравнение Рассказчиков");

            // Draw comparison table
            float yOffset = 40f;
            const float ROW_HEIGHT = 30f;
            const float COL_WIDTH = 120f;

            // Header row
            DrawComparisonHeader(contentRect, yOffset, COL_WIDTH);
            yOffset += ROW_HEIGHT;

            // Trait rows
            DrawComparisonRow(contentRect, yOffset, COL_WIDTH, "⚠️ Риск", st => GetStorytellerPersonality(st).RiskTolerance);
            yOffset += ROW_HEIGHT;
            DrawComparisonRow(contentRect, yOffset, COL_WIDTH, "🏗️ Строительство", st => GetStorytellerPersonality(st).BuildingSpeed);
            yOffset += ROW_HEIGHT;
            DrawComparisonRow(contentRect, yOffset, COL_WIDTH, "💰 Торговля", st => GetStorytellerPersonality(st).TradeAggressiveness);
            yOffset += ROW_HEIGHT;
            DrawComparisonRow(contentRect, yOffset, COL_WIDTH, "🛡️ Оборона", st => GetStorytellerPersonality(st).DefenseStyle);
            yOffset += ROW_HEIGHT;
            DrawComparisonRow(contentRect, yOffset, COL_WIDTH, "🔬 Исследования", st => GetStorytellerPersonality(st).ResearchPriority);
            yOffset += ROW_HEIGHT;
            DrawComparisonRow(contentRect, yOffset, COL_WIDTH, "😊 Социализация", st => GetStorytellerPersonality(st).SocialFocus);

            Text.Anchor = TextAnchor.UpperLeft;
        }

        /// <summary>
        /// Draw comparison table header.
        /// </summary>
        private static void DrawComparisonHeader(Rect rect, float yOffset, float colWidth)
        {
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;

            float xOffset = colWidth;
            foreach (var storyteller in _availableStorytellers)
            {
                Rect cellRect = new Rect(rect.x + xOffset, rect.y + yOffset, colWidth, 30f);
                Widgets.DrawBoxSolid(cellRect, COLOR_CARD_BG);
                Widgets.Label(cellRect, $"{storyteller.Icon}\n{storyteller.Name}");
                xOffset += colWidth;
            }
        }

        /// <summary>
        /// Draw comparison table row.
        /// </summary>
        private static void DrawComparisonRow(Rect rect, float yOffset, float colWidth, string label, System.Func<AIStoryteller, float> getValue)
        {
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;

            // Label column
            Rect labelRect = new Rect(rect.x, rect.y + yOffset, colWidth, 30f);
            Widgets.Label(labelRect, label);

            // Value columns
            Text.Anchor = TextAnchor.MiddleCenter;
            float xOffset = colWidth;
            foreach (var storyteller in _availableStorytellers)
            {
                Rect cellRect = new Rect(rect.x + xOffset, rect.y + yOffset, colWidth, 30f);
                float value = getValue(storyteller);
                
                // Color background based on value
                Color bgColor = Color.Lerp(new Color(0.3f, 0.1f, 0.1f), new Color(0.1f, 0.3f, 0.1f), value);
                Widgets.DrawBoxSolid(cellRect, bgColor);
                
                Widgets.Label(cellRect, $"{(value * 100f):F0}%");
                xOffset += colWidth;
            }
        }

        /// <summary>
        /// Draw footer with action buttons.
        /// </summary>
        private static void DrawFooter(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, COLOR_HEADER_BG);

            const float BUTTON_WIDTH = 150f;
            const float BUTTON_HEIGHT = 40f;
            float buttonY = rect.y + (rect.height - BUTTON_HEIGHT) / 2f;

            // Cancel button
            Rect cancelRect = new Rect(rect.x + PADDING, buttonY, BUTTON_WIDTH, BUTTON_HEIGHT);
            if (Widgets.ButtonText(cancelRect, "❌ Отмена"))
            {
                RimWatchLogger.Debug("StorytellerSelectionPanel: Cancelled");
                // Close window logic here
            }

            // Apply button
            Rect applyRect = new Rect(rect.x + rect.width - PADDING - BUTTON_WIDTH, buttonY, BUTTON_WIDTH, BUTTON_HEIGHT);
            if (Widgets.ButtonText(applyRect, "✅ Применить"))
            {
                if (_selectedStoryteller != null)
                {
                    RimWatchCore.ChangeStoryteller(_selectedStoryteller);
                    RimWatchLogger.Info($"StorytellerSelectionPanel: Applied storyteller {_selectedStoryteller.Name}");
                    // Close window and apply changes
                }
            }

            // Info text (center)
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = COLOR_TEXT_DESC;
            Rect infoRect = new Rect(rect.x + PADDING + BUTTON_WIDTH + 20f, buttonY, rect.width - (PADDING + BUTTON_WIDTH + 20f) * 2f, BUTTON_HEIGHT);
            Widgets.Label(infoRect, $"Выбран: {_selectedStoryteller?.Name ?? "Нет"}");
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }
    }

    /// <summary>
    /// Storyteller personality data structure.
    /// </summary>
    public class StorytellerPersonality
    {
        public float RiskTolerance { get; set; }        // 0.0-1.0
        public float BuildingSpeed { get; set; }        // 0.0-1.0
        public float TradeAggressiveness { get; set; }  // 0.0-1.0
        public float DefenseStyle { get; set; }         // 0.0-1.0
        public float ResearchPriority { get; set; }     // 0.0-1.0
        public float SocialFocus { get; set; }          // 0.0-1.0
    }
}

