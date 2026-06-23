using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace YangTools.Revit.Core
{
    public class RibbonConfig
    {
        // Key = PanelName, Value = (Key = Command FullName, Value = Is Visible on Main Panel)
        public Dictionary<string, Dictionary<string, bool>> PanelSettings { get; set; } = new Dictionary<string, Dictionary<string, bool>>();
    }

    public static class RibbonConfigManager
    {
        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "YangTools",
            "ribbon_config.json"
        );

        private static RibbonConfig _currentConfig = null;

        public static RibbonConfig Current
        {
            get
            {
                if (_currentConfig == null)
                {
                    _currentConfig = Load();
                }
                return _currentConfig;
            }
        }

        private static RibbonConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    return JsonConvert.DeserializeObject<RibbonConfig>(json) ?? new RibbonConfig();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[YangTools] Failed to load ribbon config: {ex.Message}");
            }
            return new RibbonConfig();
        }

        public static void Save(RibbonConfig config)
        {
            try
            {
                _currentConfig = config;
                string dir = Path.GetDirectoryName(ConfigPath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                string json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[YangTools] Failed to save ribbon config: {ex.Message}");
            }
        }

        /// <summary>
        /// 查询指定的命令是否应该作为独立图标显示在面板上
        /// </summary>
        public static bool IsCommandVisibleOnMainPanel(string panelName, string commandFullName, bool defaultVisible = false)
        {
            if (Current.PanelSettings.TryGetValue(panelName, out var commands))
            {
                if (commands.TryGetValue(commandFullName, out bool isVisible))
                {
                    return isVisible;
                }
            }
            return defaultVisible;
        }

        public static void SetCommandVisibility(string panelName, string commandFullName, bool isVisible)
        {
            if (!Current.PanelSettings.ContainsKey(panelName))
            {
                Current.PanelSettings[panelName] = new Dictionary<string, bool>();
            }
            Current.PanelSettings[panelName][commandFullName] = isVisible;
        }
    }
}
