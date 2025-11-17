using RimWatch.Settings;
using RimWatch.Utils;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimWatch.UI
{
    /// <summary>
    /// Hierarchical settings UI renderer with 3-level tree structure.
    /// Supports collapsible sections and automatic parent-child relationships.
    /// </summary>
    public static class HierarchicalSettingsUI
    {
        private static Dictionary<string, bool> _expandedNodes = new Dictionary<string, bool>();
        
        // Minimalist color palette - simple and clean
        private static readonly Color Level1Color = new Color(0.2f, 0.2f, 0.2f, 0.3f);
        private static readonly Color Level2Color = new Color(0.15f, 0.15f, 0.15f, 0.2f);
        private static readonly Color Level3Color = new Color(0.1f, 0.1f, 0.1f, 0.1f);
        
        private static readonly Color EnabledTextColor = Color.white;
        private static readonly Color DisabledTextColor = new Color(0.5f, 0.5f, 0.5f);

        /// <summary>
        /// Draws the hierarchical settings tree.
        /// </summary>
        public static void DrawSettingsTree(Rect rect, SettingsTree tree, RimWatchSettings settings)
        {
            if (tree == null)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rect, "RimWatch.UI.TreeNotInitialized".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(rect);

            // Draw control buttons
            Rect buttonRow = listing.GetRect(30f);
            Rect expandAllRect = new Rect(buttonRow.x, buttonRow.y, 120f, 28f);
            Rect collapseAllRect = new Rect(buttonRow.x + 125f, buttonRow.y, 120f, 28f);
            
            if (Widgets.ButtonText(expandAllRect, "RimWatch.UI.ExpandAll".Translate()))
            {
                ExpandAll(tree);
            }
            
            if (Widgets.ButtonText(collapseAllRect, "RimWatch.UI.CollapseAll".Translate()))
            {
                CollapseAll();
            }
            
            listing.Gap(6f);
            Widgets.DrawLineHorizontal(0f, listing.CurHeight, rect.width);
            listing.Gap(6f);

            // Draw root nodes (Level 1)
            foreach (var rootNode in tree.GetRootNodes())
            {
                DrawSettingNode(listing, tree, rootNode, 0, settings);
            }

            listing.End();
        }

        /// <summary>
        /// Recursively draws a setting node and its children.
        /// </summary>
        private static void DrawSettingNode(Listing_Standard listing, SettingsTree tree, SettingNode node, int indent, RimWatchSettings settings)
        {
            if (node == null) return;

            float indentSize = 20f;
            float nodeHeight = 30f;
            float toggleWidth = 24f;

            // Get rect for this node
            Rect nodeRect = listing.GetRect(nodeHeight);
            Rect originalRect = nodeRect;

            // Apply indent
            nodeRect.x += indent * indentSize;
            nodeRect.width -= indent * indentSize;

            // Simple flat background
            Color bgColor = node.Level == 1 ? Level1Color : (node.Level == 2 ? Level2Color : Level3Color);
            Widgets.DrawBoxSolid(originalRect, bgColor);
            
            // Simple line for level 1
            if (node.Level == 1)
            {
                Widgets.DrawLineHorizontal(originalRect.x, originalRect.yMax - 1, originalRect.width);
            }

            // Split rect into parts
            Rect expandRect = nodeRect;
            expandRect.width = 20f;

            Rect checkboxRect = nodeRect;
            checkboxRect.x += 25f;
            checkboxRect.width = toggleWidth;

            Rect labelRect = nodeRect;
            labelRect.x += 50f;
            labelRect.width -= 50f;

            // Draw expand/collapse arrow if has children
            bool hasChildren = node.Children != null && node.Children.Count > 0;
            if (hasChildren)
            {
                bool isExpanded = IsNodeExpanded(node.Id);
                
                string arrow = isExpanded ? "▼" : "▶";
                Text.Font = GameFont.Tiny;
                GUI.color = node.Enabled ? EnabledTextColor : DisabledTextColor;
                Widgets.Label(expandRect, arrow);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                
                if (Widgets.ButtonInvisible(expandRect))
                {
                    ToggleNodeExpansion(node.Id);
                }
            }

            // Draw checkbox with custom colors
            bool oldEnabled = node.Enabled;
            bool newEnabled = oldEnabled;
            
            Color oldColor = GUI.color;
            GUI.color = node.Enabled ? EnabledTextColor : DisabledTextColor;
            Widgets.Checkbox(checkboxRect.position, ref newEnabled, toggleWidth, false, true);
            GUI.color = oldColor;

            if (newEnabled != oldEnabled)
            {
                // Toggle in tree (automatically handles parent-child logic and updates flat values via OnToggle)
                var affectedNodes = tree.SetNodeEnabled(node.Id, newEnabled);
                
                // ✅ OPTIMIZED: Save settings ONCE after all flat values are updated
                settings.ApplyToCore();
                settings.Write();
                
                RimWatchLogger.Info($"[SettingsUI] Toggled {node.Name}: {oldEnabled} → {newEnabled} (affected {affectedNodes.Count} nodes)");
            }

            // Simple label - no fancy styling
            Text.Font = node.Level == 1 ? GameFont.Small : GameFont.Tiny;
            GUI.color = node.Enabled ? EnabledTextColor : DisabledTextColor;
            
            string labelText = node.Name;
            
            if (node.Level == 2)
            {
                labelText = "  " + labelText;
            }
            else if (node.Level == 3)
            {
                labelText = "    " + labelText;
            }
            
            Widgets.Label(labelRect, labelText);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            // Tooltip
            if (Mouse.IsOver(originalRect))
            {
                string tooltip = $"{node.Name}\n\n{node.Description}";
                if (!string.IsNullOrEmpty(node.ParentId))
                {
                    tooltip += "\n\n" + "RimWatch.UI.TooltipParent".Translate(node.ParentId);
                }
                if (hasChildren)
                {
                    tooltip += "\n\n" + "RimWatch.UI.TooltipChildren".Translate(node.Children.Count);
                }
                TooltipHandler.TipRegion(originalRect, tooltip);
            }

            // Draw children if expanded and enabled (parent must be enabled to show children)
            if (hasChildren && IsNodeExpanded(node.Id))
            {
                foreach (var child in node.Children)
                {
                    DrawSettingNode(listing, tree, child, indent + 1, settings);
                }
            }
        }

        /// <summary>
        /// Checks if a node is expanded.
        /// </summary>
        private static bool IsNodeExpanded(string nodeId)
        {
            if (!_expandedNodes.ContainsKey(nodeId))
            {
                // Level 1 nodes are expanded by default
                _expandedNodes[nodeId] = true;
            }
            return _expandedNodes[nodeId];
        }

        /// <summary>
        /// Toggles node expansion state.
        /// </summary>
        private static void ToggleNodeExpansion(string nodeId)
        {
            if (_expandedNodes.ContainsKey(nodeId))
            {
                _expandedNodes[nodeId] = !_expandedNodes[nodeId];
            }
            else
            {
                _expandedNodes[nodeId] = false;
            }
        }

        /// <summary>
        /// Resets all expansion states (useful when reopening settings).
        /// </summary>
        public static void ResetExpansionStates()
        {
            _expandedNodes.Clear();
        }

        /// <summary>
        /// Expands all nodes in the tree.
        /// </summary>
        public static void ExpandAll(SettingsTree tree)
        {
            foreach (var node in tree.GetAllNodes())
            {
                _expandedNodes[node.Id] = true;
            }
        }

        /// <summary>
        /// Collapses all nodes in the tree.
        /// </summary>
        public static void CollapseAll()
        {
            // ✅ FIX: Create a copy of keys to avoid "Collection was modified" error
            var keys = new List<string>(_expandedNodes.Keys);
            foreach (var key in keys)
            {
                _expandedNodes[key] = false;
            }
        }
        
    }
}

