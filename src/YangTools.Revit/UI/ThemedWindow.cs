using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace YangTools.Revit.UI
{
    /// <summary>
    /// 所有 YangTools 窗口的基类。
    /// 自动加载主题资源，提供统一的窗口镶边（拖拽移动、关闭按钮）。
    /// </summary>
    public class ThemedWindow : Window
    {
        private bool _themeLoaded = false;

        public ThemedWindow()
        {
            // 默认窗口设置
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = System.Windows.Media.Brushes.Transparent;
            this.FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei");

            this.Loaded += OnThemedWindowLoaded;
            Core.ThemeManager.ThemeChanged += OnThemeChanged;
        }

        private void OnThemedWindowLoaded(object sender, RoutedEventArgs e)
        {
            LoadThemeResources();
        }

        private void OnThemeChanged(Core.ThemeInfo theme)
        {
            // 重新加载主题资源
            Dispatcher.Invoke(() =>
            {
                LoadThemeResources();
            });
        }

        private void LoadThemeResources()
        {
            try
            {
                // 移除旧主题（如果有）
                if (_themeLoaded)
                {
                    var oldDict = Resources.MergedDictionaries
                        .OfType<ResourceDictionary>()
                        .FirstOrDefault(d => d.Source?.OriginalString?.Contains("Theme") == true);
                    if (oldDict != null)
                        Resources.MergedDictionaries.Remove(oldDict);
                }

                // 加载主题色
                var theme = Core.ThemeManager.CurrentTheme;
                string installDir = Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".";

                string themePath = Path.Combine(installDir, "UI", theme.Uri);
                if (!File.Exists(themePath))
                {
                    // fallback
                    themePath = Path.Combine(installDir, theme.Uri);
                }

                if (File.Exists(themePath))
                {
                    Resources.MergedDictionaries.Add(
                        new ResourceDictionary { Source = new Uri(themePath, UriKind.Absolute) });
                }

                // 加载 SharedStyles
                string sharedPath = Path.Combine(installDir, "UI", "SharedStyles.xaml");
                if (File.Exists(sharedPath))
                {
                    // 检查是否已加载
                    bool loaded = Resources.MergedDictionaries
                        .OfType<ResourceDictionary>()
                        .Any(d => d.Source?.OriginalString?.Contains("SharedStyles") == true);
                    if (!loaded)
                    {
                        Resources.MergedDictionaries.Add(
                            new ResourceDictionary { Source = new Uri(sharedPath, UriKind.Absolute) });
                    }
                }

                _themeLoaded = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[YangTools] Theme load failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 子窗口可调用此方法实现窗口拖拽移动
        /// </summary>
        protected void OnWindowDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        /// <summary>
        /// 子窗口可调用此方法关闭窗口
        /// </summary>
        protected void OnCloseClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Core.ThemeManager.ThemeChanged -= OnThemeChanged;
            this.Loaded -= OnThemedWindowLoaded;
        }
    }
}
