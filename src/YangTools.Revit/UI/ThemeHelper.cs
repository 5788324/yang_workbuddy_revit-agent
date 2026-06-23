using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace YangTools.Revit.UI
{
    /// <summary>
    /// 窗口主题辅助工具。在每个 Window 的构造或 Loaded 中调用一次即可。
    /// </summary>
    public static class ThemeHelper
    {
        private static bool _appThemeLoaded = false;

        /// <summary>
        /// 将当前主题和共享样式注入指定窗口。
        /// 在窗口构造函数末尾调用：ThemeHelper.ApplyToWindow(this);
        /// </summary>
        public static void ApplyToWindow(Window window)
        {
            try
            {
                var theme = Core.ThemeManager.CurrentTheme;
                string installDir = Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".";

                // 加载主题色
                string themePath = Path.Combine(installDir, "UI", theme.Uri);
                if (!File.Exists(themePath))
                    themePath = Path.Combine(installDir, theme.Uri);

                if (File.Exists(themePath) && !IsDictLoaded(window, "Theme.xaml"))
                {
                    window.Resources.MergedDictionaries.Add(
                        new ResourceDictionary { Source = new Uri(themePath, UriKind.Absolute) });
                }

                // 加载 SharedStyles
                string sharedPath = Path.Combine(installDir, "UI", "SharedStyles.xaml");
                if (File.Exists(sharedPath) && !IsDictLoaded(window, "SharedStyles.xaml"))
                {
                    window.Resources.MergedDictionaries.Add(
                        new ResourceDictionary { Source = new Uri(sharedPath, UriKind.Absolute) });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[YangTools] ThemeHelper failed: {ex.Message}");
            }
        }

        private static bool IsDictLoaded(Window window, string nameFragment)
        {
            return window.Resources.MergedDictionaries
                .OfType<ResourceDictionary>()
                .Any(d => d.Source?.OriginalString?.Contains(nameFragment) == true);
        }

        /// <summary>
        /// 确保 Application 级别也加载了主题（作为兜底）
        /// </summary>
        public static void EnsureAppTheme()
        {
            if (_appThemeLoaded) return;
            try
            {
                if (Application.Current == null)
                    new Application();

                var theme = Core.ThemeManager.CurrentTheme;
                string installDir = Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".";

                string themePath = Path.Combine(installDir, "UI", theme.Uri);
                if (!File.Exists(themePath))
                    themePath = Path.Combine(installDir, theme.Uri);

                if (File.Exists(themePath))
                {
                    Application.Current.Resources.MergedDictionaries.Add(
                        new ResourceDictionary { Source = new Uri(themePath, UriKind.Absolute) });
                }

                string sharedPath = Path.Combine(installDir, "UI", "SharedStyles.xaml");
                if (File.Exists(sharedPath))
                {
                    Application.Current.Resources.MergedDictionaries.Add(
                        new ResourceDictionary { Source = new Uri(sharedPath, UriKind.Absolute) });
                }

                _appThemeLoaded = true;
            }
            catch { }
        }
    }
}
