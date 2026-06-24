using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using YangTools.Revit.Core;
using Autodesk.Revit.UI;

namespace YangTools.Revit.UI
{
    public class CommandSettingItem
    {
        public string CommandFullName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public bool IsVisible { get; set; }
    }

    public class ThemeOptionItem : INotifyPropertyChanged
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string PreviewColor { get; set; } = "#808080";
        public string Description { get; set; } = "";

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class AssistantWindow : Window
    {
        private Dictionary<string, List<CommandSettingItem>> _panelSettings = new();
        private List<ThemeOptionItem> _themeOptions = new();

        public AssistantWindow()
        {
            InitializeComponent();
            ThemeHelper.ApplyToWindow(this);
            LoadThemeOptions();
            LoadPanelSettings();
            SetGreeting();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                IntPtr revitNativeWindowHandle = Process.GetCurrentProcess().MainWindowHandle;
                WindowInteropHelper windowInteropHelper = new WindowInteropHelper(this)
                {
                    Owner = revitNativeWindowHandle
                };
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] AssistantWindow.xaml.cs: {0}", ex.Message); }
        }

        private void SetGreeting()
        {
            int hour = DateTime.Now.Hour;
            if (hour >= 6 && hour < 12)
                TxtGreeting.Text = "早上好！新的一天充满活力。";
            else if (hour >= 12 && hour < 18)
                TxtGreeting.Text = "下午好！祝您工作顺利。";
            else
                TxtGreeting.Text = "晚上好！注意休息。";
        }

        // ===== 主题选项 =====
        private void LoadThemeOptions()
        {
            _themeOptions = ThemeManager.Presets.Select(t => new ThemeOptionItem
            {
                Id = t.Id,
                DisplayName = t.DisplayName,
                PreviewColor = t.PreviewColor,
                Description = t.Description,
                IsSelected = t.Id == ThemeManager.CurrentTheme.Id
            }).ToList();
            ThemeList.ItemsSource = _themeOptions;
        }

        private void Theme_Click(object sender, MouseButtonEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is ThemeOptionItem option)
            {
                foreach (var t in _themeOptions) t.IsSelected = false;
                option.IsSelected = true;
                ThemeManager.SwitchTheme(option.Id);
            }
        }

        // ===== Ribbon 面板设置 =====
        private void LoadPanelSettings()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var commands = assembly.GetTypes()
                .Where(t => typeof(IExternalCommand).IsAssignableFrom(t) && !t.IsAbstract)
                .Select(t => new { Type = t, Attr = t.GetCustomAttribute<RibbonButtonAttribute>() })
                .Where(x => x.Attr != null)
                .ToList();

            var groups = commands.GroupBy(x => x.Attr!.PanelName);
            foreach (var group in groups)
            {
                string panelName = group.Key;
                var list = new List<CommandSettingItem>();
                foreach (var cmd in group)
                {
                    bool defaultVis = !cmd.Attr!.IsSlideOut;
                    bool isVisible = RibbonConfigManager.IsCommandVisibleOnMainPanel(panelName, cmd.Type.FullName!, defaultVis);
                    list.Add(new CommandSettingItem
                    {
                        CommandFullName = cmd.Type.FullName!,
                        DisplayName = cmd.Attr.ButtonText,
                        IsVisible = isVisible
                    });
                }
                _panelSettings[panelName] = list;

                var tabItem = new TabItem { Header = panelName };
                var listBox = new ListBox
                {
                    ItemsSource = list,
                    BorderThickness = new Thickness(0),
                    Margin = new Thickness(5),
                    Background = System.Windows.Media.Brushes.Transparent
                };
                var dt = new DataTemplate();
                var factory = new FrameworkElementFactory(typeof(CheckBox));
                factory.SetBinding(CheckBox.IsCheckedProperty, new System.Windows.Data.Binding("IsVisible"));
                factory.SetBinding(CheckBox.ContentProperty, new System.Windows.Data.Binding("DisplayName"));
                factory.SetValue(CheckBox.MarginProperty, new Thickness(5));
                factory.SetValue(CheckBox.ForegroundProperty,
                    Application.Current?.TryFindResource("PrimaryText") ?? System.Windows.Media.Brushes.Black);
                dt.VisualTree = factory;
                listBox.ItemTemplate = dt;
                tabItem.Content = listBox;
                PanelTabControl.Items.Add(tabItem);
            }

            // 保存按钮
            if (PanelTabControl.Items.Count > 0)
            {
                PanelTabControl.SelectedIndex = 0;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            SavePanelSettings();
        }

        private void SavePanelSettings()
        {
            foreach (var kvp in _panelSettings)
            {
                foreach (var item in kvp.Value)
                {
                    RibbonConfigManager.SetCommandVisibility(kvp.Key, item.CommandFullName, item.IsVisible);
                }
            }
            RibbonConfigManager.Save(RibbonConfigManager.Current);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            SavePanelSettings();
            this.Close();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                this.DragMove();
        }
    }
}
