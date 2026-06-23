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
    public partial class FamilyManagerWindow : Window
    {
        private UIApplication _uiapp;
        private Document _doc;
        private ExternalEvent _externalEvent;
        private RevitEventHandler _handler;

        public ObservableCollection<FamilyListItemViewModel> FamilyList { get; set; } = new ObservableCollection<FamilyListItemViewModel>();
        public ObservableCollection<TypeItemViewModel> TypeList { get; set; } = new ObservableCollection<TypeItemViewModel>();
        private ICollectionView _familyCollectionView;

        private List<string> _parameterColumns = new List<string> { "", "" };
        private string _configPath;
        private ElementId _selectedFamilyId;

        public FamilyManagerWindow(UIApplication uiapp)
        {
            InitializeComponent();
            _uiapp = uiapp;
            _doc = uiapp.ActiveUIDocument.Document;

            _handler = new RevitEventHandler();
            _externalEvent = ExternalEvent.Create(_handler);

            LoadFamilyList();
            PopulateCategoryFilter();
        }

        private void PopulateCategoryFilter()
        {
            var categories = FamilyList.Select(x => x.Category).Distinct().OrderBy(x => x).ToList();
            categories.Insert(0, "所有类别 (All)");
            CategoryFilterComboBox.ItemsSource = categories;
            CategoryFilterComboBox.SelectedIndex = 0;
        }

        private void LoadFamilyList()
        {
            FamilyList.Clear();
            try
            {
                var families = new FilteredElementCollector(_doc)
                    .OfClass(typeof(Family))
                    .Cast<Family>()
                    .Where(f => f.IsEditable)
                    .ToList();

                foreach (var f in families)
                {
                    var symbolIds = f.GetFamilySymbolIds();
                    var vm = new FamilyListItemViewModel
                    {
                        Category = f.FamilyCategory?.Name ?? "未知",
                        FamilyName = f.Name,
                        FamilyId = f.Id,
                        TypeCount = symbolIds.Count
                    };
                    FamilyList.Add(vm);
                }
            }
            catch { }

            _familyCollectionView = CollectionViewSource.GetDefaultView(FamilyList);
            _familyCollectionView.GroupDescriptions.Clear();
            _familyCollectionView.GroupDescriptions.Add(new PropertyGroupDescription("Category"));
            _familyCollectionView.Filter = FilterFamilyItem;
            FamilyListBox.ItemsSource = _familyCollectionView;
        }

        private bool FilterFamilyItem(object obj)
        {
            if (obj is FamilyListItemViewModel item)
            {
                if (CategoryFilterComboBox.SelectedIndex > 0)
                {
                    if (item.Category != CategoryFilterComboBox.SelectedItem.ToString())
                        return false;
                }

                string searchText = SearchBox.Text?.Trim().ToLower() ?? "";
                if (!string.IsNullOrEmpty(searchText))
                {
                    if (!item.FamilyName.ToLower().Contains(searchText))
                        return false;
                }
                return true;
            }
            return false;
        }

        private void FilterChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_familyCollectionView != null) _familyCollectionView.Refresh();
        }
        
        private void FilterChanged(object sender, TextChangedEventArgs e)
        {
            if (_familyCollectionView != null) _familyCollectionView.Refresh();
        }

        private void FamilyListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = FamilyListBox.SelectedItem as FamilyListItemViewModel;
            if (selected == null)
            {
                TypeList.Clear();
                EmptyStatePanel.Visibility = System.Windows.Visibility.Visible;
                TypeDataGrid.Visibility = System.Windows.Visibility.Collapsed;
                SelectedFamilyTitle.Text = "请在左侧选择一个族";
                RenameFamilyBtn.Visibility = System.Windows.Visibility.Collapsed;
                _selectedFamilyId = null;
                return;
            }

            _selectedFamilyId = selected.FamilyId;
            SelectedFamilyTitle.Text = $"族: {selected.FamilyName} ({selected.Category})";
            RenameFamilyBtn.Visibility = System.Windows.Visibility.Visible;

            string safeName = string.Join("_", selected.FamilyName.Split(System.IO.Path.GetInvalidFileNameChars()));
            _configPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "YangTools", $"FamilyConfig_{safeName}.txt");

            LoadConfig();
            LoadTypeData();
            CreateDynamicColumns();

            EmptyStatePanel.Visibility = System.Windows.Visibility.Collapsed;
            TypeDataGrid.Visibility = System.Windows.Visibility.Visible;
        }

        private void LoadConfig()
        {
            if (string.IsNullOrEmpty(_configPath)) return;
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
            var colsToRemove = TypeDataGrid.Columns.Where(c =>
            {
                var h = c.Header;
                return h is System.Windows.Controls.ComboBox || h is System.Windows.Controls.Button;
            }).ToList();
            foreach (var c in colsToRemove) TypeDataGrid.Columns.Remove(c);

            List<AvailableParamItem> availableParams;
            try
            {
                var dict = new Dictionary<string, bool>();
                if (_selectedFamilyId != null && _selectedFamilyId != ElementId.InvalidElementId)
                {
                    var family = _doc.GetElement(_selectedFamilyId) as Family;
                    if (family != null)
                    {
                        var symbolIds = family.GetFamilySymbolIds();
                        if (symbolIds.Count > 0)
                        {
                            var type = _doc.GetElement(symbolIds.First()) as FamilySymbol;
                            if (type != null)
                            {
                                foreach (Parameter param in type.GetOrderedParameters())
                                {
                                    try
                                    {
                                        string name = param.Definition?.Name;
                                        if (!string.IsNullOrEmpty(name)) dict[name] = param.IsReadOnly;
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
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
                            CreateDynamicColumns();
                            LoadTypeData();
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

                TypeDataGrid.Columns.Add(col);
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
            };
            addCol.Header = addBtn;
            addCol.IsReadOnly = true;
            addCol.Width = new DataGridLength(1, DataGridLengthUnitType.Auto);
            TypeDataGrid.Columns.Add(addCol);
        }

        private void LoadTypeData()
        {
            TypeList.Clear();
            if (_selectedFamilyId == null || _selectedFamilyId == ElementId.InvalidElementId) return;

            try
            {
                var family = _doc.GetElement(_selectedFamilyId) as Family;
                if (family == null) return;

                var symbolIds = family.GetFamilySymbolIds();
                foreach (var symId in symbolIds)
                {
                    var sym = _doc.GetElement(symId) as FamilySymbol;
                    if (sym != null)
                    {
                        var vm = new TypeItemViewModel
                        {
                            TypeName = sym.Name,
                            TypeId = sym.Id,
                            FamilyId = family.Id
                        };

                        foreach (var pName in _parameterColumns)
                        {
                            if (string.IsNullOrEmpty(pName)) continue;
                            try
                            {
                                var pvm = new ParameterViewModel { Value = "", IsReadOnly = true };
                                var param = sym.LookupParameter(pName);
                                if (param != null)
                                {
                                    pvm.Value = GetParameterValueSafely(param);
                                    pvm.IsReadOnly = param.IsReadOnly;
                                }
                                vm.Parameters[pName] = pvm;
                            }
                            catch { }
                        }
                        TypeList.Add(vm);
                    }
                }
            }
            catch { }

            TypeDataGrid.ItemsSource = TypeList;
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

        private void TypeDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit) return;
            try
            {
                var vm = e.Row.Item as TypeItemViewModel;
                var el = e.EditingElement as System.Windows.Controls.TextBox;
                if (vm == null || el == null) return;
                var newValue = el.Text;

                var bindingExpr = el.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty);
                var bindingPath = (bindingExpr?.ParentBinding as System.Windows.Data.Binding)?.Path?.Path;
                if (string.IsNullOrEmpty(bindingPath)) return;

                if (bindingPath == "TypeName")
                {
                    var oldTypeName = vm.TypeName;
                    if (newValue == oldTypeName) return;
                    if (vm.TypeId == ElementId.InvalidElementId) return;

                    _handler.SetAction(app =>
                    {
                        try
                        {
                            var doc = app.ActiveUIDocument.Document;
                            var type = doc.GetElement(vm.TypeId) as ElementType;
                            if (type == null) return;
                            bool success = false;
                            try
                            {
                                using (Transaction t = new Transaction(doc, "Rename Type"))
                                {
                                    t.Start();
                                    type.Name = newValue;
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
                                if (success) vm.TypeName = newValue;
                                else vm.TypeName = oldTypeName;
                            });
                        }
                        catch { }
                    });
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
                            var type = doc.GetElement(vm.TypeId) as ElementType;
                            if (type == null) return;
                            var param = type.LookupParameter(pName);
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

        private void ResetColumns_Click(object sender, RoutedEventArgs e)
        {
            _parameterColumns = new List<string> { "", "" };
            SaveConfig();
            CreateDynamicColumns();
            if (_selectedFamilyId != null) LoadTypeData();
        }

        private void ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            if (TypeList.Count == 0) return;
            var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Excel File (*.xlsx)|*.xlsx", FileName = "FamilyTypes.xlsx" };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var data = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object>>();
                    foreach (var item in TypeList)
                    {
                        var row = new System.Collections.Generic.Dictionary<string, object>();
                        row["类型名称 (Type)"] = item.TypeName;
                        row["图元ID"] = item.TypeId.ToString();
                        
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

                        using (Transaction t = new Transaction(doc, "批量导入参数"))
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

                                    if (row.ContainsKey("类型名称 (Type)") && row["类型名称 (Type)"] != null)
                                    {
                                        string newName = row["类型名称 (Type)"].ToString();
                                        if (elem.Name != newName && !string.IsNullOrWhiteSpace(newName))
                                        {
                                            try { elem.Name = newName; } catch { }
                                        }
                                    }

                                    foreach (var kvp in row)
                                    {
                                        if (kvp.Key == "图元ID" || kvp.Key == "类型名称 (Type)" || kvp.Value == null) continue;
                                        
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
                            TaskDialog.Show("提示", $"成功更新了 {successCount} 个类型的参数！");
                            if (_selectedFamilyId != null) LoadTypeData();
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

        private void BatchRename_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Cast<TypeItemViewModel>(TypeDataGrid.SelectedItems));
            if (selectedItems.Count == 0)
            {
                TaskDialog.Show("提示", "请先在右侧列表中选择要重命名的族类型。");
                return;
            }
            
            var renameWin = new BatchRenameWindow { Owner = this };
            if (renameWin.ShowDialog() == true)
            {
                string prefix = renameWin.PrefixText;
                string suffix = renameWin.SuffixText;
                string find = renameWin.FindText;
                string replace = renameWin.ReplaceText;
                
                var ids = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(System.Linq.Enumerable.Where(selectedItems, x => x.TypeId != ElementId.InvalidElementId), x => x.TypeId));
                
                _handler.SetAction(app =>
                {
                    try
                    {
                        var doc = app.ActiveUIDocument.Document;
                        int count = 0;
                        using (Transaction t = new Transaction(doc, "批量重命名族类型"))
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
                                    } catch { }
                                }
                            }
                            
                            if (renameWin.ApplyToFamily)
                            {
                                var families = ids.Select(id => (doc.GetElement(id) as FamilySymbol)?.Family)
                                                  .Where(f => f != null)
                                                  .GroupBy(f => f.Id).Select(g => g.First()).ToList();
                                foreach (var fam in families)
                                {
                                    try
                                    {
                                        string oldName = fam.Name;
                                        string newName = oldName;
                                        if(!string.IsNullOrEmpty(find))
                                            newName = newName.Replace(find, replace ?? "");
                                        newName = prefix + newName + suffix;
                                        if(newName != oldName && !string.IsNullOrWhiteSpace(newName))
                                        {
                                            fam.Name = newName;
                                        }
                                    }
                                    catch { }
                                }
                            }
                            
                            t.Commit();
                        }
                        
                        Dispatcher.Invoke(() =>
                        {
                            TaskDialog.Show("提示", $"成功重命名 {count} 个族类型！");
                            if (_selectedFamilyId != null) LoadTypeData();
                        });
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() => TaskDialog.Show("错误", "Batch rename failed: " + ex.Message));
                    }
                });
                _externalEvent.Raise();
            }
        }

        private void RenameFamily_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFamilyId == null || _selectedFamilyId == ElementId.InvalidElementId) return;

            var selected = FamilyListBox.SelectedItem as FamilyListItemViewModel;
            if (selected == null) return;

            var inputDialog = new SimpleInputDialog("重命名族", "请输入新的族名称：", selected.FamilyName) { Owner = this };
            if (inputDialog.ShowDialog() == true)
            {
                string newName = inputDialog.InputText;
                if (string.IsNullOrWhiteSpace(newName) || newName == selected.FamilyName) return;

                _handler.SetAction(app =>
                {
                    var doc = app.ActiveUIDocument.Document;
                    try
                    {
                        var fam = doc.GetElement(_selectedFamilyId) as Family;
                        if (fam == null) return;

                        bool success = false;
                        using (Transaction t = new Transaction(doc, "重命名族"))
                        {
                            t.Start();
                            fam.Name = newName;
                            t.Commit();
                            success = true;
                        }

                        if (success)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                LoadFamilyList();
                                PopulateCategoryFilter();
                                TypeList.Clear();
                                TypeDataGrid.Visibility = System.Windows.Visibility.Collapsed;
                                EmptyStatePanel.Visibility = System.Windows.Visibility.Visible;
                                SelectedFamilyTitle.Text = "请在左侧选择一个族";
                                RenameFamilyBtn.Visibility = System.Windows.Visibility.Collapsed;
                                _selectedFamilyId = null;
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() => TaskDialog.Show("Error", "重命名失败: " + ex.Message));
                    }
                });
                _externalEvent.Raise();
            }
        }

        private void Duplicate_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = TypeDataGrid.SelectedItems.Cast<TypeItemViewModel>().ToList();
            if (selectedItems.Count != 1)
            {
                TaskDialog.Show("提示", "请选择且仅选择一个类型进行复制。 (Please select exactly one type to duplicate.)");
                return;
            }

            var vm = selectedItems[0];
            if (vm.TypeId == ElementId.InvalidElementId) return;

            string newName = vm.TypeName + "_Copy";

            _handler.SetAction(app =>
            {
                var doc = app.ActiveUIDocument.Document;
                var type = doc.GetElement(vm.TypeId) as ElementType;
                if (type == null) return;

                try
                {
                    ElementType newType = null;
                    using (Transaction t = new Transaction(doc, "Duplicate Type"))
                    {
                        t.Start();
                        newType = type.Duplicate(newName);
                        t.Commit();
                    }

                    if (newType != null)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            LoadTypeData();
                            // Refresh family list to update counts
                            var currentFamily = FamilyList.FirstOrDefault(x => x.FamilyId == _selectedFamilyId);
                            if (currentFamily != null) {
                                currentFamily.TypeCount++;
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => TaskDialog.Show("Error", "Could not duplicate: " + ex.Message));
                }
            });

            _externalEvent.Raise();
        }

        private void ChangeCategory_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFamilyId == null || _selectedFamilyId == ElementId.InvalidElementId) return;

            var categories = new List<CategoryItem>();
            foreach (Category cat in _doc.Settings.Categories)
            {
                if ((cat.CategoryType == CategoryType.Model || cat.CategoryType == CategoryType.Annotation) && cat.CanAddSubcategory)
                {
                    categories.Add(new CategoryItem { Name = cat.Name, Id = cat.Id });
                }
            }
            categories = categories.OrderBy(c => c.Name).ToList();

            var selectWindow = new ParameterSelectWindow(categories.Select(c => c.Name).ToList()) { Title = "选择新类别", Owner = this };
            if (selectWindow.ShowDialog() == true && selectWindow.SelectedParameters.Count > 0)
            {
                var targetCatName = selectWindow.SelectedParameters[0];
                var targetCatItem = categories.FirstOrDefault(c => c.Name == targetCatName);
                if (targetCatItem == null) return;

                var catItemId = targetCatItem.Id;

                _handler.SetAction(app =>
                {
                    var doc = app.ActiveUIDocument.Document;
                    try
                    {
                        var fam = doc.GetElement(_selectedFamilyId) as Family;
                        if (fam == null || !fam.IsEditable) return;

                        Document familyDoc = doc.EditFamily(fam);
                        if (familyDoc != null)
                        {
                            try
                            {
                                using (Transaction t = new Transaction(familyDoc, "Change Family Category"))
                                {
                                    t.Start();
                                    Category newCat = familyDoc.Settings.Categories.get_Item((BuiltInCategory)(int)catItemId.GetIdValue());
                                    if (newCat != null)
                                    {
                                        familyDoc.OwnerFamily.FamilyCategory = newCat;
                                    }
                                    t.Commit();
                                }
                                familyDoc.LoadFamily(doc, new FamilyLoadOptions());
                            }
                            finally
                            {
                                familyDoc.Close(false);
                            }
                        }

                        Dispatcher.Invoke(() =>
                        {
                            LoadFamilyList();
                            PopulateCategoryFilter();
                            TypeList.Clear();
                            TypeDataGrid.Visibility = System.Windows.Visibility.Collapsed;
                            EmptyStatePanel.Visibility = System.Windows.Visibility.Visible;
                            SelectedFamilyTitle.Text = "请在左侧选择一个族";
                            _selectedFamilyId = null;
                        });
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() => TaskDialog.Show("Error", "Could not change category: " + ex.Message));
                    }
                });

                _externalEvent.Raise();
            }
        }

        private void ExportRfa_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFamilyId == null || _selectedFamilyId == ElementId.InvalidElementId) return;

            var dialog = new Microsoft.Win32.SaveFileDialog();
            dialog.Title = "Select Folder to Export RFA (Save to any file in the target folder)";
            dialog.FileName = "Select_Folder_Here";
            dialog.Filter = "Any File (*.*)|*.*";
            
            if (dialog.ShowDialog() == true)
            {
                string folder = System.IO.Path.GetDirectoryName(dialog.FileName);
                _handler.SetAction(app =>
                {
                    var doc = app.ActiveUIDocument.Document;
                    try
                    {
                        var fam = doc.GetElement(_selectedFamilyId) as Family;
                        if (fam == null || !fam.IsEditable) return;

                        Document familyDoc = doc.EditFamily(fam);
                        if (familyDoc != null)
                        {
                            try
                            {
                                string savePath = System.IO.Path.Combine(folder, fam.Name + ".rfa");
                                familyDoc.SaveAs(savePath, new SaveAsOptions { OverwriteExistingFile = true });
                            }
                            finally
                            {
                                familyDoc.Close(false);
                            }
                            Dispatcher.Invoke(() => TaskDialog.Show("Success", "Successfully exported family."));
                        }
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() => TaskDialog.Show("Error", "Export failed: " + ex.Message));
                    }
                });

                _externalEvent.Raise();
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = TypeDataGrid.SelectedItems.Cast<TypeItemViewModel>().ToList();
            if (selectedItems.Count == 0)
            {
                // If no type selected, ask if they want to delete the whole family
                var resultFam = TaskDialog.Show("确认", "没有选中类型，是否删除当前选中的整个族？", TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);
                if (resultFam == TaskDialogResult.Yes && _selectedFamilyId != null)
                {
                    _handler.SetAction(app =>
                    {
                        var doc = app.ActiveUIDocument.Document;
                        try
                        {
                            using (Transaction t = new Transaction(doc, "Delete Family"))
                            {
                                t.Start();
                                doc.Delete(_selectedFamilyId);
                                t.Commit();
                            }
                            Dispatcher.Invoke(() => {
                                LoadFamilyList();
                                TypeList.Clear();
                                TypeDataGrid.Visibility = System.Windows.Visibility.Collapsed;
                                EmptyStatePanel.Visibility = System.Windows.Visibility.Visible;
                                SelectedFamilyTitle.Text = "请在左侧选择一个族";
                                _selectedFamilyId = null;
                            });
                        }
                        catch (Exception ex)
                        {
                            Dispatcher.Invoke(() => TaskDialog.Show("错误", "Could not delete: " + ex.Message));
                        }
                    });
                    _externalEvent.Raise();
                }
                return;
            }

            var typeIds = selectedItems.Where(x => x.TypeId != ElementId.InvalidElementId).Select(x => x.TypeId).ToList();

            var result = TaskDialog.Show("确认", $"确定要删除选中的 {selectedItems.Count} 个类型吗？", TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);
            if (result == TaskDialogResult.Yes)
            {
                _handler.SetAction(app =>
                {
                    var doc = app.ActiveUIDocument.Document;
                    bool success = false;
                    try
                    {
                        using (Transaction t = new Transaction(doc, "Delete Types"))
                        {
                            t.Start();
                            foreach (var id in typeIds)
                            {
                                if (doc.GetElement(id) != null)
                                    doc.Delete(id);
                            }
                            t.Commit();
                            success = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() => TaskDialog.Show("错误", "Could not delete: " + ex.Message));
                    }

                    if (success)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            LoadTypeData();
                            var currentFamily = FamilyList.FirstOrDefault(x => x.FamilyId == _selectedFamilyId);
                            if (currentFamily != null) {
                                currentFamily.TypeCount -= typeIds.Count;
                            }
                        });
                    }
                });
                _externalEvent.Raise();
            }
        }
    }

    public class FamilyListItemViewModel : INotifyPropertyChanged
    {
        public string Category { get; set; }
        public string FamilyName { get; set; }
        public ElementId FamilyId { get; set; }
        
        private int _typeCount;
        public int TypeCount 
        { 
            get => _typeCount; 
            set { _typeCount = value; OnPropertyChanged(nameof(TypeCount)); } 
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }

    public class TypeItemViewModel : INotifyPropertyChanged
    {
        private string _typeName;
        public string TypeName 
        { 
            get => _typeName; 
            set { _typeName = value; OnPropertyChanged(nameof(TypeName)); } 
        }
        
        public ElementId FamilyId { get; set; }
        public ElementId TypeId { get; set; }

        public Dictionary<string, ParameterViewModel> Parameters { get; set; } = new Dictionary<string, ParameterViewModel>();

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }

    public class ParameterViewModel : INotifyPropertyChanged
    {
        private string _value;
        public string Value { get => _value; set { _value = value; OnPropertyChanged(nameof(Value)); } }
        
        private bool _boolValue;
        public bool BoolValue { get => _boolValue; set { _boolValue = value; OnPropertyChanged(nameof(BoolValue)); } }
        
        private bool _isReadOnly;
        public bool IsReadOnly { get => _isReadOnly; set { _isReadOnly = value; OnPropertyChanged(nameof(IsReadOnly)); } }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }

    public class CategoryItem
    {
        public string Name { get; set; }
        public ElementId Id { get; set; }
    }

    public class AvailableParamItem
    {
        public string Name { get; set; }
        public bool IsReadOnly { get; set; }
    }

    public class FamilyLoadOptions : IFamilyLoadOptions
    {
        public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
        {
            overwriteParameterValues = true;
            return true;
        }

        public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues)
        {
            source = FamilySource.Family;
            overwriteParameterValues = true;
            return true;
        }
    }
}
