using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace YangTools.Revit.Core
{
    /// <summary>
    /// Ribbon 界面自动构建工具
    /// </summary>
    public static class RibbonBuilder
    {
        private class CommandDef
        {
            public Type Type { get; set; } = null!;
            public RibbonButtonAttribute Attr { get; set; } = null!;
            public string? RuntimeGroupName { get; set; }
            public bool RuntimeIsSlideOut { get; set; }
        }

        private const string TabName = "YangTools";

        /// <summary>
        /// 扫描程序集并构建 Ribbon 菜单
        /// </summary>
        /// <param name="application">Revit UIApplication</param>
        public static void Build(UIControlledApplication application)
        {
            // 1. 创建自定义的 Ribbon Tab
            try
            {
                application.CreateRibbonTab(TabName);
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                // 如果页签已经存在，会抛出此异常，可以安全忽略
            }

            // 2. 获取当前程序集
            Assembly assembly = Assembly.GetExecutingAssembly();

            // 3. 扫描实现了 IExternalCommand 并标记了 RibbonButtonAttribute 属性的非抽象类
            var commandTypes = assembly.GetTypes()
                .Where(t => typeof(IExternalCommand).IsAssignableFrom(t) && !t.IsAbstract)
                .Select(t => new CommandDef
                {
                    Type = t,
                    Attr = t.GetCustomAttribute<RibbonButtonAttribute>()!
                })
                .Where(x => x.Attr != null)
                .ToList();

            // 4. 按 PanelName 进行分组，批量创建 Panel 和 Button
            var groupedCommands = commandTypes.GroupBy(x => x.Attr.PanelName);

            foreach (var group in groupedCommands)
            {
                string panelName = group.Key;
                RibbonPanel panel = GetOrCreatePanel(application, TabName, panelName);

                // Apply config logic
                var panelCommands = group.ToList();
                foreach (var cmd in panelCommands)
                {
                    bool defaultVis = !cmd.Attr.IsSlideOut;
                    bool isVisible = RibbonConfigManager.IsCommandVisibleOnMainPanel(panelName, cmd.Type.FullName, defaultVis);

                    cmd.RuntimeGroupName = null; // 不要再有高级文本/更多功能了

                    if (isVisible)
                    {
                        cmd.RuntimeIsSlideOut = false; // Display on main panel
                    }
                    else
                    {
                        cmd.RuntimeIsSlideOut = true; // Display in SlideOut (the panel dropdown arrow)
                    }
                }

                var normalItems = panelCommands.Where(x => !x.RuntimeIsSlideOut).ToList();
                var slideOutItems = panelCommands.Where(x => x.RuntimeIsSlideOut).ToList();

                AddItemsToPanel(panel, normalItems, assembly);

                if (slideOutItems.Any())
                {
                    panel.AddSlideOut();
                    AddItemsToPanel(panel, slideOutItems, assembly);
                }
            }
        }

        private static void AddItemsToPanel(RibbonPanel panel, System.Collections.Generic.List<CommandDef> items, Assembly assembly)
        {
            var standaloneItems = items.Where(x => string.IsNullOrEmpty(x.RuntimeGroupName));
            var groupedItems = items.Where(x => !string.IsNullOrEmpty(x.RuntimeGroupName)).GroupBy(x => x.RuntimeGroupName!);

            // 1. Add standalone buttons
            foreach (var cmd in standaloneItems)
            {
                panel.AddItem(CreatePushButtonData(cmd.Type, cmd.Attr, assembly));
            }

            // 2. Add Pulldown buttons
            foreach (var group in groupedItems)
            {
                string groupName = group.Key;
                var firstAttr = group.First().Attr;

                PulldownButtonData pulldownData = new PulldownButtonData(
                    $"pulldown_{groupName}",
                    groupName
                );

                if (!string.IsNullOrWhiteSpace(firstAttr.LargeIcon))
                {
                    pulldownData.LargeImage = LoadImage(firstAttr.LargeIcon);
                }
                if (!string.IsNullOrWhiteSpace(firstAttr.SmallIcon))
                {
                    pulldownData.Image = LoadImage(firstAttr.SmallIcon);
                }

                if (panel.AddItem(pulldownData) is PulldownButton pulldownBtn)
                {
                    foreach (var cmd in group)
                    {
                        pulldownBtn.AddPushButton(CreatePushButtonData(cmd.Type, cmd.Attr, assembly));
                    }
                }
            }
        }

        private static PushButtonData CreatePushButtonData(Type type, RibbonButtonAttribute attr, Assembly assembly)
        {
            string buttonId = $"btn_{type.FullName}";
            PushButtonData buttonData = new PushButtonData(
                buttonId,
                attr.ButtonText,
                assembly.Location,
                type.FullName
            )
            {
                ToolTip = attr.Tooltip
            };

            if (!string.IsNullOrWhiteSpace(attr.LargeIcon))
            {
                buttonData.LargeImage = LoadImage(attr.LargeIcon);
            }
            if (!string.IsNullOrWhiteSpace(attr.SmallIcon))
            {
                buttonData.Image = LoadImage(attr.SmallIcon);
            }

            return buttonData;
        }

        /// <summary>
        /// 获取或创建 Ribbon 面板
        /// </summary>
        private static RibbonPanel GetOrCreatePanel(UIControlledApplication application, string tabName, string panelName)
        {
            // 获取已有的面板
            var existingPanels = application.GetRibbonPanels(tabName);
            var panel = existingPanels.FirstOrDefault(p => p.Name == panelName);

            if (panel == null)
            {
                // 如果没有，则创建新面板
                panel = application.CreateRibbonPanel(tabName, panelName);
            }

            return panel;
        }

        /// <summary>
        /// 安全加载图像资源
        /// </summary>
        private static ImageSource? LoadImage(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

            try
            {
                // 1. 如果是标准的 WPF pack:// 资源路径形式
                if (path!.StartsWith("pack://", StringComparison.OrdinalIgnoreCase))
                {
                    return new BitmapImage(new Uri(path));
                }

                // 2. 如果是相对路径，基于 DLL 所在目录寻找
                string dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
                string absolutePath = Path.Combine(dllDir, path);

                if (File.Exists(absolutePath))
                {
                    return new BitmapImage(new Uri(absolutePath));
                }

                // 3. 如果直接是绝对路径
                if (File.Exists(path))
                {
                    return new BitmapImage(new Uri(path));
                }
            }
            catch (Exception ex)
            {
                // 忽略图片加载异常，确保不影响命令本身在 Revit 中的加载
                System.Diagnostics.Debug.WriteLine($"[YangTools] 载入图标 [{path}] 失败: {ex.Message}");
            }

            return null;
        }
    }
}
