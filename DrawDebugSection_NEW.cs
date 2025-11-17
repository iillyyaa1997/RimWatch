        private static void DrawDebugSection(Listing_Standard listing, RimWatchSettings settings)
        {
            // === v0.8.4: GLOBAL LOGGING MASTER SWITCH ===
            bool oldGlobalLogging = settings.enableGlobalLogging;
            listing.CheckboxLabeled("🌐 Enable All Logging (Master Switch)", ref settings.enableGlobalLogging, 
                "Turn on/off all logging at once. When OFF, only critical errors are logged.");
            
            if (oldGlobalLogging != settings.enableGlobalLogging)
            {
                settings.Write();
            }
            
            if (!settings.enableGlobalLogging)
            {
                listing.Gap(6f);
                GUI.color = new Color(1f, 0.7f, 0.2f);
                listing.Label("⚠️ All logging is disabled. Enable master switch to configure individual log levels.");
                GUI.color = Color.white;
                return;
            }
            
            listing.Gap(12f);
            
            // === LOGGING SETTINGS GROUP ===
            DrawCollapsibleSection(listing, "📋 Logging Settings", "logging_settings", () =>
            {
                // Building Log Level
                listing.Gap(4f);
                listing.Label("Building Construction Log Level:");
                GUI.color = MutedTextColor;
                listing.Label("Controls verbosity for building placement and construction.");
                GUI.color = Color.white;
                
                Rect logLevelRect = listing.GetRect(28f);
                string logLevelText = settings.buildingLogLevel.ToString();
                if (Widgets.ButtonText(logLevelRect, logLevelText))
                {
                    List<FloatMenuOption> options = new List<FloatMenuOption>();
                    foreach (BuildingLogLevel lvl in System.Enum.GetValues(typeof(BuildingLogLevel)))
                    {
                        BuildingLogLevel captured = lvl;
                        options.Add(new FloatMenuOption(captured.ToString(), () => { 
                            settings.buildingLogLevel = captured;
                            settings.Write();
                        }));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                }
                
                listing.Gap(10f);
                
                // Per-System Log Levels
                listing.Label("Per-System Log Levels:");
                GUI.color = MutedTextColor;
                listing.Label("Individual verbosity for each automation system:");
                GUI.color = Color.white;
                
                DrawSystemLogLevelRow(listing, "Work", (lvl) => { settings.workLogLevel = lvl; settings.Write(); }, settings.workLogLevel);
                DrawSystemLogLevelRow(listing, "Farming", (lvl) => { settings.farmingLogLevel = lvl; settings.Write(); }, settings.farmingLogLevel);
                DrawSystemLogLevelRow(listing, "Defense", (lvl) => { settings.defenseLogLevel = lvl; settings.Write(); }, settings.defenseLogLevel);
                DrawSystemLogLevelRow(listing, "Medical", (lvl) => { settings.medicalLogLevel = lvl; settings.Write(); }, settings.medicalLogLevel);
                DrawSystemLogLevelRow(listing, "Trade", (lvl) => { settings.tradeLogLevel = lvl; settings.Write(); }, settings.tradeLogLevel);
                DrawSystemLogLevelRow(listing, "Resource", (lvl) => { settings.resourceLogLevel = lvl; settings.Write(); }, settings.resourceLogLevel);
                DrawSystemLogLevelRow(listing, "ColonistCommands", (lvl) => { settings.colonistCommandsLogLevel = lvl; settings.Write(); }, settings.colonistCommandsLogLevel);
                DrawSystemLogLevelRow(listing, "ColonyDevelopment", (lvl) => { settings.colonyDevelopmentLogLevel = lvl; settings.Write(); }, settings.colonyDevelopmentLogLevel);
                DrawSystemLogLevelRow(listing, "Construction", (lvl) => { settings.constructionLogLevel = lvl; settings.Write(); }, settings.constructionLogLevel);
            });
            
            listing.Gap(12f);
            
            // === DEBUG MODE ===
            bool oldDebug = settings.debugModeEnabled;
            listing.CheckboxLabeled("RimWatch.UI.DebugMode".Translate(), ref settings.debugModeEnabled);
            
            if (oldDebug != settings.debugModeEnabled)
            {
                settings.Write();
            }
            
            listing.Gap(8f);
            
            // === FILE LOGGING ===
            bool oldFileLogging = settings.fileLoggingEnabled;
            listing.CheckboxLabeled("RimWatch.UI.FileLogging".Translate(), ref settings.fileLoggingEnabled);
            
            if (oldFileLogging != settings.fileLoggingEnabled)
            {
                settings.Write();
            }
            
            if (settings.debugModeEnabled || settings.fileLoggingEnabled)
            {
                listing.Gap(6f);
                GUI.color = MutedTextColor;
                listing.Label("RimWatch.UI.DebugWarning".Translate());
                GUI.color = Color.white;
            }
        }

