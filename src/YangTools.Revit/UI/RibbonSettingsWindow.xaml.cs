using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

    /// <summary>
    /// 主题选项 ViewModel（用于设置窗口内的主题选择器）
    /// </summary>
    public class ThemeOption : INotifyPropertyChanged
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

    public partial class RibbonSettingsWindow : Window
    {
        private Dictionary<string, List<CommandSettingItem>> _panelSettings = new();
        private List<ThemeOption> _themeOptions = new();

        public RibbonSettingsWindow()
        {
            InitializeComponent();
            LoadThemes();
            LoadCommands();
        }

        private void LoadThemes()
        {
            _themeOptions = ThemeManager.Presets.Select(t => new ThemeOption
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
            if ((sender as FrameworkElement)?.DataContext is ThemeOption option)
            {
                foreach (var t in _themeOptions) t.IsSelected = false;
                option.IsSelected = true;
            }
        }

        private void LoadCommands()
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

                var dataTemplate = new DataTemplate();
                var factory = new FrameworkElementFactory(typeof(CheckBox));
                factory.SetBinding(CheckBox.IsCheckedProperty, new System.Windows.Data.Binding("IsVisible"));
                factory.SetBinding(CheckBox.ContentProperty, new System.Windows.Data.Binding("DisplayName"));
                factory.SetValue(CheckBox.MarginProperty, new Thickness(5));
                factory.SetValue(CheckBox.ForegroundProperty, 
                    Application.Current.TryFindResource("PrimaryText") ?? System.Windows.Media.Brushes.Black);
                dataTemplate.VisualTree = factory;
                listBox.ItemTemplate = dataTemplate;

                tabItem.Content = listBox;
                PanelTabControl.Items.Add(tabItem);
            }

            if (PanelTabControl.Items.Count > 0)
                PanelTabControl.SelectedIndex = 0;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // 保存面板可见性设置
            foreach (var kvp in _panelSettings)
            {
                string panelName = kvp.Key;
                foreach (var item in kvp.Value)
                {
                    RibbonConfigManager.SetCommandVisibility(panelName, item.CommandFullName, item.IsVisible);
                }
            }

            RibbonConfigManager.Save(RibbonConfigManager.Current);

            // 保存主题设置
            var selectedTheme = _themeOptions.FirstOrDefault(t => t.IsSelected);
            if (selectedTheme != null && selectedTheme.Id != ThemeManager.CurrentTheme.Id)
            {
                ThemeManager.SwitchTheme(selectedTheme.Id);
            }

            TaskDialog.Show("设置已保存", 
                "您的界面配置已成功保存！\n\n" +
                (selectedTheme != null && selectedTheme.Id != ThemeManager.CurrentTheme.Id
                    ? "主题将在下次打开设置窗口时生效。\n"
                    : "") +
                "注意：Ribbon 面板更改需要【重启 Revit】才能看到。");
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
