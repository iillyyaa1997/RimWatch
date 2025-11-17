using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Verse;

namespace RimWatch.Utils
{
    /// <summary>
    /// Centralized logging system for RimWatch.
    /// All log messages MUST be in English for international support and debugging.
    /// </summary>
    /// <remarks>
    /// DO NOT use localized/translated text in logs - logs are for developers, not users.
    /// For user-facing messages, use RimWorld's translation system.
    /// </remarks>
    public static class RimWatchLogger
    {
        private const string Prefix = "[RimWatch]";
        private static string? _logFilePath;
        private static bool _fileLoggingInitialized = false;
        private static readonly object _fileLock = new object();
        
        // Structured log categories (v0.8.3)
        public enum LogCategory
        {
            Decision,      // AI decision points
            State,         // State transitions
            Execution,     // Task execution start/end
            Performance,   // Performance metrics
            Failure        // Failures and errors
        }
        
        // Log levels for file logging
        public enum LogLevel
        {
            Debug,
            Info,
            Warning,
            Error
        }
        
        /// <summary>
        /// Enable or disable file logging
        /// </summary>
        public static bool FileLoggingEnabled { get; set; } = false;
        
        /// <summary>
        /// Enable or disable debug mode (shows debug logs)
        /// </summary>
        public static bool DebugModeEnabled { get; set; } = false;
        
        // v0.8.2: Warning throttling system to prevent log spam
        private static System.Collections.Generic.Dictionary<string, int> _lastWarningTick = new System.Collections.Generic.Dictionary<string, int>();
        private const int DefaultWarningCooldown = 3600; // 60 seconds (3600 ticks) default cooldown
        
        // v0.8.3: Failure pattern tracking to detect recurring issues
        private static readonly Dictionary<string, Dictionary<string, int>> _failurePatterns =
            new Dictionary<string, Dictionary<string, int>>();

