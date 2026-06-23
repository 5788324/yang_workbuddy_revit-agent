using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    public partial class FamilyInstanceManagerWindow : Window
    {
        private UIApplication _uiapp;
        private Document _doc;
        private ExternalEvent _externalEvent;
        private RevitEventHandler _handler;

        public ObservableCollection<InstanceItemViewModel> AllItems { get; set; } = new ObservableCollection<InstanceItemViewModel>();
        private ICollectionView _collectionView;

        public ObservableCollection<InstanceItemViewModel> SystemAllItems { get; set; } = new ObservableCollection<InstanceItemViewModel>();
        private ICollectionView _systemCollectionView;
        private BuiltInCategory _selectedSystemCategory = BuiltInCategory.INVALID;


        private List<string> _parameterColumns = new List<string> { "", "" };
        private string _configPath;

        private ElementId _selectedFamilyId;

        public FamilyInstanceManagerWindow(UIApplication uiapp, ElementId familyId = null)
        {
            InitializeComponent();
            _uiapp = uiapp;
            _doc = uiapp.ActiveUIDocument.Document;

            _handler = new RevitEventHandler();
            _externalEvent = ExternalEvent.Create(_handler);

            PopulateFamilyFilter();
            PopulateSystemCategoryFilter();

            // If a family ID was passed (from Family Manager), pre-select it
            if (familyId != null && familyId != ElementId.InvalidElementId)
            {
                for (int i = 0; i < FamilyFilterComboBox.Items.Count; i++)
                {
                    var item = FamilyFilterComboBox.Items[i] as FamilyFilterItem;
                    if (item != null && item.FamilyId == familyId)
                    {
                        FamilyFilterComboBox.SelectedIndex = i;
                        break;
                    }
                }
            }
        }

        private void PopulateFamilyFilter()
        {
            try
            {
                // Only collect families that have at least one instance in the project
                var familiesWithInstances = new FilteredElementCollector(_doc)
                    .OfClass(typeof(FamilyInstance))
                    .Cast<FamilyInstance>()
                    .Select(i => i.Symbol?.Family)
                    .Where(f => f != null)
                    .GroupBy(f => f.Id)
                    .Select(g => g.First())
                    .Select(f => new FamilyFilterItem
                    {
                        FamilyName = f.Name,
                        FamilyId = f.Id,
                        CategoryName = f.FamilyCategory?.Name ?? "未分类"
                    })
                    .OrderBy(f => f.CategoryName)
                    .ThenBy(f => f.FamilyName)
                    .ToList();

                FamilyFilterComboBox.ItemsSource = familiesWithInstances;
                FamilyFilterComboBox.DisplayMemberPath = "DisplayName";
            }
            catch { }
        }

        private void PopulateSystemCategoryFilter()
        {
            var systemCategories = new List<SystemCategoryItem>
            {
                new SystemCategoryItem("墙 (Walls)", BuiltInCategory.OST_Walls),
                new SystemCategoryItem("楼板 (Floors)", BuiltInCategory.OST_Floors),
                new SystemCategoryItem("天花板 (Ceilings)", BuiltInCategory.OST_Ceilings),
                new SystemCategoryItem("屋顶 (Roofs)", BuiltInCategory.OST_Roofs),
                new SystemCategoryItem("管道 (Pipes)", BuiltInCategory.OST_PipeCurves),
                new SystemCategoryItem("风管 (Ducts)", BuiltInCategory.OST_DuctCurves),
                new SystemCategoryItem("电缆桥架 (Cable Trays)", BuiltInCategory.OST_CableTray),
                new SystemCategoryItem("线管 (Conduits)", BuiltInCategory.OST_Conduit),
                new SystemCategoryItem("楼梯 (Stairs)", BuiltInCategory.OST_Stairs),
                new SystemCategoryItem("栏杆扶手 (Railings)", BuiltInCategory.OST_StairsRailing)
            };
            SystemCategoryComboBox.ItemsSource = systemCategories;
            SystemCategoryComboBox.DisplayMemberPath = "Name";
        }

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl)
            {
                // Refresh data grids when switching tabs to ensure columns are correct
            }
        }

        private void FamilyFilterChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = FamilyFilterComboBox.SelectedItem as FamilyFilterItem;
            if (selected == null)
            {
                AllItems.Clear();
                EmptyStatePanel.Visibility = System.Windows.Visibility.Visible;
                InstanceDataGrid.Visibility = System.Windows.Visibility.Collapsed;
                return;
            }

            _selectedFamilyId = selected.FamilyId;

            // Per-family config path
            string safeName = string.Join("_", selected.FamilyName.Split(System.IO.Path.GetInvalidFileNameChars()));
            _configPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "YangTools", $"InstanceConfig_{safeName}.txt");

            LoadConfig();
            LoadData();
            CreateDynamicColumns();

            EmptyStatePanel.Visibility = System.Windows.Visibility.Collapsed;
            InstanceDataGrid.Visibility = System.Windows.Visibility.Visible;
        }

        private void SystemCategoryChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = SystemCategoryComboBox.SelectedItem as SystemCategoryItem;
            if (selected == null)
            {
                SystemAllItems.Clear();
                SystemEmptyStatePanel.Visibility = System.Windows.Visibility.Visible;
                SystemDataGrid.Visibility = System.Windows.Visibility.Collapsed;
                return;
            }

            _selectedSystemCategory = selected.Category;

            // Share the same config logic but separate file
            _configPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "YangTools", $"SystemInstanceConfig_{_selectedSystemCategory}.txt");

            LoadConfig();
            LoadSystemData();
            CreateSystemDynamicColumns();

            SystemEmptyStatePanel.Visibility = System.Windows.Visibility.Collapsed;
            SystemDataGrid.Visibility = System.Windows.Visibility.Visible;
        }

        private void LoadConfig()
        {
            if (string.IsNullOrEmpty(_configPath))
            {
                _parameterColumns = new List<string> { "", "" };
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
            catch { }
        }

        private void CreateDynamicColumns()
        {
            // Remove all dynamic columns
            var colsToRemove = InstanceDataGrid.Columns.Where(c =>
            {
                var h = c.Header;
                return h is System.Windows.Controls.ComboBox || h is System.Windows.Controls.Button;
            }).ToList();
            foreach (var c in colsToRemove) InstanceDataGrid.Columns.Remove(c);

            // Collect available parameters from instances of the selected family
            List<AvailableParamItem> availableParams;
            try
            {
                var dict = new Dictionary<string, bool>();
                foreach (var item in AllItems)
                {
                    try
                    {
                        var inst = _doc.GetElement(item.InstanceId) as FamilyInstance;
                        if (inst == null) continue;
                        foreach (Parameter param in inst.GetOrderedParameters())
                        {
                            try
                            {
                                string name = param.Definition?.Name;
                                if (!string.IsNullOrEmpty(name)) dict[name] = param.IsReadOnly;
                            }
                            catch { }
                        }
                        break; // Only need one instance to get the parameter list
                    }
                    catch { }
                }
                availableParams = dict.Select(kv => new AvailableParamItem { Name = kv.Key, IsReadOnly = kv.Value })
                    .OrderBy(x => x.Name).ToList();
            }
            catch
            {
                availableParams = new List<AvailableParamItem>();
            }

            for (int colIndex = 0; colIndex < _parameterColumns.Count; colIndex++)
            {
                string pName = _parameterColumns[colIndex];
                int capturedIndex = colIndex;

                var col = new DataGridTextColumn();
                col.Width = new DataGridLength(1, DataGridLengthUnitType.Auto);

                var combo = new System.Windows.Controls.ComboBox();
                combo.ItemsSource = availableParams;
                combo.SelectedValuePath = "Name";
                if (!string.IsNullOrEmpty(pName)) combo.SelectedValue = pName;
                combo.Width = 120;
                combo.Margin = new Thickness(0, 0, 5, 0);

                // Template: gray text for read-only params
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
                    catch { }
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

                InstanceDataGrid.Columns.Add(col);
            }

            // Add "+" column
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
            };
            addCol.Header = addBtn;
            addCol.IsReadOnly = true;
            addCol.Width = new DataGridLength(1, DataGridLengthUnitType.Auto);
            InstanceDataGrid.Columns.Add(addCol);
        }

        private void CreateSystemDynamicColumns()
        {
            var colsToRemove = SystemDataGrid.Columns.Where(c =>
            {
                var h = c.Header;
                return h is System.Windows.Controls.ComboBox || h is System.Windows.Controls.Button;
            }).ToList();
            foreach (var c in colsToRemove) SystemDataGrid.Columns.Remove(c);

            List<AvailableParamItem> availableParams;
            try
            {
                var dict = new Dictionary<string, bool>();
                foreach (var item in SystemAllItems)
                {
                    try
                    {
                        var inst = _doc.GetElement(item.InstanceId);
                        if (inst == null) continue;
                        foreach (Parameter param in inst.GetOrderedParameters())
                        {
                            try
                            {
                                string name = param.Definition?.Name;
                                if (!string.IsNullOrEmpty(name)) dict[name] = param.IsReadOnly;
                            }
                            catch { }
                        }
                        break; 
                    }
                    catch { }
                }
                availableParams = dict.Select(kv => new AvailableParamItem { Name = kv.Key, IsReadOnly = kv.Value })
                    .OrderBy(x => x.Name).ToList();
            }
            catch
            {
                availableParams = new List<AvailableParamItem>();
            }

            for (int colIndex = 0; colIndex < _parameterColumns.Count; colIndex++)
            {
                string pName = _parameterColumns[colIndex];
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
                            CreateSystemDynamicColumns();
                            LoadSystemData();
                        }
                    }
                    catch { }
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

                SystemDataGrid.Columns.Add(col);
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
                CreateSystemDynamicColumns();
            };
            addCol.Header = addBtn;
            addCol.IsReadOnly = true;
            addCol.Width = new DataGridLength(1, DataGridLengthUnitType.Auto);
            SystemDataGrid.Columns.Add(addCol);
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

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) this.DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void LoadData()
        {
            AllItems.Clear();

            if (_selectedFamilyId == null || _selectedFamilyId == ElementId.InvalidElementId) return;

            try
            {
                var family = _doc.GetElement(_selectedFamilyId) as Family;
                if (family == null) return;

                bool currentViewOnly = _filterBySelection; // if _filterBySelection is true, we already filtered the list below

                FilteredElementCollector collector;
                if (currentViewOnly && _doc.ActiveView != null)
                    collector = new FilteredElementCollector(_doc, _doc.ActiveView.Id);
                else
                    collector = new FilteredElementCollector(_doc);

                var instances = collector
                    .OfClass(typeof(FamilyInstance))
                    .Cast<FamilyInstance>()
                    .Where(inst =>
                    {
                        try { return inst.Symbol?.Family?.Id == _selectedFamilyId; }
                        catch { return false; }
                    })
                    .ToList();

                if (_filterBySelection && _selectedElementIds != null && _selectedElementIds.Count > 0)
                {
                    instances = instances.Where(i => _selectedElementIds.Contains(i.Id)).ToList();
                }

                foreach (var inst in instances)
                {
                    try
                    {
                        var vm = new InstanceItemViewModel
                        {
                            Category = inst.Category?.Name ?? "",
                            FamilyName = inst.Symbol?.Family?.Name ?? "",
                            TypeName = inst.Symbol?.Name ?? "",
                            InstanceId = inst.Id
                        };

                        foreach (var pName in _parameterColumns)
                        {
                            if (string.IsNullOrEmpty(pName)) continue;
                            try
                            {
                                var pvm = new ParameterViewModel { Value = "", IsReadOnly = true };
                                var param = inst.LookupParameter(pName);
                                if (param != null)
                                {
                                    pvm.Value = GetParameterValueSafely(param);
                                    pvm.IsReadOnly = param.IsReadOnly;
                                }
                                vm.Parameters[pName] = pvm;
                            }
                            catch { }
                        }
                        AllItems.Add(vm);
                    }
                    catch { }
                }
            }
            catch { }

            _collectionView = CollectionViewSource.GetDefaultView(AllItems);
            _collectionView.GroupDescriptions.Clear();
            _collectionView.GroupDescriptions.Add(new PropertyGroupDescription("TypeName"));
            _collectionView.Filter = FilterItem;
            InstanceDataGrid.ItemsSource = _collectionView;
        }

        private void LoadSystemData()
        {
            SystemAllItems.Clear();

            if (_selectedSystemCategory == BuiltInCategory.INVALID) return;

            try
            {
                bool currentViewOnly = _filterBySelection; 

                FilteredElementCollector collector;
                if (currentViewOnly && _doc.ActiveView != null)
                    collector = new FilteredElementCollector(_doc, _doc.ActiveView.Id);
                else
                    collector = new FilteredElementCollector(_doc);

                // Use OfCategory to get system families
                var instances = collector
                    .OfCategory(_selectedSystemCategory)
                    .WhereElementIsNotElementType()
                    .ToList();

                if (_filterBySelection && _selectedElementIds != null && _selectedElementIds.Count > 0)
                {
                    instances = instances.Where(i => _selectedElementIds.Contains(i.Id)).ToList();
                }

                foreach (var inst in instances)
                {
                    try
                    {
                        var vm = new InstanceItemViewModel
                        {
                            Category = inst.Category?.Name ?? "",
                            FamilyName = "系统族",
                            TypeName = inst.Name ?? "",
                            InstanceId = inst.Id
                        };

                        foreach (var pName in _parameterColumns)
                        {
                            if (string.IsNullOrEmpty(pName)) continue;
                            try
                            {
                                var pvm = new ParameterViewModel { Value = "", IsReadOnly = true };
                                var param = inst.LookupParameter(pName);
                                if (param != null)
                                {
                                    pvm.Value = GetParameterValueSafely(param);
                                    pvm.IsReadOnly = param.IsReadOnly;
                                }
                                vm.Parameters[pName] = pvm;
                            }
                            catch { }
                        }
                        SystemAllItems.Add(vm);
                    }
                    catch { }
                }
            }
            catch { }

            _systemCollectionView = CollectionViewSource.GetDefaultView(SystemAllItems);
            _systemCollectionView.GroupDescriptions.Clear();
            _systemCollectionView.GroupDescriptions.Add(new PropertyGroupDescription("TypeName"));
            _systemCollectionView.Filter = FilterSystemItem;
            SystemDataGrid.ItemsSource = _systemCollectionView;
        }

        private bool FilterSystemItem(object obj)
        {
            if (obj is InstanceItemViewModel item)
            {
                string searchText = SystemSearchBox.Text?.Trim().ToLower() ?? "";
                if (!string.IsNullOrEmpty(searchText))
                {
                    if (!item.TypeName.ToLower().Contains(searchText) &&
                        !item.InstanceId.GetIdValue().ToString().Contains(searchText))
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }

        private bool FilterItem(object obj)
        {
            if (obj is InstanceItemViewModel item)
            {
                string searchText = SearchBox.Text?.Trim().ToLower() ?? "";
                if (!string.IsNullOrEmpty(searchText))
                {
                    if (!item.TypeName.ToLower().Contains(searchText) &&
                        !item.InstanceId.GetIdValue().ToString().Contains(searchText))
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }

        private bool _filterBySelection = false;
        private ICollection<ElementId> _selectedElementIds;

        public void SetSelectionFilter(ICollection<ElementId> selectedIds)
        {
            if (selectedIds != null && selectedIds.Count > 0)
            {
                _filterBySelection = true;
                _selectedElementIds = selectedIds;

                // Auto select the family of the first instance
                try
                {
                    var firstInst = _doc.GetElement(selectedIds.First()) as FamilyInstance;
                    if (firstInst != null && firstInst.Symbol != null && firstInst.Symbol.Family != null)
                    {
                        var famId = firstInst.Symbol.Family.Id;
                        for (int i = 0; i < FamilyFilterComboBox.Items.Count; i++)
                        {
                            var item = FamilyFilterComboBox.Items[i] as FamilyFilterItem;
                            if (item != null && item.FamilyId == famId)
                            {
                                FamilyFilterComboBox.SelectedIndex = i;
                                break;
                            }
                        }
                    }
                }
                catch { }

                LoadData();
                CreateDynamicColumns();
            }
        }

        private void SystemSearchChanged(object sender, TextChangedEventArgs e)
        {
            if (_systemCollectionView != null) _systemCollectionView.Refresh();
        }

        private void SelectElements_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            _handler.SetAction(app =>
            {
                try
                {
                    var doc = app.ActiveUIDocument.Document;
                    var sel = app.ActiveUIDocument.Selection;
                    var picked = sel.PickObjects(Autodesk.Revit.UI.Selection.ObjectType.Element, "请选择要管理的族实例 (Select instances)");
                    
                    if (picked != null && picked.Count > 0)
                    {
                        var ids = picked.Select(p => p.ElementId).ToList();
                        Dispatcher.Invoke(() =>
                        {
                            SetSelectionFilter(ids);
                            this.ShowDialog();
                        });
                    }
                    else
                    {
                        Dispatcher.Invoke(() => this.ShowDialog());
                    }
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    // User canceled selection
                    Dispatcher.Invoke(() => this.ShowDialog());
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => {
                        TaskDialog.Show("Error", ex.Message);
                        this.ShowDialog();
                    });
                }
            });
            _externalEvent.Raise();
        }

        private void ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            bool isSystem = MainTabControl.SelectedIndex == 1;
            var activeItems = isSystem ? SystemAllItems : AllItems;
            
            if (activeItems.Count == 0) return;
            var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Excel File (*.xlsx)|*.xlsx", FileName = "Instances.xlsx" };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var data = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object>>();
                    foreach (var item in activeItems)
                    {
                        var row = new System.Collections.Generic.Dictionary<string, object>();
                        row["图元ID"] = item.InstanceId.GetIdValue().ToString();
                        row["类型 (Type)"] = item.TypeName;
                        
                        foreach (var col in _parameterColumns)
                        {
                            if (string.IsNullOrEmpty(col)) continue;
                            row[col] = item.Parameters.ContainsKey(col) ? item.Parameters[col].Value : "";
                        }
                        data.Add(row);
                    }
                    MiniExcelLibs.MiniExcel.SaveAs(dialog.FileName, data);
                    TaskDialog.Show("提示", "导出成功 (Export Successful)");
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
                        var rows = System.Linq.Enumerable.Cast<System.Collections.Generic.IDictionary<string, object>>(MiniExcelLibs.MiniExcel.Query(dialog.FileName, useHeaderRow: true));
                        var doc = app.ActiveUIDocument.Document;
                        int successCount = 0;

                        using (Transaction t = new Transaction(doc, "批量导入实例参数"))
                        {
                            t.Start();
                            foreach (var row in rows)
                            {
                                if (!row.ContainsKey("图元ID") || row["图元ID"] == null || string.IsNullOrEmpty(row["图元ID"].ToString())) continue;
                                
                                if (long.TryParse(row["图元ID"].ToString(), out long idVal))
                                {
                                    ElementId eId = YangTools.Revit.Core.ElementIdExtensions.CreateId(idVal);
                                    var elem = doc.GetElement(eId);
                                    if (elem == null) continue;

                                    foreach (var kvp in row)
                                    {
                                        if (kvp.Key == "图元ID" || kvp.Key == "类型 (Type)" || kvp.Value == null) continue;
                                        
                                        var p = elem.LookupParameter(kvp.Key);
                                        if (p != null && !p.IsReadOnly)
                                        {
                                            try
                                            {
                                                switch (p.StorageType)
                                                {
                                                    case StorageType.String: p.Set(kvp.Value.ToString()); break;
                                                    case StorageType.Integer: if (int.TryParse(kvp.Value.ToString(), out int iVal)) p.Set(iVal); break;
                                                    case StorageType.Double: if (double.TryParse(kvp.Value.ToString(), out double dVal)) p.Set(dVal); break;
                                                }
                                            } catch { }
                                        }
                                    }
                                    successCount++;
                                }
                            }
                            t.Commit();
                        }

                        Dispatcher.Invoke(() =>
                        {
                            TaskDialog.Show("提示", $"成功更新了 {successCount} 个图元的参数！");
                            if (MainTabControl.SelectedIndex == 1) LoadSystemData();
                            else LoadData();
                        });
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() => TaskDialog.Show("错误", "Import failed: " + ex.Message));
                    }
                });
                _externalEvent.Raise();
            }
        }

        private void SearchChanged(object sender, TextChangedEventArgs e)
        {
            if (_collectionView != null) _collectionView.Refresh();
        }



        private void InstanceDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit) return;
            try
            {
                var vm = e.Row.Item as InstanceItemViewModel;
                var el = e.EditingElement as System.Windows.Controls.TextBox;
                if (vm == null || el == null) return;
                var newValue = el.Text;

                var bindingExpr = el.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty);
                var bindingPath = (bindingExpr?.ParentBinding as System.Windows.Data.Binding)?.Path?.Path;
                if (string.IsNullOrEmpty(bindingPath)) return;

                if (bindingPath.StartsWith("Parameters[") && bindingPath.EndsWith("].Value") && bindingPath.Length > 19)
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
                            var inst = doc.GetElement(vm.InstanceId);
                            if (inst == null) return;
                            var param = inst.LookupParameter(pName);
                            if (param == null || param.IsReadOnly) return;

                            bool success = false;
                            try
                            {
                                using (Transaction t = new Transaction(doc, "Set Instance Parameter"))
                                {
                                    t.Start();
                                    if (param.StorageType == StorageType.String) param.Set(newValue);
                                    else if (param.StorageType == StorageType.Double && double.TryParse(newValue, out double d)) param.Set(d);
                                    else if (param.StorageType == StorageType.Integer && int.TryParse(newValue, out int i)) param.Set(i);
                                    else if (param.StorageType != StorageType.ElementId)
                                    {
                                        try { param.SetValueString(newValue); } catch { }
                                    }
                                    t.Commit();
                                    success = true;
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
                        catch { }
                    });
                    _externalEvent.Raise();
                }
            }
            catch { }
        }

        private void SystemDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            InstanceDataGrid_CellEditEnding(sender, e);
        }

        private void ResetColumns_Click(object sender, RoutedEventArgs e)
        {
            _parameterColumns = new List<string> { "", "" };
            SaveConfig();
            
            if (MainTabControl.SelectedIndex == 1)
            {
                CreateSystemDynamicColumns();
                LoadSystemData();
            }
            else
            {
                CreateDynamicColumns();
                if (_selectedFamilyId != null) LoadData();
            }
        }

        private void SelectInView_Click(object sender, RoutedEventArgs e)
        {
            bool isSystem = MainTabControl.SelectedIndex == 1;
            var activeDataGrid = isSystem ? SystemDataGrid : InstanceDataGrid;
            
            var selectedItems = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Cast<InstanceItemViewModel>(activeDataGrid.SelectedItems));
            if (selectedItems.Count == 0) return;
            
            var ids = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(System.Linq.Enumerable.Where(selectedItems, x => x.InstanceId != ElementId.InvalidElementId), x => x.InstanceId));
            _handler.SetAction(app =>
            {
                var uidoc = app.ActiveUIDocument;
                uidoc.Selection.SetElementIds(ids);
                uidoc.ShowElements(ids);
            });
            _externalEvent.Raise();
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            bool isSystem = MainTabControl.SelectedIndex == 1;
            var activeDataGrid = isSystem ? SystemDataGrid : InstanceDataGrid;

            var selectedItems = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Cast<InstanceItemViewModel>(activeDataGrid.SelectedItems));
            if (selectedItems.Count == 0) return;

            var ids = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(selectedItems, x => x.InstanceId));
            ExecuteDelete(ids, "实例");
        }

        private void ExecuteDelete(System.Collections.Generic.List<ElementId> ids, string sourceDesc)
        {
            _handler.SetAction(app =>
            {
                try
                {
                    var doc = app.ActiveUIDocument.Document;
                    int actualDeleteCount = 0;
                    bool proceed = true;

                    using (TransactionGroup tg = new TransactionGroup(doc, "级联删除(YANG TOOLS)"))
                    {
                        tg.Start();
                        System.Collections.Generic.List<ElementId> extraIds = new System.Collections.Generic.List<ElementId>();
                        using (Transaction t1 = new Transaction(doc, "Trial Delete"))
                        {
                            t1.Start();
                            var deleted = doc.Delete(ids);
                            actualDeleteCount = deleted?.Count ?? 0;
                            if (deleted != null) extraIds = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(deleted, id => !ids.Contains(id)));
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
                                } catch {}
                            }
                            if (extraIds.Count > 15) extraDetails.AppendLine($"... 及其他 {extraIds.Count - 15} 个图元");

                            var td = new TaskDialog("警告");
                            td.MainInstruction = $"您选择的 {ids.Count} 个{sourceDesc}将连带删除 {extraIds.Count} 个图元！";
                            td.MainContent = "这通常是因为删除了主体，导致依附于其上的标记或其他图元也被连带删除\n\n连带删除的图元如下：\n\n" + extraDetails.ToString();
                            td.ExpandedContent = "请确认您是否要连带这些图元一起删除？";
                            td.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No;
                            td.DefaultButton = TaskDialogResult.No;
                            var result = td.Show();
                            
                            if (result != TaskDialogResult.Yes) proceed = false;
                        }

                        if (proceed)
                        {
                            using (Transaction t2 = new Transaction(doc, "Execute Delete"))
                            {
                                t2.Start();
                                doc.Delete(ids);
                                t2.Commit();
                            }
                            tg.Assimilate();
                            Dispatcher.Invoke(() => 
                            {
                                if (MainTabControl.SelectedIndex == 1) LoadSystemData();
                                else LoadData();
                            });
                        }
                        else
                        {
                            tg.RollBack();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => TaskDialog.Show("错误", "删除失败: " + ex.Message));
                }
            });
            _externalEvent.Raise();
        }
    }

    public class FamilyFilterItem
    {
        public string FamilyName { get; set; }
        public string CategoryName { get; set; }
        public ElementId FamilyId { get; set; }
        public string DisplayName => $"{CategoryName} - {FamilyName}";
    }

    public class SystemCategoryItem
    {
        public string Name { get; set; }
        public BuiltInCategory Category { get; set; }

        public SystemCategoryItem(string name, BuiltInCategory category)
        {
            Name = name;
            Category = category;
        }
    }

    public class InstanceItemViewModel : INotifyPropertyChanged
    {
        public string Category { get; set; }
        public string FamilyName { get; set; }
        public string TypeName { get; set; }
        public string FamilyAndType => $"{FamilyName} - {TypeName}";
        public ElementId InstanceId { get; set; }

        public Dictionary<string, ParameterViewModel> Parameters { get; set; } = new Dictionary<string, ParameterViewModel>();

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
