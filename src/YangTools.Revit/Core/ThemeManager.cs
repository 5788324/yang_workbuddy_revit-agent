using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Newtonsoft.Json;

namespace YangTools.Revit.Core
{
    /// <summary>
    /// 主题管理器 — 支持多套配色方案一键切换。
    /// 所有窗口使用 DynamicResource 引用主题色，切换主题时自动刷新。
    /// </summary>
    public static class ThemeManager
    {
        private const string ThemeConfigFileName = "yangtools_theme.json";

        /// <summary>
        /// 预设主题列表
        /// </summary>
        public static readonly IReadOnlyList<ThemeInfo> Presets = new List<ThemeInfo>
        {
            new ThemeInfo
            {
                Id = "light_warm",
                DisplayName = "暖光米白",
                Uri = "Themes/LightTheme.xaml",
                PreviewColor = "#A0734A",
                Description = "温暖舒适的浅色调，适合长时间工作"
            },
            new ThemeInfo
            {
                Id = "dark_modern",
                DisplayName = "深色现代",
                Uri = "Themes/DarkTheme.xaml",
                PreviewColor = "#2D2D30",
                Description = "护眼深色模式，适合暗光环境"
            },
            new ThemeInfo
            {
                Id = "prof_blue",
                DisplayName = "专业蓝灰",
                Uri = "Themes/ProfBlueTheme.xaml",
                PreviewColor = "#2C5F8A",
                Description = "专业的蓝灰色调，适合企业环境"
            }
        };

        private static ThemeInfo? _currentTheme;
        private static ResourceDictionary? _currentThemeDict;

        /// <summary>
        /// 当前激活的主题
        /// </summary>
        public static ThemeInfo CurrentTheme
        {
            get => _currentTheme ?? Presets[0];
            private set => _currentTheme = value;
        }

        /// <summary>
        /// 主题切换事件
        /// </summary>
        public static event Action<ThemeInfo>? ThemeChanged;

        /// <summary>
        /// 安装目录（DLL 所在目录）
        /// </summary>
        private static string GetInstallDir()
        {
            return Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".";
        }

        /// <summary>
        /// 在插件启动时初始化主题
        /// </summary>
        public static void Initialize()
        {
            string savedThemeId = LoadSavedThemeId();
            var theme = Presets.FirstOrDefault(t => t.Id == savedThemeId) ?? Presets[0];
            ApplyTheme(theme);
        }

        /// <summary>
        /// 切换到指定主题
        /// </summary>
        public static void SwitchTheme(string themeId)
        {
            var theme = Presets.FirstOrDefault(t => t.Id == themeId);
            if (theme == null) return;

            ApplyTheme(theme);
            SaveThemeId(themeId);
        }

        /// <summary>
        /// 应用主题到 WPF 应用程序级别
        /// </summary>
        private static void ApplyTheme(ThemeInfo theme)
        {
            // 先移除旧主题
            if (_currentThemeDict != null)
            {
                Application.Current.Resources.MergedDictionaries.Remove(_currentThemeDict);
            }

            // 加载新主题
            string themePath = Path.Combine(GetInstallDir(), theme.Uri);
            if (!File.Exists(themePath))
            {
                // 回退：尝试从开发目录加载
                string devPath = Path.Combine(GetInstallDir(), "UI", theme.Uri);
                if (File.Exists(devPath)) themePath = devPath;
            }

            var dict = new ResourceDictionary { Source = new Uri(themePath, UriKind.Absolute) };
            _currentThemeDict = dict;
            _currentTheme = theme;

            // 合并到全局资源
            Application.Current.Resources.MergedDictionaries.Add(dict);

            // 也合并 SharedStyles（如果还没合并）
            string sharedPath = Path.Combine(GetInstallDir(), "UI", "SharedStyles.xaml");
            if (File.Exists(sharedPath))
            {
                bool alreadyLoaded = Application.Current.Resources.MergedDictionaries
                    .OfType<ResourceDictionary>()
                    .Any(d => d.Source?.LocalPath?.Contains("SharedStyles.xaml") == true);
                if (!alreadyLoaded)
                {
                    Application.Current.Resources.MergedDictionaries.Add(
                        new ResourceDictionary { Source = new Uri(sharedPath, UriKind.Absolute) });
                }
            }

            ThemeChanged?.Invoke(theme);
        }

        private static string LoadSavedThemeId()
        {
            try
            {
                string path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "YangTools", ThemeConfigFileName);
                if (File.Exists(path))
                {
                    var config = JsonConvert.DeserializeObject<ThemeConfig>(File.ReadAllText(path));
                    return config?.ThemeId ?? "light_warm";
                }
            }
            catch { }
            return "light_warm";
        }

        private static void SaveThemeId(string themeId)
        {
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YangTools");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, ThemeConfigFileName);
                File.WriteAllText(path, JsonConvert.SerializeObject(new ThemeConfig { ThemeId = themeId }));
            }
            catch { }
        }

        private class ThemeConfig
        {
            public string ThemeId { get; set; } = "light_warm";
        }
    }

    /// <summary>
    /// 主题信息
    /// </summary>
    public class ThemeInfo
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Uri { get; set; } = "";
        public string PreviewColor { get; set; } = "#808080";
        public string Description { get; set; } = "";
    }
}
