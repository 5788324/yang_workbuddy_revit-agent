using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;

namespace YangTools.Revit.UI
{
    public partial class ProjectAssetManagerWindow : Window
    {
        private UIApplication _uiapp;
        private Document _doc;
        private RevitEventHandler _handler;
        private ExternalEvent _externalEvent;
        
        public ObservableCollection<AssetItemViewModel> AssetList { get; set; } = new ObservableCollection<AssetItemViewModel>();
        private ICollectionView _assetCollectionView;
        
        public ObservableCollection<LineStyleViewModel> LineStyleList { get; set; } = new ObservableCollection<LineStyleViewModel>();
        public ObservableCollection<FilledRegionViewModel> FilledRegionList { get; set; } = new ObservableCollection<FilledRegionViewModel>();

        public ObservableCollection<PatternComboItem> AvailableLinePatterns { get; set; } = new ObservableCollection<PatternComboItem>();
        public ObservableCollection<PatternComboItem> AvailableFillPatterns { get; set; } = new ObservableCollection<PatternComboItem>();


        
        private List<string> _parameterColumns = new List<string> { "", "" };
        private string _configPath;
        private AssetTreeNode _selectedNode;
        private string _currentTabName;

        public ProjectAssetManagerWindow(UIApplication uiapp)
        {
            _currentTabName = "Views"; // 防止 InitializeComponent 中 TabControl SelectionChanged 重复触发 LoadTab
            InitializeComponent();
            ThemeHelper.ApplyToWindow(this);
            this.DataContext = this;
            _uiapp = uiapp;
            _doc = _uiapp.ActiveUIDocument.Document;
            
            _handler = new RevitEventHandler();
            _externalEvent = ExternalEvent.Create(_handler);

            _assetCollectionView = CollectionViewSource.GetDefaultView(AssetList);
            _assetCollectionView.Filter = FilterAssetItem;

            LoadTab("Views");
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl && MainTabControl.SelectedItem is TabItem tab)
            {
                var newTab = tab.Tag?.ToString();
                if (newTab != _currentTabName)
                {
                    _currentTabName = newTab;
                    LoadTab(_currentTabName);
                }
            }
        }

        private void LoadTab(string tabName)
        {
            if (string.IsNullOrEmpty(tabName)) return;

            var rootNodes = new ObservableCollection<AssetTreeNode>();

            if (tabName == "Views")
            {
                var viewTypes = new FilteredElementCollector(_doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v => !v.IsTemplate)
                    .GroupBy(v => v.ViewType)
                    .OrderBy(g => g.Key.ToString());

                foreach (var group in viewTypes)
                {
                    var node = new AssetTreeNode { Name = group.Key.ToString(), Tag = group.Select(v => v.Id).ToList(), Type = "ViewCategory" };
                    rootNodes.Add(node);
                }
                
                var viewTemplates = new FilteredElementCollector(_doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v => v.IsTemplate)
                    .Select(v => v.Id).ToList();
                if(viewTemplates.Count > 0)
                {
                    rootNodes.Add(new AssetTreeNode { Name = "ViewTemplates (视图模板)", Tag = viewTemplates, Type = "ViewCategory" });
                }
                
                var sheets = new FilteredElementCollector(_doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>().Select(s => s.Id).ToList();
                if(sheets.Count > 0)
                {
                    rootNodes.Add(new AssetTreeNode { Name = "ViewSheets (图纸)", Tag = sheets, Type = "ViewCategory" });
                }
            }
            else if (tabName == "Materials")
            {
                var materials = new FilteredElementCollector(_doc).OfClass(typeof(Material)).Select(m => m.Id).ToList();
                if(materials.Count > 0) rootNodes.Add(new AssetTreeNode { Name = "Materials (材质)", Tag = materials, Type = "Material" });
            }
            else if (tabName == "Worksets")
            {
                if (_doc.IsWorkshared)
                {
                    var worksets = new FilteredWorksetCollector(_doc).OfKind(WorksetKind.UserWorkset).Select(w => YangTools.Revit.Core.ElementIdExtensions.CreateId(w.Id.GetIdValue())).ToList();
                    if(worksets.Count > 0) rootNodes.Add(new AssetTreeNode { Name = "User Worksets (用户工作集)", Tag = worksets, Type = "Workset" });
                }
                else
                {
                    StatusText.Text = "当前文档未开启工作集 (Worksharing not enabled).";
                }
            }
            else if (tabName == "Filters")
            {
                var filters = new FilteredElementCollector(_doc).OfClass(typeof(ParameterFilterElement)).Cast<ParameterFilterElement>().ToList();
                if (filters.Count > 0) 
                {
                    rootNodes.Add(new AssetTreeNode { Name = "View Filters (视图过滤器)", Tag = filters.Select(f => f.Id).ToList(), Type = "Filter" });
                    
                    var usedFilterIds = new HashSet<ElementId>();
                    var views = new FilteredElementCollector(_doc).OfClass(typeof(View)).ToElements();
                    foreach (View v in views)
                    {
                        try
                        {
                            if (v != null && !v.IsTemplate && v.AreGraphicsOverridesAllowed())
                            {
                                var fids = v.GetFilters();
                                if (fids != null)
                                {
                                    foreach(var fid in fids) usedFilterIds.Add(fid);
                                }
                            }
                        }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] ProjectAssetManagerWindow.xaml.cs: {0}", ex.Message); }
                    }

                    var unusedIds = new List<ElementId>();
                    foreach (var f in filters)
                    {
                        if (!usedFilterIds.Contains(f.Id) && !CheckFilterControlsElements(f))
                        {
                            unusedIds.Add(f.Id);
                        }
                    }
                    rootNodes.Add(new AssetTreeNode { Name = "Unused View Filters (未使用的视图过滤器)", Tag = unusedIds, Type = "Filter" });
                }
            }
            else if (tabName == "System")
            {
                var phases = new FilteredElementCollector(_doc).OfClass(typeof(Phase)).Select(p => p.Id).ToList();
                rootNodes.Add(new AssetTreeNode { Name = "Phases (阶段)", Tag = phases, Type = "System" });
                
                var projectInfo = new FilteredElementCollector(_doc).OfClass(typeof(ProjectInfo)).Select(p => p.Id).ToList();
                rootNodes.Add(new AssetTreeNode { Name = "Project Info (项目信息)", Tag = projectInfo, Type = "System" });
                
                var allParams = new FilteredElementCollector(_doc).OfClass(typeof(ParameterElement)).Select(p => p.Id).ToList();
                var allSharedParams = new FilteredElementCollector(_doc).OfClass(typeof(SharedParameterElement)).Select(p => p.Id).ToList();
                var sharedParamIds = new HashSet<ElementId>(allSharedParams);

                var boundParamIds = new HashSet<ElementId>();
                var bindingIterator = _doc.ParameterBindings.ForwardIterator();
                while (bindingIterator.MoveNext())
                {
                    if (bindingIterator.Key is InternalDefinition internalDef)
                    {
                        boundParamIds.Add(internalDef.Id);
                    }
                }

                // 1. Project Parameters Breakdown
                var visibleProjectParams = boundParamIds.ToList();
                var hiddenPureProjectParams = allParams.Where(id => !sharedParamIds.Contains(id) && !boundParamIds.Contains(id)).ToList();
                var allProjectParamsNodeIds = visibleProjectParams.Concat(hiddenPureProjectParams).ToList();

                var projectNode = new AssetTreeNode { Name = "Project Parameters (项目参数)", Tag = allProjectParamsNodeIds, Type = "System" };
                projectNode.Children.Add(new AssetTreeNode { Name = "Visible Bound Params (显式绑定参数)", Tag = visibleProjectParams, Type = "System", Parent = projectNode });
                projectNode.Children.Add(new AssetTreeNode { Name = "Internal Implicit Params (系统隐式参数)", Tag = hiddenPureProjectParams, Type = "System", Parent = projectNode });
                rootNodes.Add(projectNode);

                // 2. Shared Parameters Breakdown
                var boundSharedParams = allSharedParams.Where(id => boundParamIds.Contains(id)).ToList();
                var hiddenSharedParams = allSharedParams.Where(id => !boundParamIds.Contains(id)).ToList();

                var sharedNode = new AssetTreeNode { Name = "Shared Parameters (共享参数)", Tag = allSharedParams, Type = "System" };
                sharedNode.Children.Add(new AssetTreeNode { Name = "Project Bound Shared Params (已绑定到项目)", Tag = boundSharedParams, Type = "System", Parent = sharedNode });
                sharedNode.Children.Add(new AssetTreeNode { Name = "Family Ghost Shared Params (族内幽灵参数)", Tag = hiddenSharedParams, Type = "System", Parent = sharedNode });
                rootNodes.Add(sharedNode);

                var globalParams = new FilteredElementCollector(_doc).OfClass(typeof(GlobalParameter)).Select(p => p.Id).ToList();
                rootNodes.Add(new AssetTreeNode { Name = "Global Parameters (全局参数)", Tag = globalParams, Type = "System" });
            }
            else if (tabName == "ProjectLinks")
            {
                var revitLinks = new FilteredElementCollector(_doc).OfClass(typeof(RevitLinkType)).Select(r => r.Id).ToList();
                if(revitLinks.Count > 0) rootNodes.Add(new AssetTreeNode { Name = "Revit Links (Revit链接)", Tag = revitLinks, Type = "Link" });
                
                var cadLinks = new FilteredElementCollector(_doc).OfClass(typeof(ImportInstance)).Where(i => ((ImportInstance)i).IsLinked).Select(i => i.Id).ToList();
                if(cadLinks.Count > 0) rootNodes.Add(new AssetTreeNode { Name = "CAD Links (CAD链接)", Tag = cadLinks, Type = "Link" });
            }
            else if (tabName == "Groups")
            {
                var groups = new FilteredElementCollector(_doc).OfClass(typeof(GroupType)).Select(g => g.Id).ToList();
                if(groups.Count > 0) rootNodes.Add(new AssetTreeNode { Name = "Groups (组)", Tag = groups, Type = "Group" });
            }

            if (tabName == "FilledRegions" || tabName == "LineStyles")
            {
                DefaultActionPanel.Visibility = System.Windows.Visibility.Visible;
                DuplicateActionPanel.Visibility = System.Windows.Visibility.Visible;
                
                if (ExportBtn != null) ExportBtn.Visibility = System.Windows.Visibility.Collapsed;
                if (ImportBtn != null) ImportBtn.Visibility = System.Windows.Visibility.Collapsed;

                TreeColumn.Width = new GridLength(0);
                SplitterColumn.Width = new GridLength(0);
                TreeBorder.Visibility = System.Windows.Visibility.Collapsed;
                
                AssetDataGrid.Visibility = System.Windows.Visibility.Collapsed;
                if (tabName == "FilledRegions")
                {
                    FilledRegionDataGrid.Visibility = System.Windows.Visibility.Visible;
                    LineStyleDataGrid.Visibility = System.Windows.Visibility.Collapsed;
                    LoadFilledRegions();
                }
                else
                {
                    FilledRegionDataGrid.Visibility = System.Windows.Visibility.Collapsed;
                    LineStyleDataGrid.Visibility = System.Windows.Visibility.Visible;
                    LoadLineStyles();
                }
            }
            else
            {
                DefaultActionPanel.Visibility = System.Windows.Visibility.Visible;
                DuplicateActionPanel.Visibility = System.Windows.Visibility.Collapsed;
                
                if (ExportBtn != null) ExportBtn.Visibility = System.Windows.Visibility.Visible;
                if (ImportBtn != null) ImportBtn.Visibility = System.Windows.Visibility.Visible;

                TreeColumn.Width = new GridLength(250);
                SplitterColumn.Width = new GridLength(10);
                TreeBorder.Visibility = System.Windows.Visibility.Visible;
                
                AssetDataGrid.Visibility = System.Windows.Visibility.Visible;
                FilledRegionDataGrid.Visibility = System.Windows.Visibility.Collapsed;
                LineStyleDataGrid.Visibility = System.Windows.Visibility.Collapsed;

                AssetTreeView.ItemsSource = rootNodes;
                AssetList.Clear();
                AssetDataGrid.ItemsSource = null;
                while (AssetDataGrid.Columns.Count > 2) AssetDataGrid.Columns.RemoveAt(2);
                _selectedNode = null;

                if (rootNodes.Count > 0)
                {
                    _selectedNode = rootNodes[0];
                    string safeName = string.Join("_", _selectedNode.Name.Split(System.IO.Path.GetInvalidFileNameChars()));
                    _configPath = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "YangTools", $"AssetConfig_{_currentTabName}_{safeName}.txt");
                    LoadConfig();
                    LoadData();
                    CreateDynamicColumns();
                }

                if (_currentTabName == "Filters") AnalyzeFilterBtn.Visibility = System.Windows.Visibility.Visible;
                else AnalyzeFilterBtn.Visibility = System.Windows.Visibility.Collapsed;
            }
            
