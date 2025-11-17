using RimWatch.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Verse;

namespace RimWatch.Utils
{
    /// <summary>
    /// Structured JSON logging for AI decision-making.
    /// v0.9.0: Now supports ALL decision types through integration with RimWatchLogger.LogDecision().
    /// 
    /// Decision types logged:
    /// - building_placement: Building and room placement decisions
    /// - work_prioritization: Work priority changes (via RimWatchLogger.LogDecision)
    /// - farming_management: Taming, planting, slaughter decisions (via RimWatchLogger.LogDecision)
    /// - defense_positioning: Combat positioning, drafting (via RimWatchLogger.LogDecision)
    /// - medical_triage: Emergency medical prioritization (via RimWatchLogger.LogDecision)
    /// - construction_planning: Builder assignments (via RimWatchLogger.LogDecision)
    /// - production_management: Bill creation and management (via RimWatchLogger.LogDecision)
    /// 
    /// All automation systems use RimWatchLogger.LogDecision() which can optionally write to JSON.
    /// </summary>
    public static class DecisionLogger
    {
        private static string? _logFilePath;
        private static bool _loggingEnabled = false;
        private static bool _hasEntries = false;
        private static List<string> _pendingEntries = new List<string>();
        private static readonly object _logLock = new object();
        
        private const int FlushThreshold = 10; // Auto-flush after N entries
        
        /// <summary>
        /// Enable or disable decision logging.
        /// </summary>
        public static bool IsEnabled
        {
            get => _loggingEnabled;
            set
            {
                _loggingEnabled = value;
                if (value && _logFilePath == null)
                {
                    InitializeLogFile();
                }
            }
        }
        
        /// <summary>
        /// Initialize the decision log file.
        /// </summary>
        private static void InitializeLogFile()
        {
            try
            {
                // Use same directory as RimWatchLogger
                string logsDir = Path.Combine(GenFilePaths.SaveDataFolderPath, "RimWatch_Logs");
                
                if (!Directory.Exists(logsDir))
                {
                    Directory.CreateDirectory(logsDir);
                }
                
                // Use timestamp to create a new file each session
                string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                _logFilePath = Path.Combine(logsDir, $"decisions_{stamp}.json");
                
                // Initialize with JSON array start
                File.WriteAllText(_logFilePath, "[\n", Encoding.UTF8);
                _hasEntries = false;
                
                RimWatchLogger.Info($"DecisionLogger: Initialized at {_logFilePath}");
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("DecisionLogger: Failed to initialize", ex);
                _loggingEnabled = false;
            }
        }
        
        /// <summary>
        /// Logs a building placement decision with all candidates.
        /// </summary>
        public static void LogBuildingDecision(
            string buildingDefName,
            List<CandidateLocation> candidates,
            CandidateLocation? chosen,
            int rejectedCount,
            long searchTimeMs)
        {
            if (!_loggingEnabled) return;
            
            try
            {
                var entry = new
                {
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    decision_type = "building_placement",
                    building = buildingDefName,
                    candidates = candidates.Select(c => new
                    {
                        pos = new[] { c.Position.x, c.Position.z },
                        score = Math.Round(c.Score, 2),
                        reasons = c.Reasons
                    }).ToList(),
                    chosen = chosen != null ? new
                    {
                        pos = new[] { chosen.Position.x, chosen.Position.z },
                        score = Math.Round(chosen.Score, 2),
                        reasons = chosen.Reasons
                    } : null,
                    rejection_count = rejectedCount,
                    search_time_ms = searchTimeMs
                };
                
                string json = SimpleJson.Serialize(entry);
                QueueEntry(json);
                
                RimWatchLogger.Debug($"DecisionLogger: Logged placement for {buildingDefName} " +
                                    $"({candidates.Count} candidates, {rejectedCount} rejected)");
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("DecisionLogger: Failed to log building decision", ex);
            }
        }
        
        /// <summary>
        /// Logs a generic AI decision for any RimWatch system.
        /// Used by RimWatchLogger.LogDecision to export decisions to JSON.
        /// </summary>
        public static void LogGenericDecision(
            string decisionType,
            string system,
            string decision,
            System.Collections.Generic.Dictionary<string, object>? context = null)
        {
            if (!_loggingEnabled) return;
            
            try
            {
                var entry = new
                {
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    decision_type = decisionType,
                    system = system,
                    decision = decision,
                    context = context != null
                        ? context.Select(kv => new
                        {
                            key = kv.Key,
                            value = kv.Value != null ? kv.Value.ToString() ?? "null" : "null"
                        }).ToList()
                        : null
                };
                
                string json = SimpleJson.Serialize(entry);
                QueueEntry(json);
                
                RimWatchLogger.Debug($"DecisionLogger: Logged decision {decisionType} ({system}.{decision})");
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("DecisionLogger: Failed to log generic decision", ex);
            }
        }
        
