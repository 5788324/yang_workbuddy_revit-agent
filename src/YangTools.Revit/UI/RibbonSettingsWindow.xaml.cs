using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using YangTools.Revit.Core;
using Autodesk.Revit.UI;

namespace YangTools.Revit.UI
{
    public class CommandSettingItem
    {
        public string CommandFullName { get; set; }
        public string DisplayName { get; set; }
        public bool IsVisible { get; set; }
    }

    public partial class RibbonSettingsWindow : Window
    {
        private Dictionary<string, List<CommandSettingItem>> _panelSettings = new Dictionary<string, List<CommandSettingItem>>();

        public RibbonSettingsWindow()
        {
            InitializeComponent();
            LoadCommands();
        }

        private void LoadCommands()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var commands = assembly.GetTypes()
                .Where(t => typeof(IExternalCommand).IsAssignableFrom(t) && !t.IsAbstract)
                .Select(t => new { Type = t, Attr = t.GetCustomAttribute<RibbonButtonAttribute>() })
                .Where(x => x.Attr != null)
                .ToList();

            var groups = commands.GroupBy(x => x.Attr.PanelName);

            foreach (var group in groups)
            {
                string panelName = group.Key;
                var list = new List<CommandSettingItem>();

                foreach (var cmd in group)
                {
                    bool defaultVis = string.IsNullOrEmpty(cmd.Attr.GroupName);
                    bool isVisible = RibbonConfigManager.IsCommandVisibleOnMainPanel(panelName, cmd.Type.FullName, defaultVis);

                    list.Add(new CommandSettingItem
                    {
                        CommandFullName = cmd.Type.FullName,
                        DisplayName = cmd.Attr.ButtonText,
                        IsVisible = isVisible
                    });
                }

                _panelSettings[panelName] = list;

                // Create UI
                var tabItem = new TabItem { Header = panelName };
                var listBox = new ListBox { ItemsSource = list, BorderThickness = new Thickness(0), Margin = new Thickness(5) };
                
                var dataTemplate = new DataTemplate();
                var factory = new FrameworkElementFactory(typeof(CheckBox));
                factory.SetBinding(CheckBox.IsCheckedProperty, new System.Windows.Data.Binding("IsVisible"));
                factory.SetBinding(CheckBox.ContentProperty, new System.Windows.Data.Binding("DisplayName"));
                factory.SetValue(CheckBox.MarginProperty, new Thickness(5));
                dataTemplate.VisualTree = factory;
                listBox.ItemTemplate = dataTemplate;

                tabItem.Content = listBox;
                PanelTabControl.Items.Add(tabItem);
            }

            if (PanelTabControl.Items.Count > 0)
            {
                PanelTabControl.SelectedIndex = 0;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            foreach (var kvp in _panelSettings)
            {
                string panelName = kvp.Key;
                foreach (var item in kvp.Value)
                {
                    RibbonConfigManager.SetCommandVisibility(panelName, item.CommandFullName, item.IsVisible);
                }
            }
            
            RibbonConfigManager.Save(RibbonConfigManager.Current);
            
            TaskDialog.Show("设置已保存", "您的界面配置已成功保存！\n\n注意：由于 Revit 核心机制限制，您需要【重启 Revit】才能看到界面的最新变化。");
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