            StatusText.Text = "就绪";
        }

        private void DuplicateAsset_Click(object sender, RoutedEventArgs e)
        {
            var tabItem = MainTabControl.SelectedItem as System.Windows.Controls.TabItem;
            string tab = tabItem?.Tag?.ToString();
            
            if (tab == "FilledRegions")
            {
                DuplicateFilledRegion_Click(null, null);
            }
            else if (tab == "LineStyles")
            {
                DuplicateLineStyle_Click(null, null);
            }
        }

        private void LoadLineStyles()
        {
            LineStyleList.Clear();
            if (AvailableLinePatterns.Count == 0)
            {
                var linePatterns = new FilteredElementCollector(_doc).OfClass(typeof(LinePatternElement)).Cast<LinePatternElement>().OrderBy(p => p.Name).ToList();
                var solidId = LinePatternElement.GetSolidPatternId();
                AvailableLinePatterns.Add(new PatternComboItem { Id = solidId, Name = "Solid", Preview = AssetPreviewGenerator.CreateLinePatternPreview(null, System.Windows.Media.Colors.Black) });
                foreach(var p in linePatterns)
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        AvailableLinePatterns.Add(new PatternComboItem { Id = p.Id, Name = p.Name, Preview = AssetPreviewGenerator.CreateLinePatternPreview(p, System.Windows.Media.Colors.Black) });
                    }, System.Windows.Threading.DispatcherPriority.Background);
                }
            }
            
            var linesCat = Category.GetCategory(_doc, BuiltInCategory.OST_Lines);
            if (linesCat != null)
            {
                foreach (Category subCat in linesCat.SubCategories)
                {
                    var vm = new LineStyleViewModel
                    {
                        Id = subCat.Id,
                        Name = subCat.Name,
                        LineWeight = subCat.GetLineWeight(GraphicsStyleType.Projection) ?? 1,
                        ColorBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(subCat.LineColor.Red, subCat.LineColor.Green, subCat.LineColor.Blue)),
                        IsBuiltIn = subCat.Id.GetIdValue() < 0
                    };
                    
                    var patternId = subCat.GetLinePatternId(GraphicsStyleType.Projection);
                    vm.LinePatternId = patternId == ElementId.InvalidElementId ? LinePatternElement.GetSolidPatternId() : patternId;
                    
                    LineStyleList.Add(vm);
                }
            }
            LineStyleDataGrid.ItemsSource = LineStyleList;
            StatusText.Text = $"加载了 {LineStyleList.Count} 个线型";
        }

        private void LoadFilledRegions()
        {
            FilledRegionList.Clear();
            if (AvailableFillPatterns.Count == 0)
            {
                var fillPatterns = new FilteredElementCollector(_doc).OfClass(typeof(FillPatternElement)).Cast<FillPatternElement>().OrderBy(p => p.Name).ToList();
                AvailableFillPatterns.Clear();
                AvailableFillPatterns.Add(new PatternComboItem { Id = ElementId.InvalidElementId, Name = "<无填充图案>", Preview = null });
                foreach(var p in fillPatterns)
                {
                    AvailableFillPatterns.Add(new PatternComboItem { Id = p.Id, Name = p.Name, Preview = null });
                }
            }
            
            var types = new FilteredElementCollector(_doc).OfClass(typeof(FilledRegionType)).Cast<FilledRegionType>().OrderBy(t => t.Name);
            foreach (var t in types)
            {
                var vm = new FilledRegionViewModel
                {
                    Id = t.Id,
                    Name = t.Name,
                    ForeColorBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(t.ForegroundPatternColor.Red, t.ForegroundPatternColor.Green, t.ForegroundPatternColor.Blue)),
                    BackColorBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(t.BackgroundPatternColor.Red, t.BackgroundPatternColor.Green, t.BackgroundPatternColor.Blue)),
                    IsMasking = t.IsMasking,
                    ForePatternId = t.ForegroundPatternId,
                    BackPatternId = t.BackgroundPatternId
                };

                FilledRegionList.Add(vm);
            }
            FilledRegionDataGrid.ItemsSource = FilledRegionList;
            StatusText.Text = $"加载了 {FilledRegionList.Count} 个填充图案";
        }

        private void AssetTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_doc == null || !_doc.IsValidObject) return;
            if (e.NewValue is AssetTreeNode node)
            {
                _selectedNode = node;
                
                string safeName = string.Join("_", node.Name.Split(System.IO.Path.GetInvalidFileNameChars()));
                _configPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "YangTools", $"AssetConfig_{_currentTabName}_{safeName}.txt");

                LoadConfig();
                LoadData();
                CreateDynamicColumns();
            }
        }
        
        private void LoadConfig()
        {
            if (string.IsNullOrEmpty(_configPath)) return;
            if (_selectedNode != null && _selectedNode.Name.Contains("Project Info"))
            {
                _parameterColumns = new List<string> { "Value" };
                return;
            }

            if (System.IO.File.Exists(_configPath))
            {
                try
                {
                    string text = System.IO.File.ReadAllText(_configPath);
                    _parameterColumns = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    if (_parameterColumns.Count == 0) _parameterColumns = new List<string> { "", "" };
                }
                catch { _parameterColumns = new List<string> { "", "" }; }
            }
            else
            {
                _parameterColumns = new List<string> { "", "" };
            }
        }

        private void SaveConfig()
        {
            if (string.IsNullOrEmpty(_configPath)) return;
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_configPath));
                var validParams = _parameterColumns.Where(p => !string.IsNullOrEmpty(p)).ToList();
                string text = string.Join("\n", validParams);
                System.IO.File.WriteAllText(_configPath, text);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] ProjectAssetManagerWindow.xaml.cs: {0}", ex.Message); }
        }

        private List<ElementId> GetElementsForCurrentNode()
        {
            var ids = new List<ElementId>();
            if (_selectedNode == null) return ids;

            if (_selectedNode.Tag is List<ElementId> idList)
            {
                ids = idList;
            }
            else if (_selectedNode.Type == "FamilySymbol" && _selectedNode.Tag is ElementId symId)
            {
                var filter = new FamilyInstanceFilter(_doc, symId);
                ids = new FilteredElementCollector(_doc).WherePasses(filter).Select(e => e.Id).ToList();
            }
            else if (_selectedNode.Type == "Standard" && _selectedNode.Tag?.ToString() == "Materials")
            {
                ids = new FilteredElementCollector(_doc).OfClass(typeof(Material)).Select(e => e.Id).ToList();
            }

            return ids;
        }

        private void CreateDynamicColumns()
        {
            while (AssetDataGrid.Columns.Count > 2)
            {
                AssetDataGrid.Columns.RemoveAt(2);
            }

            List<AvailableParamItem> availableParams = new List<AvailableParamItem>();
            try
            {
                var ids = GetElementsForCurrentNode();
                var dict = new Dictionary<string, bool>();

                if (ids.Count > 0)
                {
                    var el = _doc.GetElement(ids.First());
                    if (el != null)
                    {
                        foreach (Parameter param in el.GetOrderedParameters())
                        {
                            try
                            {
                                string name = param.Definition?.Name;
                                if (!string.IsNullOrEmpty(name)) dict[name] = param.IsReadOnly;
                            }
                            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] ProjectAssetManagerWindow.xaml.cs: {0}", ex.Message); }
                        }
                    }
                }
                
                availableParams = dict.Select(kv => new AvailableParamItem { Name = kv.Key, IsReadOnly = kv.Value })
                    .OrderBy(x => x.Name).ToList();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] ProjectAssetManagerWindow.xaml.cs: {0}", ex.Message); }

            if (_currentTabName == "Filters")
            {
                var textCol1 = new System.Windows.Controls.DataGridTextColumn
                {
                    Header = "是否控制实例元素",
                    Binding = new System.Windows.Data.Binding($"Parameters[是否控制实例元素].Value"),
                    IsReadOnly = true,
                    Width = new System.Windows.Controls.DataGridLength(1, System.Windows.Controls.DataGridLengthUnitType.Star)
                };
                AssetDataGrid.Columns.Add(textCol1);

                var textCol2 = new System.Windows.Controls.DataGridTextColumn
                {
                    Header = "是否在视图中使用",
                    Binding = new System.Windows.Data.Binding($"Parameters[是否在视图中使用].Value"),
                    IsReadOnly = true,
                    Width = new System.Windows.Controls.DataGridLength(1, System.Windows.Controls.DataGridLengthUnitType.Star)
                };
                AssetDataGrid.Columns.Add(textCol2);

                var textCol3 = new System.Windows.Controls.DataGridTextColumn
                {
                    Header = "在视图样板中是否使用",
                    Binding = new System.Windows.Data.Binding($"Parameters[在视图样板中是否使用].Value"),
                    IsReadOnly = true,
                    Width = new System.Windows.Controls.DataGridLength(1, System.Windows.Controls.DataGridLengthUnitType.Star)
                };
                AssetDataGrid.Columns.Add(textCol3);
            }

            for (int colIndex = 0; colIndex < _parameterColumns.Count; colIndex++)
            {
                string pName = _parameterColumns[colIndex];

                if (_selectedNode != null && _selectedNode.Name.Contains("Project Info") && pName == "Value")
                {
                    var textCol = new System.Windows.Controls.DataGridTextColumn
                    {
                        Header = "参数值 (Value)",
                        Binding = new System.Windows.Data.Binding($"Parameters[{pName}].Value"),
                        IsReadOnly = false,
                        Width = new System.Windows.Controls.DataGridLength(1, System.Windows.Controls.DataGridLengthUnitType.Star)
                    };
                    AssetDataGrid.Columns.Add(textCol);
                    continue;
                }

                int capturedIndex = colIndex;

                var col = new DataGridTextColumn();
                col.Width = new DataGridLength(1, DataGridLengthUnitType.Auto);

                var combo = new System.Windows.Controls.ComboBox();
                combo.ItemsSource = availableParams;
                combo.SelectedValuePath = "Name";
                if (!string.IsNullOrEmpty(pName)) combo.SelectedValue = pName;
                combo.Width = 120;
                combo.Margin = new Thickness(0, 0, 5, 0);

                var template = new DataTemplate();
                var factory = new FrameworkElementFactory(typeof(TextBlock));
                factory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Name"));
                var tbStyle = new Style(typeof(TextBlock));
                var trigger = new DataTrigger { Binding = new System.Windows.Data.Binding("IsReadOnly"), Value = true };
                trigger.Setters.Add(new Setter(TextBlock.ForegroundProperty, System.Windows.Media.Brushes.Gray));
                tbStyle.Triggers.Add(trigger);
                factory.SetValue(TextBlock.StyleProperty, tbStyle);
                template.VisualTree = factory;
                combo.ItemTemplate = template;

                combo.SelectionChanged += (s, ev) =>
                {
                    try
                    {
                        if (combo.SelectedValue is string newPName)
                        {
                            _parameterColumns[capturedIndex] = newPName;
                            SaveConfig();
                            CreateDynamicColumns();
                            LoadData();
                        }
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] ProjectAssetManagerWindow.xaml.cs: {0}", ex.Message); }
                };

                col.Header = combo;

                if (!string.IsNullOrEmpty(pName))
                {
                    col.Binding = new System.Windows.Data.Binding($"Parameters[{pName}].Value") { UpdateSourceTrigger = UpdateSourceTrigger.Explicit };

                    var elStyle = new Style(typeof(TextBlock));
                    var t1 = new DataTrigger { Binding = new System.Windows.Data.Binding($"Parameters[{pName}].IsReadOnly"), Value = true };
                    t1.Setters.Add(new Setter(TextBlock.ForegroundProperty, System.Windows.Media.Brushes.Gray));
                    elStyle.Triggers.Add(t1);
                    col.ElementStyle = elStyle;

                    var editStyle = new Style(typeof(System.Windows.Controls.TextBox));
                    editStyle.Setters.Add(new Setter(System.Windows.Controls.TextBox.IsReadOnlyProperty, new System.Windows.Data.Binding($"Parameters[{pName}].IsReadOnly")));
                    var t2 = new DataTrigger { Binding = new System.Windows.Data.Binding($"Parameters[{pName}].IsReadOnly"), Value = true };
                    t2.Setters.Add(new Setter(System.Windows.Controls.TextBox.ForegroundProperty, System.Windows.Media.Brushes.Gray));
                    editStyle.Triggers.Add(t2);
                    col.EditingElementStyle = editStyle;
                }
                else
                {
                    col.IsReadOnly = true;
                }

                AssetDataGrid.Columns.Add(col);
            }

            var addCol = new DataGridTextColumn();
            var addBtn = new System.Windows.Controls.Button
            {
                Content = "➕",
                ToolTip = "添加列",
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(5, 2, 5, 2)
            };
            addBtn.Click += (s, ev) =>
            {
                _parameterColumns.Add("");
                CreateDynamicColumns();
                LoadData();
            };
            addCol.Header = addBtn;
            addCol.IsReadOnly = true;
            addCol.Width = new DataGridLength(1, DataGridLengthUnitType.Auto);
            AssetDataGrid.Columns.Add(addCol);
        }

        private System.Collections.Generic.IList<Element> _cachedViews;

        private void LoadData()
        {
            _cachedViews = null;
            var ids = GetElementsForCurrentNode();
            
            if (ids.Count == 0 && (_selectedNode == null || !_selectedNode.Name.Contains("Project Info")))
            {
                AssetDataGrid.ItemsSource = null;
                StatusText.Text = "当前节点无数据";
                return;
            }

            int total = ids.Count;
            StatusText.Text = $"正在加载 0 / {total} 个对象...";

            _handler.SetAction(app =>
            {
                var doc = app.ActiveUIDocument.Document;
                if (doc == null || !doc.IsValidObject) return;

                if (_selectedNode != null && _selectedNode.Name.Contains("Project Info"))
                {
                    var pInfo = new FilteredElementCollector(doc).OfClass(typeof(ProjectInfo)).FirstElement() as ProjectInfo;
                    if (pInfo != null)
                    {
                        var tempParams = new List<AssetItemViewModel>();
                        foreach (Parameter p in pInfo.Parameters)
                        {
                            if (p.Definition == null) continue;
                            var pvm = new AssetItemViewModel
                            {
                                AssetId = p.Id,
                                AssetName = p.Definition.Name,
                                IsRowVisible = true
                            };
                            string val = "";
                            try { val = p.AsValueString() ?? p.AsString() ?? (p.StorageType == StorageType.Integer ? p.AsInteger().ToString() : ""); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] ProjectAssetManagerWindow.xaml.cs: {0}", ex.Message); }
                            pvm.Parameters["Value"] = new ParameterViewModel { Value = val, IsReadOnly = p.IsReadOnly };
                            tempParams.Add(pvm);
                        }
                        
                        Dispatcher.Invoke(() =>
                        {
                            AssetList = new System.Collections.ObjectModel.ObservableCollection<AssetItemViewModel>(tempParams.OrderBy(x => x.AssetName));
                            AssetDataGrid.ItemsSource = AssetList;
                            _assetCollectionView = System.Windows.Data.CollectionViewSource.GetDefaultView(AssetList);
                            _assetCollectionView.Filter = FilterAssetItem;
                            _assetCollectionView.Refresh();
                            StatusText.Text = $"加载了 {AssetList.Count} 个项目参数";
                        });
                        return;
                    }
                }

                var newItems = new List<AssetItemViewModel>();
                foreach (var id in ids)
                {
                    if (_currentTabName == "Worksets")
                    {
                        var wt = doc.GetWorksetTable();
                        var worksetId = new WorksetId((int)id.GetIdValue());
                        var workset = wt.GetWorkset(worksetId);
                        if (workset == null) continue;

                        var worksetVm = new AssetItemViewModel
                        {
                            AssetId = id,
                            AssetName = workset.Name,
                            IsRowVisible = true
                        };
                        newItems.Add(worksetVm);
                        continue;
                    }

                    var el = doc.GetElement(id);
                    if (el == null) continue;

                    var vm = new AssetItemViewModel
                    {
                        AssetId = el.Id,
                        AssetName = el.Name,
                        IsRowVisible = true
                    };

                    foreach (var pName in _parameterColumns)
                    {
                        if (string.IsNullOrEmpty(pName)) continue;
                        try
                        {
                            var pvm = new ParameterViewModel { Value = "", BoolValue = false, IsReadOnly = true };
                            var param = el.LookupParameter(pName);
                            if (param == null) param = el.GetParameters(pName).FirstOrDefault();
                            
                            if (param != null)
                            {
                                pvm.Value = GetParameterValueSafely(param);
                                pvm.IsReadOnly = param.IsReadOnly;
                            }
                            vm.Parameters[pName] = pvm;
                        }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] ProjectAssetManagerWindow.xaml.cs: {0}", ex.Message); }
                    }
                    
                    if (_currentTabName == "Filters")
                    {
                        vm.Parameters["是否控制实例元素"] = new ParameterViewModel { Value = CheckFilterControlsElements(el as ParameterFilterElement) ? "是" : "否", IsReadOnly = true };
                        vm.Parameters["是否在视图中使用"] = new ParameterViewModel { Value = CheckFilterUsedInViews(el as ParameterFilterElement) ? "是" : "否", IsReadOnly = true };
                        vm.Parameters["在视图样板中是否使用"] = new ParameterViewModel { Value = CheckFilterUsedInTemplates(el as ParameterFilterElement) ? "是" : "否", IsReadOnly = true };
                    }
                    
                    newItems.Add(vm);
                }

                Dispatcher.Invoke(() =>
                {
                    AssetList.Clear();
                    foreach (var item in newItems) AssetList.Add(item);
                    AssetDataGrid.ItemsSource = AssetList;
                    _assetCollectionView = System.Windows.Data.CollectionViewSource.GetDefaultView(AssetList);
                    _assetCollectionView.Filter = FilterAssetItem;
                    _assetCollectionView.Refresh();
                    StatusText.Text = $"加载了 {AssetList.Count} 个对象";
                });
            }, operationName: "加载数据", showSuccess: false);
            _externalEvent.Raise();
        }

        private bool CheckFilterControlsElements(ParameterFilterElement filter)
        {
            if (filter == null) return false;
            try
            {
                var catIds = filter.GetCategories();
                if (catIds.Count == 0) return false;
                
                var catFilter = new ElementMulticategoryFilter(catIds);
                var elemFilter = filter.GetElementFilter();
                
                var collector = new FilteredElementCollector(_doc).WhereElementIsNotElementType().WherePasses(catFilter);
                if (elemFilter != null) collector = collector.WherePasses(elemFilter);
                
                return collector.GetElementCount() > 0;
            }
            catch { return false; }
        }

        private bool CheckFilterUsedInViews(ParameterFilterElement filter)
        {
            if (filter == null) return false;
            if (_cachedViews == null) _cachedViews = new FilteredElementCollector(_doc).OfClass(typeof(View)).ToElements();
            foreach (View v in _cachedViews)
            {
                try
                {
                    if (v != null && !v.IsTemplate && v.AreGraphicsOverridesAllowed())
                    {
                        var fids = v.GetFilters();
                        if (fids != null && fids.Contains(filter.Id)) return true;
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] ProjectAssetManagerWindow.xaml.cs: {0}", ex.Message); }
            }
            return false;
        }

        private bool CheckFilterUsedInTemplates(ParameterFilterElement filter)
        {
            if (filter == null) return false;
            if (_cachedViews == null) _cachedViews = new FilteredElementCollector(_doc).OfClass(typeof(View)).ToElements();
            foreach (View v in _cachedViews)
            {
                try
                {
                    if (v != null && v.IsTemplate && v.AreGraphicsOverridesAllowed())
                    {
                        var fids = v.GetFilters();
                        if (fids != null && fids.Contains(filter.Id)) return true;
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] ProjectAssetManagerWindow.xaml.cs: {0}", ex.Message); }
            }
            return false;
        }

        private string GetParameterValueSafely(Parameter param)
        {
            if (param == null) return "";
            try
            {
                if (param.StorageType == StorageType.String) return param.AsString() ?? "";
                if (param.StorageType == StorageType.Double) return param.AsValueString() ?? param.AsDouble().ToString();
                if (param.StorageType == StorageType.ElementId) return param.AsValueString() ?? param.AsElementId().GetIdValue().ToString();
                return param.AsValueString() ?? param.AsInteger().ToString();
            }
            catch { return ""; }
        }
        
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_assetCollectionView != null) _assetCollectionView.Refresh();
        }

        private bool FilterAssetItem(object obj)
        {
            if (obj is AssetItemViewModel item)
            {
                if (!item.IsRowVisible) return false;

                string searchText = SearchBox.Text?.Trim().ToLower() ?? "";
                if (!string.IsNullOrEmpty(searchText))
                {
                    bool matchName = item.AssetName != null && item.AssetName.ToLower().Contains(searchText);
                    bool matchId = item.AssetId != null && item.AssetId.GetIdValue().ToString().Contains(searchText);
                    if (!matchName && !matchId)
                        return false;
                }
                return true;
            }
            return false;
        }

        private void LineStyleDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit) return;
            try
            {
                var vm = e.Row.Item as LineStyleViewModel;
                var el = e.EditingElement as System.Windows.Controls.TextBox;
                if (vm == null || el == null) return;
                var newValue = el.Text;

                var bindingExpr = el.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty);
                var bindingPath = (bindingExpr?.ParentBinding as System.Windows.Data.Binding)?.Path?.Path;
                if (string.IsNullOrEmpty(bindingPath)) return;

                if (bindingPath == "Name")
                {
                    if (vm.IsBuiltIn)
                    {
                        TaskDialog.Show("禁止", "内置线型不能重命名。");
                        el.Text = vm.Name;
                        return;
                    }
                    _handler.SetAction(app =>
                    {
                        try
                        {
                            var doc = app.ActiveUIDocument.Document;
                            var linesCat = Category.GetCategory(doc, BuiltInCategory.OST_Lines);
                            var cat = linesCat.SubCategories.Cast<Category>().FirstOrDefault(c => c.Id == vm.Id);
                            if (cat != null)
                            {
                                using (Transaction t = new Transaction(doc, "Rename Line Style"))
                                {
                                    t.Start();
                                    var gs = cat.GetGraphicsStyle(GraphicsStyleType.Projection);
                                    if (gs != null) gs.Name = newValue;
                                    t.Commit();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Dispatcher.Invoke(() => TaskDialog.Show("Error", "Could not rename Line Style: " + ex.Message));
                        }
                    }, operationName: "编辑线型名称");
                    _externalEvent.Raise();
                }
                else if (bindingPath == "LineWeight")
                {
                    if (int.TryParse(newValue, out int weight) && weight >= 1 && weight <= 16)
                    {
                        _handler.SetAction(app =>
                        {
                            try
                            {
                                var doc = app.ActiveUIDocument.Document;
                                var linesCat = Category.GetCategory(doc, BuiltInCategory.OST_Lines);
                                var cat = linesCat.SubCategories.Cast<Category>().FirstOrDefault(c => c.Id == vm.Id);
                                if (cat != null)
                                {
                                    using (Transaction t = new Transaction(doc, "Change Line Weight"))
                                    {
                                        t.Start();
                                        cat.SetLineWeight(weight, GraphicsStyleType.Projection);
                                        t.Commit();
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Dispatcher.Invoke(() => TaskDialog.Show("Error", "Could not set Line Weight: " + ex.Message));
                            }
                        }, operationName: "编辑线宽");
                        _externalEvent.Raise();
                    }
                    else
                    {
                        TaskDialog.Show("无效输入", "线宽必须是 1 到 16 之间的整数。");
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] ProjectAssetManagerWindow.xaml.cs: {0}", ex.Message); }
        }


        private void FilledRegionDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit) return;
            try
            {
                var vm = e.Row.Item as FilledRegionViewModel;
                var el = e.EditingElement as System.Windows.Controls.TextBox;
                var cb = e.EditingElement as System.Windows.Controls.CheckBox;
                if (vm == null) return;
                
                var bindingExpr = el?.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty) ?? 
                                  cb?.GetBindingExpression(System.Windows.Controls.CheckBox.IsCheckedProperty);
                var bindingPath = (bindingExpr?.ParentBinding as System.Windows.Data.Binding)?.Path?.Path;
                if (string.IsNullOrEmpty(bindingPath)) return;

                if (bindingPath == "Name" && el != null)
                {
                    var newValue = el.Text;
                    if (vm.Name.StartsWith("<") || vm.Name.EndsWith(">"))
                    {
                        TaskDialog.Show("禁止", "内置类型不能重命名。");
                        el.Text = vm.Name;
                        return;
                    }
                    _handler.SetAction(app =>
                    {
                        var doc = app.ActiveUIDocument.Document;
                        var frType = doc.GetElement(vm.Id) as FilledRegionType;
                        if (frType != null)
                        {
                            using (Transaction t = new Transaction(doc, "Rename Filled Region"))
                            {
                                t.Start();
                                try { frType.Name = newValue; } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] ProjectAssetManagerWindow.xaml.cs: {0}", ex.Message); }
                                t.Commit();
                            }
                        }
                    }, operationName: "编辑填充区域名称");
                    _externalEvent.Raise();
                }
                else if (bindingPath == "IsMasking" && cb != null)
                {
                    bool val = cb.IsChecked ?? false;
                    _handler.SetAction(app =>
                    {
                        var doc = app.ActiveUIDocument.Document;
                        var frType = doc.GetElement(vm.Id) as FilledRegionType;
                        if (frType != null)
                        {
                            using (Transaction t = new Transaction(doc, "Change Masking"))
                            {
                                t.Start();
                                frType.IsMasking = val;
                                t.Commit();
                            }
                        }
                    }, operationName: "编辑遮罩设置");
                    _externalEvent.Raise();
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] ProjectAssetManagerWindow.xaml.cs: {0}", ex.Message); }
        }

        private void LinePattern_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var combo = sender as System.Windows.Controls.ComboBox;
            if (combo == null || (!combo.IsDropDownOpen && !combo.IsKeyboardFocusWithin)) return;

            var vm = combo.DataContext as LineStyleViewModel;
            if (vm == null || combo.SelectedValue == null) return;
            
            var newId = (ElementId)combo.SelectedValue;
            
            _handler.SetAction(app =>
            {
                var doc = app.ActiveUIDocument.Document;
                var cat = Category.GetCategory(doc, BuiltInCategory.OST_Lines).SubCategories.Cast<Category>().FirstOrDefault(c => c.Id == vm.Id);
                if (cat != null)
                {
                    var currentId = cat.GetLinePatternId(GraphicsStyleType.Projection);
                    var solidId = LinePatternElement.GetSolidPatternId();
                    if (currentId == ElementId.InvalidElementId) currentId = solidId;
                    
                    if (currentId != newId)
                    {
                        using (Transaction t = new Transaction(doc, "Change Line Pattern"))
                        {
                            t.Start();
                            cat.SetLinePatternId(newId, GraphicsStyleType.Projection);
                            t.Commit();
                        }
                    }
                }
            }, operationName: "更改线型图案");
            _externalEvent.Raise();
        }

        private void FillPattern_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var combo = sender as System.Windows.Controls.ComboBox;
            if (combo == null || (!combo.IsDropDownOpen && !combo.IsKeyboardFocusWithin)) return;

            var vm = combo.DataContext as FilledRegionViewModel;
            if (vm == null || combo.SelectedValue == null) return;
            
            string tag = combo.Tag as string;
            var newId = (ElementId)combo.SelectedValue;
            
            _handler.SetAction(app =>
            {
                var doc = app.ActiveUIDocument.Document;
                var frType = doc.GetElement(vm.Id) as FilledRegionType;
                if (frType != null)
                {
                    var currentId = tag == "Fore" ? frType.ForegroundPatternId : frType.BackgroundPatternId;
                    if (currentId != newId)
                    {
                        using (Transaction t = new Transaction(doc, "Change Fill Pattern"))
                        {
                            t.Start();
                            if (tag == "Fore") frType.ForegroundPatternId = newId;
                            else if (tag == "Back") frType.BackgroundPatternId = newId;
                            t.Commit();
                        }
                    }
                }
            }, operationName: "更改填充图案");
            _externalEvent.Raise();
        }

        private string GetUniqueLineStyleName(string baseName)
        {
            var linesCat = Category.GetCategory(_doc, BuiltInCategory.OST_Lines);
            var existingNames = linesCat.SubCategories.Cast<Category>().Select(c => c.Name).ToHashSet();
            string name = baseName + " copy 1";
            int count = 1;
            while (existingNames.Contains(name))
            {
                count++;
                name = baseName + $" copy {count}";
            }
            return name;
        }

        private void DuplicateLineStyle_Click(object sender, RoutedEventArgs e)
        {
            var vm = LineStyleDataGrid.SelectedItem as LineStyleViewModel;
            if (vm == null) return;

            string newName = GetUniqueLineStyleName(vm.Name);

            _handler.SetAction(app =>
            {
                var doc = app.ActiveUIDocument.Document;
                var linesCat = Category.GetCategory(doc, BuiltInCategory.OST_Lines);
                
                using (Transaction t = new Transaction(doc, "Duplicate Line Style"))
                {
                    t.Start();
                    try
                    {
                        var newCat = doc.Settings.Categories.NewSubcategory(linesCat, newName);
                        var originalCat = linesCat.SubCategories.Cast<Category>().FirstOrDefault(c => c.Id == vm.Id);
                        if (originalCat != null)
                        {
                            newCat.LineColor = originalCat.LineColor;
                            newCat.SetLineWeight(originalCat.GetLineWeight(GraphicsStyleType.Projection) ?? 1, GraphicsStyleType.Projection);
                            var patId = originalCat.GetLinePatternId(GraphicsStyleType.Projection);
                            if (patId != ElementId.InvalidElementId)
                            {
                                newCat.SetLinePatternId(patId, GraphicsStyleType.Projection);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() => TaskDialog.Show("Error", "Could not duplicate Line Style: " + ex.Message));
                    }
                    t.Commit();
                }
                Dispatcher.Invoke(() => LoadLineStyles());
            }, operationName: "复制线型");
            _externalEvent.Raise();
        }

        private void DuplicateFilledRegion_Click(object sender, RoutedEventArgs e)
        {
            var vm = FilledRegionDataGrid.SelectedItem as FilledRegionViewModel;
            if (vm == null) return;

            string baseName = vm.Name;
            
            _handler.SetAction(app =>
            {
                var doc = app.ActiveUIDocument.Document;
                var frType = doc.GetElement(vm.Id) as FilledRegionType;
                if (frType != null)
                {
                    using (Transaction t = new Transaction(doc, "Duplicate Filled Region"))
                    {
                        t.Start();
                        try
                        {
                            var existingNames = new FilteredElementCollector(doc).OfClass(typeof(FilledRegionType)).Cast<FilledRegionType>().Select(f => f.Name).ToHashSet();
                            string name = baseName + " copy 1";
                            int count = 1;
                            while (existingNames.Contains(name))
                            {
                                count++;
                                name = baseName + $" copy {count}";
                            }
                            frType.Duplicate(name);
                        }
                        catch (Exception ex)
                        {
                            Dispatcher.Invoke(() => TaskDialog.Show("Error", "Could not duplicate Filled Region: " + ex.Message));
                        }
                        t.Commit();
                    }
                    Dispatcher.Invoke(() => LoadFilledRegions());
                }
            }, operationName: "复制填充区域");
            _externalEvent.Raise();
        }

        private void LineStyleColor_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as System.Windows.Controls.Button;
            var vm = btn?.DataContext as LineStyleViewModel;
            if (vm == null) return;

            var colorDialog = new Autodesk.Revit.UI.ColorSelectionDialog();
            var brush = vm.ColorBrush as System.Windows.Media.SolidColorBrush;
            colorDialog.OriginalColor = new Autodesk.Revit.DB.Color(brush.Color.R, brush.Color.G, brush.Color.B);
            
            if (colorDialog.Show() == Autodesk.Revit.UI.ItemSelectionDialogResult.Confirmed)
            {
                var newColor = colorDialog.SelectedColor;
                vm.ColorBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(newColor.Red, newColor.Green, newColor.Blue));
                
                _handler.SetAction(app =>
                {
                    var doc = app.ActiveUIDocument.Document;
                    var cat = Category.GetCategory(doc, BuiltInCategory.OST_Lines).SubCategories.Cast<Category>().FirstOrDefault(c => c.Id == vm.Id);
                    if (cat != null)
                    {
                        using (Transaction t = new Transaction(doc, "Change Line Color"))
                        {
                            t.Start();
                            cat.LineColor = newColor;
                            t.Commit();
                        }
                    }
                }, operationName: "更改线颜色");
                _externalEvent.Raise();
            }
        }

        private void FilledRegionColor_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as System.Windows.Controls.Button;
            var vm = btn?.DataContext as FilledRegionViewModel;
            if (vm == null) return;

            string tag = btn.Tag as string;
            var colorDialog = new Autodesk.Revit.UI.ColorSelectionDialog();
            var brush = (tag == "Fore" ? vm.ForeColorBrush : vm.BackColorBrush) as System.Windows.Media.SolidColorBrush;
            colorDialog.OriginalColor = new Autodesk.Revit.DB.Color(brush.Color.R, brush.Color.G, brush.Color.B);
            
            if (colorDialog.Show() == Autodesk.Revit.UI.ItemSelectionDialogResult.Confirmed)
            {
                var newColor = colorDialog.SelectedColor;
                if (tag == "Fore") vm.ForeColorBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(newColor.Red, newColor.Green, newColor.Blue));
                else vm.BackColorBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(newColor.Red, newColor.Green, newColor.Blue));
                
                _handler.SetAction(app =>
                {
                    var doc = app.ActiveUIDocument.Document;
                    var frType = doc.GetElement(vm.Id) as FilledRegionType;
                    if (frType != null)
                    {
                        using (Transaction t = new Transaction(doc, "Change Fill Color"))
                        {
                            t.Start();
                            if (tag == "Fore") frType.ForegroundPatternColor = newColor;
                            else frType.BackgroundPatternColor = newColor;
                            t.Commit();
                        }
                    }
                }, operationName: "更改填充颜色");
                _externalEvent.Raise();
            }
        }

        private void AssetDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit) return;
            try
            {
                var vm = e.Row.Item as AssetItemViewModel;
                var el = e.EditingElement as System.Windows.Controls.TextBox;
                if (vm == null || el == null) return;

                string header = e.Column.Header?.ToString();
                
                if (header == "参数值 (Value)" && _selectedNode != null && _selectedNode.Name.Contains("Project Info"))
                {
                    var pInfo = new FilteredElementCollector(_doc).OfClass(typeof(ProjectInfo)).FirstElement() as ProjectInfo;
                    if (pInfo != null)
                    {
                        var paramByName = pInfo.LookupParameter(vm.AssetName);
                        if (paramByName != null && !paramByName.IsReadOnly)
                        {
                            var tb = el;
                            string newVal = tb?.Text;
                            if (newVal != null)
                            {
                                _handler.SetAction(app =>
                                {
                                    using (var t = new Transaction(app.ActiveUIDocument.Document, "Edit Project Info"))
                                    {
                                        t.Start();
                                        try { paramByName.Set(newVal); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] ProjectAssetManagerWindow.xaml.cs: {0}", ex.Message); }
                                        t.Commit();
                                    }
                                }, operationName: "编辑项目信息");
                                _externalEvent.Raise();
                            }
                        }
                    }
                    return;
                }
                var newValue = el.Text;

                var bindingExpr = el.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty);
                var bindingPath = (bindingExpr?.ParentBinding as System.Windows.Data.Binding)?.Path?.Path;
                if (string.IsNullOrEmpty(bindingPath)) return;

                if (bindingPath == "AssetName")
                {
                    var oldName = vm.AssetName;
                    if (newValue == oldName) return;

                    _handler.SetAction(app =>
                    {
                        try
                        {
                            var doc = app.ActiveUIDocument.Document;
                            var elem = doc.GetElement(vm.AssetId);
                            if (elem == null) return;
                            bool success = false;
                            try
                            {
                                using (Transaction t = new Transaction(doc, "Rename Asset"))
                                {
                                    t.Start();
                                    elem.Name = newValue;
                                    t.Commit();
                                    success = true;
                                }
                            }
                            catch (Exception ex)
                            {
                                Dispatcher.Invoke(() => TaskDialog.Show("Error", "Could not rename: " + ex.Message));
                            }
                            Dispatcher.Invoke(() =>
                            {
                                if (success) vm.AssetName = newValue;
                                else vm.AssetName = oldName;
                            });
                        }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] ProjectAssetManagerWindow.xaml.cs: {0}", ex.Message); }
                    }, operationName: "编辑资产名称");
                    _externalEvent.Raise();
                }
                else if (bindingPath.StartsWith("Parameters[") && bindingPath.EndsWith("].Value") && bindingPath.Length > 19)
                {
                    string pName = bindingPath.Substring(11, bindingPath.Length - 19);
                    if (string.IsNullOrEmpty(pName)) return;
                    if (!vm.Parameters.ContainsKey(pName)) return;
                    var pvm = vm.Parameters[pName];
                    var oldValue = pvm.Value;
                    if (newValue == oldValue || pvm.IsReadOnly) return;

                    _handler.SetAction(app =>
                    {
                        try
                        {
                            var doc = app.ActiveUIDocument.Document;
                            var elem = doc.GetElement(vm.AssetId);
                            if (elem == null) return;
                            
                            var param = elem.LookupParameter(pName);
                            if (param == null) param = elem.GetOrderedParameters().FirstOrDefault(p => p.Definition?.Name == pName);
                            
                            if (param == null || param.IsReadOnly) return;

                            bool success = false;
                            try
                            {
                                using (Transaction t = new Transaction(doc, "Set Parameter"))
                                {
                                    t.Start();
                                    if (param.StorageType == StorageType.String) param.Set(newValue);
                                    else if (param.StorageType == StorageType.Double && double.TryParse(newValue, out double d)) param.Set(d);
                                    else if (param.StorageType == StorageType.Integer && int.TryParse(newValue, out int i)) param.Set(i);
                                    else if (param.StorageType != StorageType.ElementId)
                                    {
                                        try { param.SetValueString(newValue); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] ProjectAssetManagerWindow.xaml.cs: {0}", ex.Message); }
                                    }
                                    t.Commit();
                                    success = true;
                                    newValue = GetParameterValueSafely(param);
                                }
                            }
                            catch (Exception ex)
                            {
                                Dispatcher.Invoke(() => TaskDialog.Show("Error", "Could not set parameter: " + ex.Message));
                            }
                            Dispatcher.Invoke(() =>
                            {
                                if (success) pvm.Value = newValue;
                                else pvm.Value = oldValue;
                            });
                        }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] ProjectAssetManagerWindow.xaml.cs: {0}", ex.Message); }
                    }, operationName: "编辑参数值");
                    _externalEvent.Raise();
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] ProjectAssetManagerWindow.xaml.cs: {0}", ex.Message); }
        }

        private void AnalyzeFilter_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedNode == null || !_selectedNode.Name.Contains("View Filters"))
            {
                TaskDialog.Show("提示", "请先在左侧选择 'View Filters (视图过滤器)' 节点。");
                return;
            }

            var selectedItem = AssetDataGrid.SelectedItem as AssetItemViewModel;
            if (selectedItem == null)
            {
                TaskDialog.Show("提示", "请先在表格中选中一个过滤器。");
                return;
            }
            var filter = _doc.GetElement(selectedItem.AssetId) as ParameterFilterElement;
            if (filter == null) return;
            
            var filterWindow = new FilterAnalysisWindow(_doc, filter);
            filterWindow.Show();
        }

        private void ResetColumns_Click(object sender, RoutedEventArgs e)
        {
            _parameterColumns = new List<string>();
            SaveConfig();
            CreateDynamicColumns();
            if (_selectedNode != null) LoadData();
        }

        private void ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            if (AssetList.Count == 0) return;
            var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Excel File (*.xlsx)|*.xlsx", FileName = "ProjectAssets.xlsx" };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var data = new List<Dictionary<string, object>>();
                    foreach (var item in AssetList)
                    {
                        var row = new Dictionary<string, object>();
                        foreach (var column in AssetDataGrid.Columns)
                        {
                            string header = "";
                            string boundKey = "";
                            
                            if (column.Header is System.Windows.Controls.ComboBox cb)
                            {
                                var selected = cb.SelectedItem as AvailableParamItem;
                                if (selected != null) 
                                {
                                    header = selected.Name;
                                    boundKey = selected.Name;
                                }
                            }
                            else if (column.Header is string str)
                            {
                                header = str;
                                if (column is System.Windows.Controls.DataGridTextColumn textCol)
                                {
                                    var binding = textCol.Binding as System.Windows.Data.Binding;
                                    if (binding != null && binding.Path.Path.StartsWith("Parameters["))
                                    {
                                        boundKey = binding.Path.Path.Replace("Parameters[", "").Replace("].Value", "");
                                    }
                                    else if (binding != null && binding.Path.Path == "AssetIdValue") boundKey = "AssetId";
                                    else if (binding != null && binding.Path.Path == "AssetName") boundKey = "AssetName";
                                }
                            }
                            
                            if (string.IsNullOrEmpty(header) || string.IsNullOrEmpty(boundKey)) continue;
                            
                            if (boundKey == "AssetId") row[header] = item.AssetId.GetIdValue().ToString();
                            else if (boundKey == "AssetName") row[header] = item.AssetName;
                            else row[header] = item.Parameters.ContainsKey(boundKey) ? item.Parameters[boundKey].Value : "";
                        }
                        data.Add(row);
                    }
                    MiniExcelLibs.MiniExcel.SaveAs(dialog.FileName, data);
                    TaskDialog.Show("提示", "导出成功！(Export Successful)");
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("错误", "导出失败: " + ex.Message);
                }
            }
        }

        private void ImportExcel_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Excel File (*.xlsx)|*.xlsx" };
            if (dialog.ShowDialog() == true)
            {
                _handler.SetAction(app =>
                {
                    try
                    {
                        var rows = MiniExcelLibs.MiniExcel.Query(dialog.FileName, useHeaderRow: true).Cast<IDictionary<string, object>>();
                        var doc = app.ActiveUIDocument.Document;
                        int successCount = 0;

                        using (Transaction t = new Transaction(doc, "Import Excel Sync"))
                        {
                            t.Start();
                            foreach (var row in rows)
                            {
                                if (!row.ContainsKey("元素ID") || string.IsNullOrEmpty(row["元素ID"]?.ToString())) continue;
                                if (long.TryParse(row["元素ID"].ToString(), out long idVal))
                                {
                                    var elem = doc.GetElement(YangTools.Revit.Core.ElementIdExtensions.CreateId(idVal));
                                    if (elem == null) continue;

                                    if (row.ContainsKey("资产名称") && row["资产名称"] != null)
                                    {
                                        string newName = row["资产名称"].ToString();
                                        if (elem.Name != newName && !string.IsNullOrWhiteSpace(newName))
                                        {
                                            try { elem.Name = newName; } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] ProjectAssetManagerWindow.xaml.cs: {0}", ex.Message); }
                                        }
                                    }

                                    foreach (var kvp in row)
                                    {
                                        if (kvp.Key == "元素ID" || kvp.Key == "资产名称") continue;
                                        string pName = kvp.Key;
                                        string pVal = kvp.Value?.ToString() ?? "";

                                        var param = elem.LookupParameter(pName);
                                        if (param == null) param = elem.GetOrderedParameters().FirstOrDefault(p => p.Definition?.Name == pName);
                                        
                                        if (param != null && !param.IsReadOnly)
                                        {
                                            string oldVal = GetParameterValueSafely(param);
                                            if (oldVal != pVal)
                                            {
                                                try
                                                {
                                                    if (param.StorageType == StorageType.String) param.Set(pVal);
                                                    else if (param.StorageType == StorageType.Double && double.TryParse(pVal, out double d)) param.Set(d);
                                                    else if (param.StorageType == StorageType.Integer && int.TryParse(pVal, out int i)) param.Set(i);
                                                    else if (param.StorageType != StorageType.ElementId)
                                                    {
                                                        param.SetValueString(pVal);
                                                    }
                                                }
                                                catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] ProjectAssetManagerWindow.xaml.cs: {0}", ex.Message); }
                                            }
                                        }
                                    }
                                    successCount++;
                                }
                            }
                            t.Commit();
                        }
                        Dispatcher.Invoke(() =>
                        {
                            LoadData();
                            TaskDialog.Show("提示", $"导入同步完成，成功处理 {successCount} 个图元！");
                        });
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() => TaskDialog.Show("Error", "Import failed: " + ex.Message));
                    }
                }, operationName: "导入Excel同步", showSuccess: false);
                _externalEvent.Raise();
            }
        }
        
        private void SelectInView_Click(object sender, RoutedEventArgs e)
        {
            var ids = new List<ElementId>();

            if (AssetDataGrid.Visibility == System.Windows.Visibility.Visible)
                ids = AssetDataGrid.SelectedItems.Cast<AssetItemViewModel>().Select(x => x.AssetId).ToList();
            else if (FilledRegionDataGrid.Visibility == System.Windows.Visibility.Visible)
                ids = FilledRegionDataGrid.SelectedItems.Cast<FilledRegionViewModel>().Select(x => x.Id).ToList();
            else if (LineStyleDataGrid.Visibility == System.Windows.Visibility.Visible)
                ids = LineStyleDataGrid.SelectedItems.Cast<LineStyleViewModel>().Select(x => x.Id).ToList();

            if (ids.Count == 0)
            {
                TaskDialog.Show("提示", "请先在列表中高亮选择要查看的资产。");
                return;
            }

            _handler.SetAction(app =>
            {
                try
                {
                    var uidoc = app.ActiveUIDocument;
                    uidoc.Selection.SetElementIds(ids);
                    uidoc.ShowElements(ids);
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => TaskDialog.Show("Error", "Could not select: " + ex.Message));
                }
            }, operationName: "在视图中选择");
            _externalEvent.Raise();
        }

        private List<ElementId> GetCheckedElementIds()
        {
            var ids = new HashSet<ElementId>();
            var rootNodes = AssetTreeView.ItemsSource as ObservableCollection<AssetTreeNode>;
            if (rootNodes == null) return new List<ElementId>();

            void Traverse(AssetTreeNode node)
            {
                if (node.IsChecked == true)
                {
                    var nodeIds = GetElementsForNode(node);
                    foreach(var id in nodeIds) ids.Add(id);
                }
                else if (node.IsChecked == null)
                {
                    foreach (var child in node.Children) Traverse(child);
                }
            }

            foreach (var root in rootNodes) Traverse(root);
            return ids.ToList();
        }

        private List<ElementId> GetElementsForNode(AssetTreeNode node)
        {
            var ids = new List<ElementId>();
            if (node == null) return ids;

            if (node.Tag is List<ElementId> idList)
            {
                ids = idList;
            }
            else if (node.Type == "FamilySymbol" && node.Tag is ElementId symId)
            {
                var filter = new FamilyInstanceFilter(_doc, symId);
                ids = new FilteredElementCollector(_doc).WherePasses(filter).Select(e => e.Id).ToList();
                if(ids.Count == 0) ids.Add(symId); // If no instances, at least select the symbol
            }
            else if (node.Type == "Material" && node.Tag is List<ElementId> matIds)
            {
                ids = matIds;
            }

            return ids;
        }

        private void DeleteCheckedNodes_Click(object sender, RoutedEventArgs e)
        {
            var ids = GetCheckedElementIds();
            if (ids.Count == 0)
            {
                TaskDialog.Show("提示", "请先在左侧树中勾选要删除的节点。");
                return;
            }
            ExecuteDelete(ids, "勾选的节点");
        }

        private void DeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            var ids = new List<ElementId>();

            if (AssetDataGrid.Visibility == System.Windows.Visibility.Visible)
                ids = AssetDataGrid.SelectedItems.Cast<AssetItemViewModel>().Select(x => x.AssetId).ToList();
            else if (FilledRegionDataGrid.Visibility == System.Windows.Visibility.Visible)
                ids = FilledRegionDataGrid.SelectedItems.Cast<FilledRegionViewModel>().Select(x => x.Id).ToList();
            else if (LineStyleDataGrid.Visibility == System.Windows.Visibility.Visible)
                ids = LineStyleDataGrid.SelectedItems.Cast<LineStyleViewModel>().Select(x => x.Id).ToList();

            if (ids.Count == 0)
            {
                TaskDialog.Show("提示", "请先在列表中高亮选择要删除的资产。\n(如想删除左侧树状图勾选的节点，请在树上右键选择「删除勾选的节点」)");
                return;
            }

            ExecuteDelete(ids, "高亮选中的资产");
        }

        private void ExecuteDelete(List<ElementId> ids, string sourceDesc)
        {
            _handler.SetAction(app =>
            {
                try
                {
                    var doc = app.ActiveUIDocument.Document;
                    int actualDeleteCount = 0;
                    bool proceed = true;

                    using (TransactionGroup tg = new TransactionGroup(doc, "批量删除资产(YANG TOOLS)"))
                    {
                        tg.Start();
                        List<ElementId> extraIds = new List<ElementId>();
                        using (Transaction t1 = new Transaction(doc, "Trial Delete"))
                        {
                            t1.Start();
                            var deleted = doc.Delete(ids);
                            actualDeleteCount = deleted?.Count ?? 0;
                            if (deleted != null) extraIds = deleted.Where(id => !ids.Contains(id)).ToList();
                            t1.RollBack();
                        }

                        if (extraIds.Count > 0)
                        {
                            System.Text.StringBuilder extraDetails = new System.Text.StringBuilder();
                            int count = 0;
                            foreach (var eId in extraIds)
                            {
                                if (count++ >= 15) break;
                                try {
                                    var el = doc.GetElement(eId);
                                    if (el != null) 
                                    {
                                        string catName = el.Category?.Name ?? el.GetType().Name;
                                        extraDetails.AppendLine($"- [{catName}] {el.Name}");
                                    }
                                } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] ProjectAssetManagerWindow.xaml.cs: {0}", ex.Message); }
                            }
                            if (extraIds.Count > 15) extraDetails.AppendLine($"... 及其他 {extraIds.Count - 15} 个图元");

                            var td = new TaskDialog("警告");
                            td.MainInstruction = $"您选择了 {ids.Count} 个{sourceDesc}进行删除，但将连带删除 {extraIds.Count} 个关联图元！";
                            td.MainContent = "这通常是因为删除了它们依赖的宿主（例如删除了视图，关联的标签也会被删）。\n\n部分连带删除的图元如下：\n\n" + extraDetails.ToString();
                            td.ExpandedContent = "如果确认不需要这些连带图元，请点击“是”。否则点击“否”取消删除。";
                            td.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No;
                            td.DefaultButton = TaskDialogResult.No;
                            var result = td.Show();
                            
                            if (result != TaskDialogResult.Yes) proceed = false;
                            else
                            {
                                using (Transaction t2 = new Transaction(doc, "Real Delete"))
                                {
                                    t2.Start();
                                    doc.Delete(ids);
                                    t2.Commit();
                                }
                            }
                        }
                        else
                        {
                            using (Transaction t2 = new Transaction(doc, "Real Delete"))
                            {
                                t2.Start();
                                doc.Delete(ids);
                                t2.Commit();
                            }
                        }

                        if (proceed) tg.Commit();
                        else tg.RollBack();
                    }

                    if (proceed)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            TaskDialog.Show("提示", $"成功删除 {actualDeleteCount} 个图元/资产！\n\n温馨提示：如果您想撤销(Undo)刚才的删除操作，由于当前小窗口占用了您的键盘，请用鼠标【点击一下Revit的绘图区背景】，然后按下 Ctrl+Z 即可撤销。\n您也可以在Revit左上角的快速访问工具栏找到“批量删除资产(YANG TOOLS)”的撤销记录。");
                            
                            var toRemoveAsset = AssetList.Where(a => ids.Contains(a.AssetId)).ToList();
                            foreach (var a in toRemoveAsset) AssetList.Remove(a);

                            var toRemoveFill = FilledRegionList.Where(a => ids.Contains(a.Id)).ToList();
                            foreach (var a in toRemoveFill) FilledRegionList.Remove(a);

                            var toRemoveLine = LineStyleList.Where(a => ids.Contains(a.Id)).ToList();
                            foreach (var a in toRemoveLine) LineStyleList.Remove(a);
                            
                            if (_selectedNode?.Tag is List<ElementId> idList)
                            {
                                idList.RemoveAll(id => ids.Contains(id));
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => TaskDialog.Show("Error", "Delete failed: " + ex.Message));
                }
            }, operationName: "删除图元", showSuccess: false);
            _externalEvent.Raise();
        }

        private void BatchRename_Click(object sender, RoutedEventArgs e)
        {
            var ids = new List<ElementId>();
            
            if (AssetDataGrid.Visibility == System.Windows.Visibility.Visible)
                ids = AssetDataGrid.SelectedItems.Cast<AssetItemViewModel>().Select(x => x.AssetId).ToList();
            else if (FilledRegionDataGrid.Visibility == System.Windows.Visibility.Visible)
                ids = FilledRegionDataGrid.SelectedItems.Cast<FilledRegionViewModel>().Select(x => x.Id).ToList();
            else if (LineStyleDataGrid.Visibility == System.Windows.Visibility.Visible)
                ids = LineStyleDataGrid.SelectedItems.Cast<LineStyleViewModel>().Select(x => x.Id).ToList();

            if (ids.Count == 0)
            {
                TaskDialog.Show("提示", "请先在列表中高亮选择要重命名的资产。 (Please select assets first.)");
                return;
            }
            
            var renameWin = new BatchRenameWindow { Owner = this };
            if (renameWin.ShowDialog() == true)
            {
                string prefix = renameWin.PrefixText;
                string suffix = renameWin.SuffixText;
                string find = renameWin.FindText;
                string replace = renameWin.ReplaceText;
                
                _handler.SetAction(app =>
                {
                    try
                    {
                        var doc = app.ActiveUIDocument.Document;
                        int count = 0;
                        using (Transaction t = new Transaction(doc, "批量重命名资产"))
                        {
                            t.Start();
                            foreach(var id in ids)
                            {
                                var elem = doc.GetElement(id);
                                if(elem != null)
                                {
                                    try {
                                        string oldName = elem.Name;
                                        string newName = oldName;
                                        
                                        if(!string.IsNullOrEmpty(find))
                                            newName = newName.Replace(find, replace ?? "");
                                        
                                        newName = prefix + newName + suffix;
                                        
                                        if(newName != oldName && !string.IsNullOrWhiteSpace(newName))
                                        {
                                            elem.Name = newName;
                                            count++;
                                        }
                                    } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] ProjectAssetManagerWindow.xaml.cs: {0}", ex.Message); }
                                }
                            }
                            t.Commit();
                        }
                        
                        Dispatcher.Invoke(() =>
                        {
                            TaskDialog.Show("提示", $"成功重命名 {count} 个资产！");
                            LoadData();
                            if (FilledRegionDataGrid.Visibility == System.Windows.Visibility.Visible) LoadFilledRegions();
                            if (LineStyleDataGrid.Visibility == System.Windows.Visibility.Visible) LoadLineStyles();
                        });
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() => TaskDialog.Show("Error", "Batch rename failed: " + ex.Message));
                    }
                }, operationName: "批量重命名", showSuccess: false);
                _externalEvent.Raise();
            }
        }
        private void HideCurrentColumn_Click(object sender, RoutedEventArgs e)
        {
            var cellInfo = AssetDataGrid.CurrentCell;
            if (cellInfo.Column != null)
            {
                cellInfo.Column.Visibility = System.Windows.Visibility.Collapsed;
            }
            else if (AssetDataGrid.CurrentColumn != null)
            {
                AssetDataGrid.CurrentColumn.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        private void RestoreHiddenColumns_Click(object sender, RoutedEventArgs e)
        {
            foreach (var col in AssetDataGrid.Columns)
            {
                col.Visibility = System.Windows.Visibility.Visible;
            }
        }
    }

    public class AssetItemViewModel : INotifyPropertyChanged
    {
        private string _assetName;
        public string AssetName 
        { 
            get => _assetName; 
            set { _assetName = value; OnPropertyChanged(nameof(AssetName)); } 
        }
        
        public ElementId AssetId { get; set; }
        public long AssetIdValue => AssetId?.GetIdValue() ?? -1;

        private bool _isRowVisible = true;
        public bool IsRowVisible
        {
            get => _isRowVisible;
            set { _isRowVisible = value; OnPropertyChanged(nameof(IsRowVisible)); }
        }
        
        public Dictionary<string, ParameterViewModel> Parameters { get; set; } = new Dictionary<string, ParameterViewModel>();

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }

    public class AssetTreeNode : INotifyPropertyChanged
    {
        private bool? _isChecked = false;

        public string Name { get; set; }
        public object Tag { get; set; }
        public string Type { get; set; }
        
        public AssetTreeNode Parent { get; set; }

        public bool? IsChecked
        {
            get => _isChecked;
            set => SetIsChecked(value, true, true);
        }

        public void SetIsChecked(bool? value, bool updateChildren, bool updateParent)
        {
            if (value == _isChecked) return;
            _isChecked = value;

            if (updateChildren && _isChecked.HasValue)
            {
                foreach (var child in Children)
                {
                    child.SetIsChecked(_isChecked.Value, true, false);
                }
            }

            if (updateParent && Parent != null)
            {
                Parent.VerifyCheckState();
            }

            OnPropertyChanged(nameof(IsChecked));
        }

        private void VerifyCheckState()
        {
            bool? state = null;
            for (int i = 0; i < Children.Count; ++i)
            {
                bool? current = Children[i].IsChecked;
                if (i == 0) state = current;
                else if (state != current)
                {
                    state = null;
                    break;
                }
            }
            SetIsChecked(state, false, true);
        }

        public ObservableCollection<AssetTreeNode> Children { get; set; } = new ObservableCollection<AssetTreeNode>();

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }

    public class PatternComboItem
    {
        public ElementId Id { get; set; }
        public string Name { get; set; }
        public System.Windows.Media.ImageSource Preview { get; set; }
    }

    public class LineStyleViewModel : INotifyPropertyChanged
    {
        public ElementId Id { get; set; }
        public string Name { get; set; }
        public int LineWeight { get; set; }
        public System.Windows.Media.Brush ColorBrush { get; set; }
        public ElementId LinePatternId { get; set; }
        public bool IsBuiltIn { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public class FilledRegionViewModel : INotifyPropertyChanged
    {
        public ElementId Id { get; set; }
        public string Name { get; set; }
        public System.Windows.Media.Brush ForeColorBrush { get; set; }
        public ElementId ForePatternId { get; set; }
        public System.Windows.Media.Brush BackColorBrush { get; set; }
        public ElementId BackPatternId { get; set; }
        public bool IsMasking { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
