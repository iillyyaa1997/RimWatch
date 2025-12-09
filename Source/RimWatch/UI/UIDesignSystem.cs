using UnityEngine;
using Verse;

namespace RimWatch.UI
{
    /// <summary>
    /// Unified Design System for all RimWatch UI components.
    /// Single source of truth for colors, spacing, fonts, and visual styles.
    /// v1.3.1: Clean, minimalist design with subtle colors and clear hierarchy.
    /// </summary>
    public static class UIDesignSystem
    {
        // ═══════════════════════════════════════════════════════════════
        // 🎨 COLOR PALETTE - Minimalist & Clean
        // ═══════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Neutral gray backgrounds - subtle and unobtrusive
        /// </summary>
        public static readonly Color BG_Dark = new Color(0.12f, 0.12f, 0.12f, 0.92f);      // Main backgrounds
        public static readonly Color BG_Medium = new Color(0.18f, 0.18f, 0.18f, 0.88f);   // Secondary panels
        public static readonly Color BG_Light = new Color(0.24f, 0.24f, 0.24f, 0.85f);    // Hover states
        public static readonly Color BG_Subtle = new Color(0.14f, 0.14f, 0.14f, 0.4f);    // Very subtle backgrounds
        
        /// <summary>
        /// Accent colors - minimal and purposeful
        /// </summary>
        public static readonly Color Accent_Blue = new Color(0.4f, 0.65f, 0.92f, 0.9f);   // Primary actions
        public static readonly Color Accent_Green = new Color(0.45f, 0.82f, 0.55f, 0.9f); // Success/enabled
        public static readonly Color Accent_Orange = new Color(0.95f, 0.75f, 0.42f, 0.9f);// Warnings
        public static readonly Color Accent_Red = new Color(0.92f, 0.45f, 0.42f, 0.9f);   // Errors/disabled
        public static readonly Color Accent_Purple = new Color(0.72f, 0.55f, 0.92f, 0.9f);// Special features
        
        /// <summary>
        /// Text colors - clear hierarchy
        /// </summary>
        public static readonly Color Text_Primary = new Color(0.95f, 0.95f, 0.95f, 1f);   // Main text
        public static readonly Color Text_Secondary = new Color(0.7f, 0.7f, 0.7f, 1f);    // Descriptions
        public static readonly Color Text_Muted = new Color(0.5f, 0.5f, 0.5f, 1f);        // Hints
        public static readonly Color Text_Disabled = new Color(0.35f, 0.35f, 0.35f, 1f);  // Disabled
        
        /// <summary>
        /// Status indicators - clear meaning
        /// </summary>
        public static readonly Color Status_Active = new Color(0.45f, 0.82f, 0.55f, 1f);  // Green - active/good
        public static readonly Color Status_Inactive = new Color(0.5f, 0.5f, 0.5f, 1f);   // Gray - inactive
        public static readonly Color Status_Warning = new Color(0.95f, 0.75f, 0.42f, 1f); // Orange - caution
        public static readonly Color Status_Error = new Color(0.92f, 0.45f, 0.42f, 1f);   // Red - error
        
        /// <summary>
        /// Border colors
        /// </summary>
        public static readonly Color Border_Default = new Color(0.3f, 0.3f, 0.3f, 0.6f);
        public static readonly Color Border_Highlight = new Color(0.5f, 0.5f, 0.5f, 0.8f);
        public static readonly Color Border_Active = Accent_Blue;
        
        // ═══════════════════════════════════════════════════════════════
        // 📏 SPACING & SIZING - Consistent measurements
        // ═══════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Standard spacing units (8px grid system)
        /// </summary>
        public const float SPACE_XXS = 2f;   // Tiny gap
        public const float SPACE_XS = 4f;    // Small gap
        public const float SPACE_SM = 8f;    // Default gap
        public const float SPACE_MD = 12f;   // Medium gap
        public const float SPACE_LG = 16f;   // Large gap
        public const float SPACE_XL = 24f;   // Extra large gap
        public const float SPACE_XXL = 32f;  // Section spacing
        
        /// <summary>
        /// UI element heights
        /// </summary>
        public const float HEIGHT_Button = 30f;         // Standard button
        public const float HEIGHT_ButtonSmall = 24f;    // Compact button
        public const float HEIGHT_ButtonLarge = 40f;    // Prominent button
        public const float HEIGHT_Checkbox = 28f;       // Checkbox with label
        public const float HEIGHT_Header = 36f;         // Section header
        public const float HEIGHT_HeaderLarge = 48f;    // Main header
        public const float HEIGHT_Tab = 36f;            // Tab button
        public const float HEIGHT_Card = 80f;           // Info card
        public const float HEIGHT_CardLarge = 120f;     // Large info card
        
        /// <summary>
        /// Border thickness
        /// </summary>
        public const int BORDER_Thin = 1;
        public const int BORDER_Medium = 2;
        public const int BORDER_Thick = 3;
        
        /// <summary>
        /// Corner rounding (not used in RimWorld, but for reference)
        /// </summary>
        public const float CORNER_Radius = 4f;
        
        // ═══════════════════════════════════════════════════════════════
        // ✏️ TYPOGRAPHY - Font settings
        // ═══════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Text anchor presets (reset after use!)
        /// </summary>
        public static readonly TextAnchor Anchor_Left = TextAnchor.UpperLeft;
        public static readonly TextAnchor Anchor_Center = TextAnchor.MiddleCenter;
        public static readonly TextAnchor Anchor_Right = TextAnchor.UpperRight;
        
