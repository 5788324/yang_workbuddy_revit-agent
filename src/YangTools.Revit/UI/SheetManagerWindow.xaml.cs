using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using YangTools.Revit.Models;

namespace YangTools.Revit.UI
{
    public partial class SheetManagerWindow : Window, INotifyPropertyChanged
    {
        private UIApplication _uiapp;
        private Document _doc;
        private ExternalEvent _externalEvent;
        private RevitEventHandler _handler;
        
        public ObservableCollection<SheetItemViewModel> Sheets { get; set; } = new ObservableCollection<SheetItemViewModel>();
        public ObservableCollection<TitleblockItemViewModel> Titleblocks { get; set; } = new ObservableCollection<TitleblockItemViewModel>();
        
        private List<FamilySymbol> _availableTitleblocks = new List<FamilySymbol>();
        public List<FamilySymbol> AvailableTitleblocks
        {
            get => _availableTitleblocks;
            set { _availableTitleblocks = value; OnPropertyChanged(nameof(AvailableTitleblocks)); }
        }

        private List<string> _addedSheetParameters = new List<string>();
        private List<string> _titleblockParameterColumns = new List<string>();

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));

        public SheetManagerWindow(UIApplication uiapp)
        {
            InitializeComponent();
            ThemeHelper.ApplyToWindow(this);
            this.DataContext = this;
            _uiapp = uiapp;
            _doc = uiapp.ActiveUIDocument.Document;

            _handler = new RevitEventHandler();
            _externalEvent = ExternalEvent.Create(_handler);
            
            SheetsDataGrid.ItemsSource = Sheets;
            TitleblocksDataGrid.ItemsSource = Titleblocks;
            
            LoadSheets();
            LoadAvailableTitleblocks();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _externalEvent?.Dispose();
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

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl && e.AddedItems.Count > 0)
            {
                var tab = e.AddedItems[0] as TabItem;
                if (tab != null)
                {
                    if (tab.Header.ToString().Contains("图框"))
                    {
                        LoadTitleblocks();
                    }
                    else if (tab.Header.ToString().Contains("图纸"))
                    {
                        LoadSheets();
                    }
                }
            }
        }

        #region 图纸管理 (Sheet Management)

        private void LoadSheets()
        {
            Sheets.Clear();
            var sheets = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .OrderBy(s => s.SheetNumber)
                .ToList();

            int index = 1;
            foreach (var s in sheets)
            {
                var vm = new SheetItemViewModel
                {
                    Index = index++,
                    SheetId = s.Id,
                    SheetNumber = s.SheetNumber,
                    SheetName = s.Name,
                    Revisions = GetRevisionNames(s)
                };

                vm.OnSheetInfoChanged = (prop, newVal) => 
                {
                    var sheetId = vm.SheetId;
                    _handler.SetAction(app => {
                        var doc = app.ActiveUIDocument.Document;
                        var sheet = doc.GetElement(sheetId) as ViewSheet;
                        if (sheet != null) {
                            try {
                                using (Transaction t = new Transaction(doc, "修改图纸参数")) {
                                    t.Start();
                                    if (prop == "SheetNumber" && sheet.SheetNumber != newVal) sheet.SheetNumber = newVal;
                                    else if (prop == "SheetName" && sheet.Name != newVal) sheet.Name = newVal;
                                    t.Commit();
                                }
                            } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] SheetManagerWindow.xaml.cs: {0}", ex.Message); }
                        }
                    }, operationName: "修改图纸信息", showSuccess: false);
                    _externalEvent.Raise();
                };

                foreach (var paramName in _addedSheetParameters)
                {
                    var p = s.LookupParameter(paramName);
                    string val = p != null ? (p.AsValueString() ?? p.AsString() ?? "") : "";
                    var pItem = new ParameterItem { Name = paramName, Value = val };
                    pItem.OnValueChanged = (pName, newVal) => {
                        var sheetId = vm.SheetId;
                        _handler.SetAction(app => {
                            var doc = app.ActiveUIDocument.Document;
                            var sheet = doc.GetElement(sheetId) as ViewSheet;
                            if (sheet != null) {
                                try {
                                    using (Transaction t = new Transaction(doc, "修改图纸参数")) {
                                        t.Start();
                                        var param = sheet.LookupParameter(pName);
                                        if (param != null && !param.IsReadOnly) param.Set(newVal);
                                        t.Commit();
                                    }
                                } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] SheetManagerWindow.xaml.cs: {0}", ex.Message); }
                            }
                        }, operationName: "修改图纸参数", showSuccess: false);
                        _externalEvent.Raise();
                    };
                    vm.Parameters[paramName] = pItem;
                }

                Sheets.Add(vm);
            }
            SheetStatusBarText.Text = $"共计 {Sheets.Count} 张图纸";
        }

        private void AddParameterColumn_Click(object sender, RoutedEventArgs e)
        {
            var sheet = new FilteredElementCollector(_doc).OfClass(typeof(ViewSheet)).WhereElementIsNotElementType().FirstElement() as ViewSheet;
            if (sheet == null) return;
            var paramNames = new List<string>();
            foreach (Parameter p in sheet.Parameters)
            {
                if (!string.IsNullOrEmpty(p.Definition.Name))
                {
                    paramNames.Add(p.Definition.Name);
                }
            }
            paramNames.Sort();

            var dialog = new ParameterSelectWindow(paramNames.Distinct().ToList()) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                foreach (var sel in dialog.SelectedParameters)
                {
                    if (!_addedSheetParameters.Contains(sel))
                    {
                        _addedSheetParameters.Add(sel);
                        var col = new DataGridTextColumn
                        {
                            Header = sel,
                            Binding = new System.Windows.Data.Binding($"Parameters[{sel}].Value") { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus },
                            Width = DataGridLength.Auto
                        };
                        SheetsDataGrid.Columns.Add(col);
                    }
                }
                LoadSheets();
            }
        }

        // CellEditEnding removed to follow MVVM

        private Dictionary<ElementId, string> _revisionCache = new Dictionary<ElementId, string>();

        private string GetRevisionNames(ViewSheet sheet)
        {
            var revIds = sheet.GetAllRevisionIds();
            var names = new List<string>();
            foreach (var id in revIds)
            {
                if (!_revisionCache.ContainsKey(id))
                {
                    if (_doc.GetElement(id) is Revision rev)
                    {
                        _revisionCache[id] = rev.RevisionDate + " " + rev.Description;
                    }
                    else
                    {
                        _revisionCache[id] = "";
                    }
                }
                
                string name = _revisionCache[id];
                if (!string.IsNullOrEmpty(name))
                {
                    names.Add(name);
                }
            }
            return string.Join("; ", names);
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            foreach (var col in SheetsDataGrid.Columns)
            {
                col.Visibility = System.Windows.Visibility.Visible;
            }
            LoadSheets();
        }

        private void DeleteSheets_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = SheetsDataGrid.SelectedItems.Cast<SheetItemViewModel>().ToList();
            if (selectedItems.Count == 0) return;

            var ids = selectedItems.Select(x => x.SheetId).ToList();
            
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
                                } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] SheetManagerWindow.xaml.cs: {0}", ex.Message); }
                            }
                            if (extraIds.Count > 15) extraDetails.AppendLine($"... 及其他 {extraIds.Count - 15} 个图元");

                            var td = new TaskDialog("警告");
                            td.MainInstruction = $"您选择的 {ids.Count} 张图纸将连带删除 {extraIds.Count} 个图元！";
                            td.MainContent = "连带删除的通常是图纸上的视图、明细表、图框等图元\n\n图元如下：\n\n" + extraDetails.ToString();
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
                            Dispatcher.Invoke(() => LoadSheets());
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
            }, operationName: "删除图纸", showSuccess: true);
            _externalEvent.Raise();
        }

        private void EditRevisions_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var vm = btn?.Tag as SheetItemViewModel;
            if (vm == null) return;

            var sheet = _doc.GetElement(vm.SheetId) as ViewSheet;
            if (sheet == null) return;

            var revWindow = new RevisionsWindow(_doc, sheet) { Owner = this };
            if (revWindow.ShowDialog() == true)
            {
                var selectedRevisionIds = revWindow.SelectedRevisionIds.ToList();
                var sheetId = vm.SheetId;

                _handler.SetAction(app =>
                {
                    var doc = app.ActiveUIDocument.Document;
                    var sheetElem = doc.GetElement(sheetId) as ViewSheet;
                    if (sheetElem == null) return;

                    try
                    {
                        using (Transaction t = new Transaction(doc, "更新修订 (Update Revisions)"))
                        {
                            t.Start();
                            sheetElem.SetAdditionalRevisionIds(selectedRevisionIds);
                            t.Commit();
                        }

                        Dispatcher.Invoke(() =>
                        {
                            // MVVM handles updates automatically now
                            vm.Revisions = GetRevisionNames(sheetElem);
                        });
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() => TaskDialog.Show("错误", "无法更新修订: " + ex.Message));
                    }
                }, operationName: "更新修订", showSuccess: true);
                _externalEvent.Raise();
            }
        }

        private void ExportSheetExcel_Click(object sender, RoutedEventArgs e)
        {
            if (Sheets.Count == 0) return;
            var filePath = ExcelService.ShowSaveDialog("Sheets.xlsx");
            if (filePath == null) return;

            var data = new List<Dictionary<string, object>>();
            foreach (var item in Sheets)
            {
                var row = new Dictionary<string, object>();
                row["图号 (Sheet Number)"] = item.SheetNumber;
                row["图名 (Sheet Name)"] = item.SheetName;

                foreach (var col in _addedSheetParameters)
                {
                    if (string.IsNullOrEmpty(col)) continue;
                    row[col] = item.Parameters.ContainsKey(col) ? item.Parameters[col].Value : "";
                }
                data.Add(row);
            }
            ExcelService.ExportToExcel(filePath, data);
        }

        private void ImportSheetExcel_Click(object sender, RoutedEventArgs e)
        {
            var filePath = ExcelService.ShowOpenDialog();
            if (filePath == null) return;

            _handler.SetAction(app =>
                {
                    try
                    {
                        var rows = ExcelService.ReadExcel(filePath);
                        var doc = app.ActiveUIDocument.Document;
                        int successCount = 0;
                        int createdCount = 0;

                        // 获取一个默认图框用于创建新图纸
                        var defaultTitleblockId = ElementId.InvalidElementId;
                        var tbSymbols = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_TitleBlocks).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>().ToList();
                        if (tbSymbols.Count > 0) defaultTitleblockId = tbSymbols.First().Id;

                        // Cache existing sheets
                        var existingSheetsCache = new FilteredElementCollector(doc)
                            .OfClass(typeof(ViewSheet))
                            .Cast<ViewSheet>()
                            .ToDictionary(s => s.SheetNumber, s => s);

                        using (Transaction t = new Transaction(doc, "批量导入建图/更新"))
                        {
                            t.Start();
                            foreach (var row in rows)
                            {
                                if (!row.ContainsKey("图号 (Sheet Number)") || string.IsNullOrEmpty(row["图号 (Sheet Number)"]?.ToString())) continue;
                                
                                string sheetNumber = row["图号 (Sheet Number)"].ToString();
                                string sheetName = row.ContainsKey("图名 (Sheet Name)") ? row["图名 (Sheet Name)"]?.ToString() ?? "" : "";

                                // 查找是否存在
                                existingSheetsCache.TryGetValue(sheetNumber, out ViewSheet existingSheet);

                                ViewSheet targetSheet = existingSheet;

                                if (targetSheet == null)
                                {
                                    // 新建图纸
                                    targetSheet = ViewSheet.Create(doc, defaultTitleblockId);
                                    targetSheet.SheetNumber = sheetNumber;
                                    targetSheet.Name = string.IsNullOrEmpty(sheetName) ? "未命名图纸" : sheetName;
                                    existingSheetsCache[sheetNumber] = targetSheet;
                                    createdCount++;
                                }
                                else
                                {
                                    // 更新现有图纸名称
                                    if (!string.IsNullOrEmpty(sheetName) && targetSheet.Name != sheetName)
                                    {
                                        targetSheet.Name = sheetName;
                                    }
                                }

                                // 刷自定义参数
                                foreach (var kvp in row)
                                {
                                    if (kvp.Key == "图号 (Sheet Number)" || kvp.Key == "图名 (Sheet Name)" || kvp.Value == null) continue;
                                    
                                    var p = targetSheet.LookupParameter(kvp.Key);
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
                                        } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] SheetManagerWindow.xaml.cs: {0}", ex.Message); }
                                    }
                                }
                                successCount++;
                            }
                            t.Commit();
                        }

                        Dispatcher.Invoke(() =>
                        {
                            LoadSheets();
                        });
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() => TaskDialog.Show("错误", "Import failed: " + ex.Message));
                    }
                }, operationName: "导入图纸参数", showSuccess: true);
                _externalEvent.Raise();
            }
        }

        #endregion

        #region 图框管理 (Titleblock Management)

        private void LoadAvailableTitleblocks()
        {
            AvailableTitleblocks = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .OrderBy(x => x.FamilyName).ThenBy(x => x.Name)
                .ToList();
        }

        private void LoadTitleblocks()
        {
            Titleblocks.Clear();
            
            // 找到所有的图框实例
            var titleblockInstances = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .ToList();

            // 提取参数列 (过滤掉空值或无关紧要的)
            var paramSet = new HashSet<string>();

            foreach (var inst in titleblockInstances)
            {
                var sheet = _doc.GetElement(inst.OwnerViewId) as ViewSheet;
                if (sheet == null) continue;

                var vm = new TitleblockItemViewModel
                {
                    InstanceId = inst.Id,
                    InstanceIdValue = inst.Id.GetIdValue(),
                    SheetId = sheet.Id,
                    SheetNumber = sheet.SheetNumber,
                    SheetName = sheet.Name,
                    TypeId = inst.Symbol.Id,
                    TypeName = inst.Symbol.Name
                };

                foreach (Parameter p in inst.Parameters)
                {
                    if (p.IsReadOnly || string.IsNullOrEmpty(p.Definition.Name)) continue;
                    if (p.StorageType == StorageType.ElementId || p.StorageType == StorageType.None) continue;
                    
                    string pName = p.Definition.Name;
                    paramSet.Add(pName);
                    vm.Parameters[pName] = new ParameterItem { Name = pName, Value = p.AsValueString() ?? p.AsString() ?? "" };
                }

                Titleblocks.Add(vm);
            }

            // 动态生成DataGrid列
            _titleblockParameterColumns = paramSet.OrderBy(x => x).ToList();
            
            // 清理旧的动态列
            var colsToRemove = TitleblocksDataGrid.Columns.Where(c => c.Header is string s && paramSet.Contains(s)).ToList();
            foreach (var c in colsToRemove) TitleblocksDataGrid.Columns.Remove(c);

            // 重新添加动态列
            foreach (var colName in _titleblockParameterColumns)
            {
                var col = new DataGridTextColumn
                {
                    Header = colName,
                    Binding = new System.Windows.Data.Binding($"Parameters[{colName}].Value") { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
                    Width = DataGridLength.Auto
                };
                TitleblocksDataGrid.Columns.Add(col);
            }

            TitleblockStatusBarText.Text = $"共计 {Titleblocks.Count} 个图框实例";
        }

        private void TitleblockComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is FamilySymbol newSymbol)
            {
                var cb = sender as System.Windows.Controls.ComboBox;
                var vm = cb?.DataContext as TitleblockItemViewModel;
                if (vm != null && vm.TypeId != newSymbol.Id)
                {
                    _handler.SetAction(app =>
                    {
                        var doc = app.ActiveUIDocument.Document;
                        try
                        {
                            using (Transaction t = new Transaction(doc, "更换图框类型"))
                            {
                                t.Start();
                                if (!newSymbol.IsActive) newSymbol.Activate();
                                
                                var inst = doc.GetElement(vm.InstanceId) as FamilyInstance;
                                if (inst != null)
                                {
                                    inst.Symbol = newSymbol;
                                }
                                t.Commit();
                            }
                        }
                        catch (Exception ex)
                        {
                            Dispatcher.Invoke(() => TaskDialog.Show("错误", "Change type failed: " + ex.Message));
                        }
                    }, operationName: "更换图框类型", showSuccess: false);
                    _externalEvent.Raise();
                }
            }
        }

        private void ExportTitleblockExcel_Click(object sender, RoutedEventArgs e)
        {
            if (Titleblocks.Count == 0) return;
            var filePath = ExcelService.ShowSaveDialog("Titleblocks.xlsx");
            if (filePath == null) return;

            var data = new List<Dictionary<string, object>>();
            foreach (var item in Titleblocks)
            {
                var row = new Dictionary<string, object>();
                row["图元ID"] = item.InstanceIdValue.ToString();
                row["图纸号"] = item.SheetNumber;
                row["图纸名"] = item.SheetName;
                row["图框类型"] = item.TypeName;

                foreach (var col in _titleblockParameterColumns)
                {
                    row[col] = item.Parameters.ContainsKey(col) ? item.Parameters[col].Value : "";
                }
                data.Add(row);
            }
            ExcelService.ExportToExcel(filePath, data);
        }

        private void ImportTitleblockExcel_Click(object sender, RoutedEventArgs e)
        {
            var filePath = ExcelService.ShowOpenDialog();
            if (filePath == null) return;

            _handler.SetAction(app =>
                {
                    try
                    {
                        var rows = ExcelService.ReadExcel(filePath);
                        var doc = app.ActiveUIDocument.Document;
                        int successCount = 0;

                        using (Transaction t = new Transaction(doc, "批量导入图框参数"))
                        {
                            t.Start();
                            foreach (var row in rows)
                            {
                                if (!row.ContainsKey("图元ID") || string.IsNullOrEmpty(row["图元ID"]?.ToString())) continue;
                                
                                if (long.TryParse(row["图元ID"].ToString(), out long idVal))
                                {
                                    ElementId eId = YangTools.Revit.Core.ElementIdExtensions.CreateId(idVal);
                                    var elem = doc.GetElement(eId);
                                    if (elem == null) continue;

                                    foreach (var kvp in row)
                                    {
                                        if (kvp.Key == "图元ID" || kvp.Key == "图纸号" || kvp.Key == "图纸名" || kvp.Key == "图框类型" || kvp.Value == null) continue;
                                        
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
                                            } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] SheetManagerWindow.xaml.cs: {0}", ex.Message); }
                                        }
                                    }
                                    successCount++;
                                }
                            }
                            t.Commit();
                        }

                        Dispatcher.Invoke(() =>
                        {
                            LoadTitleblocks();
                        });
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() => TaskDialog.Show("错误", "Import failed: " + ex.Message));
                    }
                }, operationName: "导入图框参数", showSuccess: true);
                _externalEvent.Raise();
            }
        }

        private void SelectTitleblockInView_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = TitleblocksDataGrid.SelectedItems.Cast<TitleblockItemViewModel>().ToList();
            if (selectedItems.Count == 0) return;
            
            var ids = selectedItems.Where(x => x.InstanceId != ElementId.InvalidElementId).Select(x => x.InstanceId).ToList();
            _handler.SetAction(app =>
            {
                var uidoc = app.ActiveUIDocument;
                uidoc.Selection.SetElementIds(ids);
                uidoc.ShowElements(ids);
            }, operationName: "在视图中定位图框", showSuccess: true);
            _externalEvent.Raise();
        }

        private void RefreshTitleblock_Click(object sender, RoutedEventArgs e)
        {
            foreach (var col in TitleblocksDataGrid.Columns)
            {
                col.Visibility = System.Windows.Visibility.Visible;
            }
            LoadTitleblocks();
        }

        private void HideColumn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem menuItem && menuItem.Parent is System.Windows.Controls.ContextMenu contextMenu)
            {
                var header = contextMenu.PlacementTarget as System.Windows.Controls.Primitives.DataGridColumnHeader;
                if (header != null && header.Column != null)
                {
                    // 保护核心列：禁止隐藏 序号/图号/图名/图框类型
                    string headerText = header.Column.Header?.ToString() ?? "";
                    if (headerText == "#" || headerText.Contains("序号") || headerText.Contains("图号") || headerText.Contains("图名") || headerText.Contains("图框"))
                        return;
                    header.Column.Visibility = System.Windows.Visibility.Collapsed;
                }
            }
        }

        #endregion
    }

}