        /// <summary>
        /// Initialize file logging system
        /// </summary>
        private static void InitializeFileLogging()
        {
            if (_fileLoggingInitialized) return;
            
            try
            {
                // Create logs directory in RimWorld's user data folder
                string logsDir = Path.Combine(GenFilePaths.SaveDataFolderPath, "RimWatch_Logs");
                
                if (!Directory.Exists(logsDir))
                {
                    Directory.CreateDirectory(logsDir);
                }
                
                // Create log file with timestamp
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                _logFilePath = Path.Combine(logsDir, $"RimWatch_{timestamp}.log");
                
                // Write header
                WriteToFile($"═══════════════════════════════════════════════════════════");
                WriteToFile($"RimWatch Log File");
                WriteToFile($"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                WriteToFile($"RimWorld Version: 1.6");
                WriteToFile($"═══════════════════════════════════════════════════════════");
                WriteToFile("");
                
                _fileLoggingInitialized = true;
                Log.Message($"{Prefix} File logging initialized: {_logFilePath}");
            }
            catch (Exception ex)
            {
                Log.Error($"{Prefix} Failed to initialize file logging: {ex.Message}");
                FileLoggingEnabled = false;
            }
        }
        
        /// <summary>
        /// Write message to log file
        /// </summary>
        private static void WriteToFile(string message)
        {
            if (!FileLoggingEnabled || string.IsNullOrEmpty(_logFilePath)) return;
            
            try
            {
                lock (_fileLock)
                {
                    File.AppendAllText(_logFilePath, message + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"{Prefix} Failed to write to log file: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Format log message with timestamp and level
        /// </summary>
        private static string FormatLogMessage(LogLevel level, string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            return $"[{timestamp}] [{level.ToString().ToUpper()}] {message}";
        }

        /// <summary>
        /// Format a structured context dictionary as key=value pairs.
        /// We intentionally keep this lightweight and human-readable, not strict JSON.
        /// </summary>
        private static string FormatContext(Dictionary<string, object>? context)
        {
            if (context == null || context.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder();
            bool first = true;
            foreach (var kv in context)
            {
                if (!first)
                {
                    sb.Append(", ");
                }
                first = false;

                string value = kv.Value != null ? kv.Value.ToString() ?? "null" : "null";
                sb.Append(kv.Key);
                sb.Append('=');
                sb.Append(value);
            }

            return sb.ToString();
        }

        public static void Info(string message)
        {
            Log.Message($"{Prefix} {message}");
            
            if (FileLoggingEnabled)
            {
                if (!_fileLoggingInitialized) InitializeFileLogging();
                WriteToFile(FormatLogMessage(LogLevel.Info, message));
            }
        }

        public static void Warning(string message)
        {
            Log.Warning($"{Prefix} {message}");
            
            if (FileLoggingEnabled)
            {
                if (!_fileLoggingInitialized) InitializeFileLogging();
                WriteToFile(FormatLogMessage(LogLevel.Warning, message));
            }
        }

        public static void Error(string message, Exception? ex = null)
        {
            string fullMessage = ex != null ? $"{message}\n{ex}" : message;
            Log.Error($"{Prefix} {fullMessage}");
            
            if (FileLoggingEnabled)
            {
                if (!_fileLoggingInitialized) InitializeFileLogging();
                WriteToFile(FormatLogMessage(LogLevel.Error, fullMessage));
            }
        }

        public static void Debug(string message)
        {
            // Show debug logs if either DevMode OR DebugMode is enabled
            if (Prefs.DevMode || DebugModeEnabled)
            {
                Log.Message($"{Prefix} [DEBUG] {message}");
            }
            
            // Always write to file if file logging is enabled
            if (FileLoggingEnabled)
            {
                if (!_fileLoggingInitialized) InitializeFileLogging();
                WriteToFile(FormatLogMessage(LogLevel.Debug, message));
            }
        }
        
        // ================== v0.8.3: Structured Logging Methods ==================

        /// <summary>
        /// Log an AI decision with structured context.
        /// Typically used for debugging and analysis; logged as DEBUG.
        /// </summary>
        public static void LogDecision(string system, string decision, Dictionary<string, object>? context = null)
        {
            string ctx = FormatContext(context);
            string msg = $"[DECISION] [{system}] {decision}" + (ctx.Length > 0 ? $": {ctx}" : string.Empty);
            Debug(msg);
            
            // Also export to JSON decision log if enabled
            try
            {
                if (DecisionLogger.IsEnabled)
                {
                    string decisionType = $"{system}.{decision}";
                    DecisionLogger.LogGenericDecision(decisionType, system, decision, context);
                }
            }
            catch (Exception ex)
            {
                // Never let JSON logging break normal logging flow
                Error("RimWatchLogger: Failed to forward decision to DecisionLogger", ex);
            }
        }

        /// <summary>
        /// Log a state transition with reason.
        /// Logged as INFO.
        /// </summary>
        public static void LogStateChange(string system, string oldState, string newState, string reason)
        {
            string msg = $"[STATE] [{system}] {oldState} → {newState} (reason: {reason})";
            Info(msg);
        }

        /// <summary>
        /// Log the start of an operation with parameters.
        /// Logged as DEBUG to avoid flooding normal logs.
        /// </summary>
        public static void LogExecutionStart(string system, string operation, Dictionary<string, object>? parameters = null)
        {
            string ctx = FormatContext(parameters);
            string msg = $"[EXEC START] [{system}.{operation}]" + (ctx.Length > 0 ? $": {ctx}" : string.Empty);
            Debug(msg);
        }

        /// <summary>
        /// Log the end of an operation with success flag and duration.
        /// Logged as INFO.
        /// </summary>
        public static void LogExecutionEnd(string system, string operation, bool success, long durationMs, string? details = null)
        {
            string status = success ? "SUCCESS" : "FAIL";
            string msg = $"[EXEC END] [{system}.{operation}] {status} in {durationMs} ms";
            if (!string.IsNullOrEmpty(details))
            {
                msg += $" - {details}";
            }
            Info(msg);
        }

        /// <summary>
        /// Log a performance entry if an operation is considered slow.
        /// Logged as WARNING to draw attention.
        /// </summary>
        public static void LogPerformance(string system, string operation, long durationMs, Dictionary<string, object>? metrics = null)
        {
            string ctx = FormatContext(metrics);
            string msg = $"[PERF] [{system}.{operation}] took {durationMs} ms";
            if (ctx.Length > 0)
            {
                msg += $" ({ctx})";
            }
            Warning(msg);
        }

        /// <summary>
        /// Log a failure with structured context and track recurring patterns.
        /// Logged as ERROR, with additional WARNING when patterns repeat often.
        /// </summary>
        public static void LogFailure(string system, string operation, string reason, Dictionary<string, object>? context = null)
        {
            string ctx = FormatContext(context);
            string msg = $"[FAILURE] [{system}.{operation}] {reason}";
            if (ctx.Length > 0)
            {
                msg += $" | Context: {ctx}";
            }
            Error(msg);
            TrackFailurePattern(system, operation, reason);
        }

        /// <summary>
        /// Track recurring failures by (system, operation, reason).
        /// When a threshold is hit, we emit a throttled warning.
        /// </summary>
        private static void TrackFailurePattern(string system, string operation, string reason)
        {
            try
            {
                string key = $"{system}.{operation}";
                if (!_failurePatterns.TryGetValue(key, out var opDict))
                {
                    opDict = new Dictionary<string, int>();
                    _failurePatterns[key] = opDict;
                }

                if (!opDict.ContainsKey(reason))
                {
                    opDict[reason] = 0;
                }

                opDict[reason]++;
                int count = opDict[reason];

                // Emit a warning when this specific failure happens often
                if (count == 5 || count == 10 || count == 25 || count == 50)
                {
                    string warnKey = $"failure_pattern:{key}:{reason}";
                    string message = $"⚠️ Recurring failure detected: {key} failed {count} times with reason: {reason}";
                    WarningThrottledByKey(warnKey, message, DefaultWarningCooldown);
                }
            }
            catch (Exception ex)
            {
                // Failure tracking must never crash logging; degrade gracefully.
                Log.Error($"{Prefix} Failure in TrackFailurePattern: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Get the current log file path
        /// </summary>
        public static string? GetLogFilePath()
        {
            return _logFilePath;
        }
        
        // ================== v0.8.2: Throttled Warning Methods ==================
        
        /// <summary>
        /// Logs a warning with throttling to prevent spam.
        /// Same message will only be logged once per cooldownTicks.
        /// </summary>
        /// <param name="message">Warning message to log</param>
        /// <param name="cooldownTicks">Cooldown in ticks (default: 3600 = 60 seconds)</param>
        public static void WarningThrottled(string message, int cooldownTicks = DefaultWarningCooldown)
        {
            // Generate unique key for this message
            string messageKey = message.GetHashCode().ToString();
            int currentTick = Find.TickManager.TicksGame;
            
            // Check if this message was recently logged
            if (_lastWarningTick.ContainsKey(messageKey))
            {
                int ticksSinceLastWarning = currentTick - _lastWarningTick[messageKey];
                if (ticksSinceLastWarning < cooldownTicks)
                {
                    // Skip logging - cooldown not expired
                    return;
                }
            }
            
            // Log the warning and update timestamp
            Warning(message);
            _lastWarningTick[messageKey] = currentTick;
        }
        
        /// <summary>
        /// Logs a warning with throttling using a custom key.
        /// Useful for grouping similar messages together.
        /// Example: WarningThrottledByKey("bedroom_deficit", "Colonists sleeping outside!", 3600)
        /// </summary>
        /// <param name="key">Custom key to group messages</param>
        /// <param name="message">Warning message to log</param>
        /// <param name="cooldownTicks">Cooldown in ticks (default: 3600 = 60 seconds)</param>
        public static void WarningThrottledByKey(string key, string message, int cooldownTicks = DefaultWarningCooldown)
        {
            int currentTick = Find.TickManager.TicksGame;
            
            // Check if this key was recently logged
            if (_lastWarningTick.ContainsKey(key))
            {
                int ticksSinceLastWarning = currentTick - _lastWarningTick[key];
                if (ticksSinceLastWarning < cooldownTicks)
                {
                    // Skip logging - cooldown not expired
                    return;
                }
            }
            
            // Log the warning and update timestamp
            Warning(message);
            _lastWarningTick[key] = currentTick;
        }
        
        /// <summary>
        /// Clears all throttled warning timers (for debugging).
        /// </summary>
        public static void ClearWarningThrottles()
        {
            int count = _lastWarningTick.Count;
            _lastWarningTick.Clear();
            Debug($"Cleared {count} warning throttle entries");
        }
        
        /// <summary>
        /// Open the logs directory in file explorer
        /// </summary>
        public static void OpenLogsDirectory()
        {
            try
            {
                string logsDir = Path.Combine(GenFilePaths.SaveDataFolderPath, "RimWatch_Logs");
                
                if (!Directory.Exists(logsDir))
                {
                    Directory.CreateDirectory(logsDir);
                }
                
                Application.OpenURL("file://" + logsDir);
                Info("Opening logs directory: " + logsDir);
            }
            catch (Exception ex)
            {
                Error("Failed to open logs directory", ex);
            }
        }
    }
}

