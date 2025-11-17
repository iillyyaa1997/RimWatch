using System;
using System.Collections.Generic;
using System.Linq;
using RimWatch.Utils;

namespace RimWatch.Settings
{
    /// <summary>
    /// Represents a single setting node in the hierarchical settings tree.
    /// </summary>
    public class SettingNode
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Enabled { get; set; }
        public int Level { get; set; } // 1, 2, or 3
        public string ParentId { get; set; }
        public List<SettingNode> Children { get; set; }
        public Action<bool> OnToggle { get; set; }

        public SettingNode()
        {
            Children = new List<SettingNode>();
        }

        public SettingNode(string id, string name, string description, int level, string? parentId = null)
        {
            Id = id;
            Name = name;
            Description = description;
            Level = level;
            ParentId = parentId;
            Enabled = true;
            Children = new List<SettingNode>();
        }
    }

    /// <summary>
    /// Manages hierarchical settings tree with automatic parent-child relationships.
    /// When a parent is disabled, all children are disabled.
    /// When a child is enabled, all parents are enabled.
    /// </summary>
    public class SettingsTree
    {
        private Dictionary<string, SettingNode> _nodes;
        private List<SettingNode> _rootNodes;

        public SettingsTree()
        {
            _nodes = new Dictionary<string, SettingNode>();
            _rootNodes = new List<SettingNode>();
        }

        /// <summary>
        /// Adds a node to the tree.
        /// </summary>
        public void AddNode(SettingNode node)
        {
            if (_nodes.ContainsKey(node.Id))
            {
                RimWatchLogger.Warning($"SettingsTree: Node {node.Id} already exists, replacing");
                _nodes[node.Id] = node;
            }
            else
            {
                _nodes[node.Id] = node;
            }

            // If has parent, add to parent's children
            if (!string.IsNullOrEmpty(node.ParentId))
            {
                if (_nodes.TryGetValue(node.ParentId, out SettingNode parent))
                {
                    if (!parent.Children.Contains(node))
                    {
                        parent.Children.Add(node);
                    }
                }
                else
                {
                    RimWatchLogger.Warning($"SettingsTree: Parent {node.ParentId} not found for node {node.Id}");
                }
            }
            else
            {
                // Root node (level 1)
                if (!_rootNodes.Contains(node))
                {
                    _rootNodes.Add(node);
                }
            }
        }

        /// <summary>
        /// Gets a node by ID.
        /// </summary>
        public SettingNode GetNode(string id)
        {
            _nodes.TryGetValue(id, out SettingNode node);
            return node;
        }

        /// <summary>
        /// Gets all root nodes (level 1).
        /// </summary>
        public List<SettingNode> GetRootNodes()
        {
            return _rootNodes;
        }

        /// <summary>
        /// Gets all nodes at a specific level.
        /// </summary>
        public List<SettingNode> GetNodesByLevel(int level)
        {
            return _nodes.Values.Where(n => n.Level == level).ToList();
        }

        /// <summary>
        /// Gets direct children of a node.
        /// </summary>
        public List<SettingNode> GetChildren(string parentId)
        {
            if (_nodes.TryGetValue(parentId, out SettingNode parent))
            {
                return parent.Children;
            }
            return new List<SettingNode>();
        }

        /// <summary>
        /// Enables a node and all its parents up the tree (ensures parent hierarchy is consistent).
        /// Also enables all children down the tree.
        /// DOES NOT trigger callbacks - use SetNodeEnabled for that.
        /// </summary>
        private void EnableNodeInternal(string id)
        {
            if (!_nodes.TryGetValue(id, out SettingNode node))
            {
                RimWatchLogger.Warning($"SettingsTree: Node {id} not found for enabling");
                return;
            }

            // 🛑 CRITICAL: Skip if already enabled to prevent infinite recursion
            if (node.Enabled)
            {
                return;
            }

            // Enable this node
            node.Enabled = true;
            RimWatchLogger.Debug($"SettingsTree: Enabled {node.Id} ({node.Name})");

            // ✅ Enable all parents recursively (child can't be on if parent is off)
            if (!string.IsNullOrEmpty(node.ParentId))
            {
                EnableNodeInternal(node.ParentId);
            }

            // ✅ Enable all children recursively (when parent is enabled, enable children too)
            var children = new List<SettingNode>(node.Children);
            foreach (var child in children)
            {
                EnableNodeInternal(child.Id);
            }
        }

        /// <summary>
        /// Disables a node and all its children down the tree (ensures consistency).
        /// DOES NOT trigger callbacks - use SetNodeEnabled for that.
        /// </summary>
        private void DisableNodeInternal(string id)
        {
            if (!_nodes.TryGetValue(id, out SettingNode node))
            {
                RimWatchLogger.Warning($"SettingsTree: Node {id} not found for disabling");
                return;
            }

            // 🛑 CRITICAL: Skip if already disabled to prevent redundant recursion
            if (!node.Enabled)
            {
                return;
            }

            // Disable this node
            node.Enabled = false;
            RimWatchLogger.Debug($"SettingsTree: Disabled {node.Id} ({node.Name})");

            // ✅ CORRECT: Disable all children recursively (parent off → children must be off)
            // Create a copy to avoid "Collection was modified" error
            var children = new List<SettingNode>(node.Children);
            foreach (var child in children)
            {
                DisableNodeInternal(child.Id);
            }
        }

        /// <summary>
        /// Sets the enabled state of a node with automatic parent-child logic.
        /// This is the PUBLIC method that should be used - it triggers all callbacks.
        /// Returns the list of all affected nodes for further processing.
        /// </summary>
        public List<SettingNode> SetNodeEnabled(string id, bool enabled)
        {
            // Step 1: Collect nodes BEFORE change to track what actually changed
            var beforeStates = new Dictionary<string, bool>();
            CollectAllNodeStates(_nodes.Values, beforeStates);

            // Step 2: Update tree structure (no callbacks)
            if (enabled)
            {
                EnableNodeInternal(id);
            }
            else
            {
                DisableNodeInternal(id);
            }

            // Step 3: Collect nodes that changed OR were part of cascading update
            var affectedNodes = new List<SettingNode>();
            
            // Add the starting node
            if (_nodes.TryGetValue(id, out SettingNode startNode))
            {
                affectedNodes.Add(startNode);
                
                // Always collect parents and children (both can be affected by cascade)
                CollectParents(startNode, affectedNodes);
                CollectChildren(startNode, affectedNodes);
            }

            // Step 4: Trigger OnToggle for all affected nodes
            foreach (var node in affectedNodes)
            {
                bool oldState = beforeStates.TryGetValue(node.Id, out bool state) ? state : node.Enabled;
                if (node.Enabled != oldState)
                {
                    RimWatchLogger.Debug($"SettingsTree: State changed for {node.Id}: {oldState} → {node.Enabled}");
                }
                node.OnToggle?.Invoke(node.Enabled);
            }
            
            return affectedNodes;
        }

        /// <summary>
        /// Collects all parent nodes recursively.
        /// </summary>
        private void CollectParents(SettingNode node, List<SettingNode> result)
        {
            if (!string.IsNullOrEmpty(node.ParentId))
            {
                if (_nodes.TryGetValue(node.ParentId, out SettingNode parent))
                {
                    if (!result.Contains(parent))
                    {
                        result.Add(parent);
                        CollectParents(parent, result);
                    }
                }
            }
        }

        /// <summary>
        /// Collects all child nodes recursively.
        /// </summary>
        private void CollectChildren(SettingNode node, List<SettingNode> result)
        {
            foreach (var child in node.Children)
            {
                if (!result.Contains(child))
                {
                    result.Add(child);
                    CollectChildren(child, result);
                }
            }
        }

        /// <summary>
        /// Collects current state of all nodes.
        /// </summary>
        private void CollectAllNodeStates(IEnumerable<SettingNode> nodes, Dictionary<string, bool> result)
        {
            foreach (var node in nodes)
            {
                if (!result.ContainsKey(node.Id))
                {
                    result[node.Id] = node.Enabled;
                }
            }
        }

        /// <summary>
        /// Gets all nodes in the tree.
        /// </summary>
        public IEnumerable<SettingNode> GetAllNodes()
        {
            return _nodes.Values;
        }

        /// <summary>
        /// Clears all nodes from the tree.
        /// </summary>
        public void Clear()
        {
            _nodes.Clear();
            _rootNodes.Clear();
        }

        /// <summary>
        /// Logs the entire tree structure for debugging.
        /// </summary>
        public void LogTreeStructure()
        {
            RimWatchLogger.Info("=== Settings Tree Structure ===");
            foreach (var root in _rootNodes)
            {
                LogNodeRecursive(root, 0);
            }
            RimWatchLogger.Info("===============================");
        }

        private void LogNodeRecursive(SettingNode node, int indent)
        {
            string indentStr = new string(' ', indent * 2);
            string status = node.Enabled ? "✓" : "✗";
            RimWatchLogger.Info($"{indentStr}{status} [{node.Level}] {node.Name} ({node.Id})");
            
            foreach (var child in node.Children)
            {
                LogNodeRecursive(child, indent + 1);
            }
        }
    }
}