        /// <summary>
        /// Logs a zone cache update.
        /// </summary>
        public static void LogZoneUpdate(
            IntVec3 baseCenter,
            int livingZoneSize,
            int workshopZoneSize,
            int farmZoneSize)
        {
            if (!_loggingEnabled) return;
            
            try
            {
                var entry = new
                {
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    decision_type = "zone_cache_update",
                    base_center = new[] { baseCenter.x, baseCenter.z },
                    zones = new
                    {
                        living = livingZoneSize,
                        workshop = workshopZoneSize,
                        farm = farmZoneSize
                    }
                };
                
                string json = SimpleJson.Serialize(entry);
                QueueEntry(json);
                
                RimWatchLogger.Debug($"DecisionLogger: Logged zone update");
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("DecisionLogger: Failed to log zone update", ex);
            }
        }
        
        /// <summary>
        /// Queue an entry for writing (batch for performance).
        /// </summary>
        private static void QueueEntry(string json)
        {
            lock (_logLock)
            {
                _pendingEntries.Add(json);
                
                if (_pendingEntries.Count >= FlushThreshold)
                {
                    FlushToFile();
                }
            }
        }
        
        /// <summary>
        /// Flush all pending entries to file.
        /// v0.8.5: Fixed to ensure valid JSON format (no leading comma).
        /// </summary>
        public static void FlushToFile()
        {
            if (!_loggingEnabled || string.IsNullOrEmpty(_logFilePath)) return;
            
            lock (_logLock)
            {
                if (_pendingEntries.Count == 0) return;
                
                try
                {
                    StringBuilder sb = new StringBuilder();
                    
                    for (int i = 0; i < _pendingEntries.Count; i++)
                    {
                        // v0.8.5: Add comma ONLY if this is not the very first entry in the entire file
                        // AND not the first entry in current batch if file already has entries
                        if (_hasEntries || i > 0)
                        {
                            sb.Append(",\n");
                        }
                        
                        sb.Append(_pendingEntries[i]);
                        _hasEntries = true; // Mark that file now has at least one entry
                    }
                    
                    File.AppendAllText(_logFilePath, sb.ToString(), Encoding.UTF8);
                    
                    RimWatchLogger.Debug($"DecisionLogger: Flushed {_pendingEntries.Count} entries to file");
                    _pendingEntries.Clear();
                }
                catch (Exception ex)
                {
                    RimWatchLogger.Error("DecisionLogger: Failed to flush to file", ex);
                }
            }
        }
        
        /// <summary>
        /// Close the log file (call on game exit).
        /// </summary>
        public static void CloseLogFile()
        {
            if (!_loggingEnabled || string.IsNullOrEmpty(_logFilePath)) return;
            
            try
            {
                FlushToFile();
                
                // Close JSON array
                File.AppendAllText(_logFilePath, "\n]", Encoding.UTF8);
                
                RimWatchLogger.Info("DecisionLogger: Closed log file");
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("DecisionLogger: Failed to close log file", ex);
            }
        }
        
        /// <summary>
        /// Get the current log file path.
        /// </summary>
        public static string? GetLogFilePath()
        {
            return _logFilePath;
        }
    }
    
    /// <summary>
    /// Represents a candidate location for building placement.
    /// </summary>
    public class CandidateLocation
    {
        public IntVec3 Position { get; set; }
        public float Score { get; set; }
        public List<string> Reasons { get; set; } = new List<string>();
    }
    
    /// <summary>
    /// Simple JSON serializer (no external dependencies).
    /// </summary>
    public static class SimpleJson
    {
        public static string Serialize(object obj)
        {
            if (obj == null) return "null";
            
            var type = obj.GetType();
            
            // Handle primitives
            if (type == typeof(string))
                return $"\"{EscapeString((string)obj)}\"";
            if (type == typeof(int) || type == typeof(long) || type == typeof(float) || type == typeof(double))
                return obj.ToString();
            if (type == typeof(bool))
                return obj.ToString().ToLower();
            
            // Handle arrays
            if (obj is System.Collections.IEnumerable && !(obj is string))
            {
                var items = new List<string>();
                foreach (var item in (System.Collections.IEnumerable)obj)
                {
                    items.Add(Serialize(item));
                }
                return $"[{string.Join(", ", items)}]";
            }
            
            // Handle anonymous objects
            var properties = type.GetProperties();
            var fields = type.GetFields();
            
            var members = new List<string>();
            
            foreach (var prop in properties)
            {
                var value = prop.GetValue(obj, null);
                members.Add($"\"{prop.Name}\": {Serialize(value)}");
            }
            
            foreach (var field in fields)
            {
                var value = field.GetValue(obj);
                members.Add($"\"{field.Name}\": {Serialize(value)}");
            }
            
            return $"{{{string.Join(", ", members)}}}";
        }
        
        private static string EscapeString(string str)
        {
            if (str == null) return "";
            return str.Replace("\\", "\\\\")
                      .Replace("\"", "\\\"")
                      .Replace("\n", "\\n")
                      .Replace("\r", "\\r")
                      .Replace("\t", "\\t");
        }
    }
}