        // ═══════════════════════════════════════════════════════════════
        // 🎯 DRAWING HELPERS - Reusable UI components
        // ═══════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Draws a clean card background with border
        /// </summary>
        public static void DrawCard(Rect rect, Color? bgColor = null, bool drawBorder = true)
        {
            Color bg = bgColor ?? BG_Medium;
            Widgets.DrawBoxSolid(rect, bg);
            
            if (drawBorder)
            {
                Widgets.DrawBox(rect, BORDER_Thin);
            }
        }
        
        /// <summary>
        /// Draws a subtle section background
        /// </summary>
        public static void DrawSection(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, BG_Subtle);
        }
        
        /// <summary>
        /// Draws a header with background and centered text
        /// </summary>
        public static void DrawHeader(Rect rect, string label, GameFont font = GameFont.Medium)
        {
            DrawCard(rect, BG_Dark);
            
            Text.Font = font;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = Text_Primary;
            Widgets.Label(rect, label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }
        
        /// <summary>
        /// Draws a collapsible section header with arrow
        /// </summary>
        public static bool DrawCollapsibleHeader(Rect rect, string label, bool isCollapsed)
        {
            DrawCard(rect, BG_Dark);
            
            // Arrow
            Rect arrowRect = new Rect(rect.x + SPACE_SM, rect.y + (rect.height - 20f) / 2f, 20f, 20f);
            Text.Font = GameFont.Medium;
            GUI.color = Text_Secondary;
            Widgets.Label(arrowRect, isCollapsed ? "▶" : "▼");
            GUI.color = Color.white;
            
            // Label
            Rect labelRect = new Rect(rect.x + SPACE_SM + 24f, rect.y, rect.width - SPACE_SM - 24f, rect.height);
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.Font = GameFont.Small;
            GUI.color = Text_Primary;
            Widgets.Label(labelRect, label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            
            // Click handler
            return Widgets.ButtonInvisible(rect);
        }
        
        /// <summary>
        /// Draws a status indicator dot
        /// </summary>
        public static void DrawStatusDot(Rect rect, bool isActive)
        {
            GUI.color = isActive ? Status_Active : Status_Inactive;
            Widgets.Label(rect, isActive ? "●" : "○");
            GUI.color = Color.white;
        }
        
        /// <summary>
        /// Draws a button with consistent styling
        /// </summary>
        public static bool DrawButton(Rect rect, string label, Color? bgColor = null)
        {
            Color bg = bgColor ?? Accent_Blue;
            
            if (Mouse.IsOver(rect))
            {
                bg = bg * 1.2f; // Lighten on hover
            }
            
            Widgets.DrawBoxSolid(rect, bg);
            Widgets.DrawBox(rect, BORDER_Thin);
            
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = Text_Primary;
            Widgets.Label(rect, label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            
            return Widgets.ButtonInvisible(rect);
        }
        
        /// <summary>
        /// Draws a tab button (active/inactive states)
        /// </summary>
        public static bool DrawTab(Rect rect, string label, bool isActive)
        {
            Color bg = isActive ? Accent_Blue : BG_Medium;
            
            Widgets.DrawBoxSolid(rect, bg);
            
            if (isActive)
            {
                Widgets.DrawBox(rect, BORDER_Medium);
            }
            
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = isActive ? GameFont.Small : GameFont.Tiny;
            GUI.color = isActive ? Text_Primary : Text_Secondary;
            Widgets.Label(rect, label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            
            return Widgets.ButtonInvisible(rect);
        }
        
        /// <summary>
        /// Draws a horizontal line separator
        /// </summary>
        public static void DrawSeparator(float x, float y, float width)
        {
            Widgets.DrawLineHorizontal(x, y, width);
        }
        
        /// <summary>
        /// Draws a labeled value (key: value pair)
        /// </summary>
        public static void DrawLabeledValue(Rect rect, string label, string value)
        {
            Rect labelRect = new Rect(rect.x, rect.y, rect.width * 0.5f, rect.height);
            Rect valueRect = new Rect(rect.x + rect.width * 0.5f, rect.y, rect.width * 0.5f, rect.height);
            
            GUI.color = Text_Secondary;
            Widgets.Label(labelRect, label);
            
            GUI.color = Text_Primary;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(valueRect, value);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }
        
        /// <summary>
        /// Draws a simple info box with icon and text
        /// </summary>
        public static void DrawInfoBox(Rect rect, string icon, string text, Color? color = null)
        {
            DrawCard(rect, color);
            
            // Icon
            Rect iconRect = new Rect(rect.x + SPACE_MD, rect.y + SPACE_MD, 24f, 24f);
            Text.Font = GameFont.Medium;
            Widgets.Label(iconRect, icon);
            
            // Text
            Rect textRect = new Rect(rect.x + SPACE_MD + 32f, rect.y + SPACE_SM, 
                rect.width - SPACE_MD - 32f - SPACE_MD, rect.height - SPACE_SM * 2);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = Text_Primary;
            Widgets.Label(textRect, text);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }
        
        /// <summary>
        /// Resets all text/GUI state to defaults (call after custom rendering)
        /// </summary>
        public static void ResetTextState()
        {
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }
    }
}
