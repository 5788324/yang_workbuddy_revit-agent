$ErrorActionPreference = "Stop"

function PatchFile($path, $searchStr, $replaceStr) {
    $content = Get-Content $path -Raw -Encoding UTF8
    $content = $content.Replace($searchStr, $replaceStr)
    [System.IO.File]::WriteAllText($path, $content, [System.Text.Encoding]::UTF8)
}

$famManPath = "E:\Antigravity\YANG TOOLS_REVIT\src\YangTools.Revit\UI\FamilyManagerWindow.xaml.cs"
$famInstPath = "E:\Antigravity\YANG TOOLS_REVIT\src\YangTools.Revit\UI\FamilyInstanceManagerWindow.xaml.cs"

# --- Family Manager Export ---
$famManExportCsv = @'
        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            if (TypeList.Count == 0) return;
            var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "CSV File (*.csv)|*.csv", FileName = "FamilyTypes.csv" };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var lines = new List<string>();
                    var headers = new List<string> { "类型名称" };
                    headers.AddRange(_parameterColumns.Where(p => !string.IsNullOrEmpty(p)));
                    lines.Add(string.Join(",", headers));

                    foreach (var item in TypeList)
                    {
                        var row = new List<string> { item.TypeName };
                        foreach (var col in _parameterColumns)
                        {
                            if (string.IsNullOrEmpty(col)) continue;
                            string val = item.Parameters.ContainsKey(col) ? item.Parameters[col].Value : "";
                            row.Add($"\"{val.Replace("\"", "\"\"")}\"");
                        }
                        lines.Add(string.Join(",", row));
                    }
                    System.IO.File.WriteAllLines(dialog.FileName, lines, System.Text.Encoding.UTF8);
                    TaskDialog.Show("提示", "导出成功！(Export Successful)");
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("错误", "导出失败: " + ex.Message);
                }
            }
        }
'@

$famManExportExcel = @'
        private void ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            if (TypeList.Count == 0) return;
            var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Excel File (*.xlsx)|*.xlsx", FileName = "FamilyTypes.xlsx" };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var data = new List<Dictionary<string, object>>();
                    foreach (var item in TypeList)
                    {
                        var row = new Dictionary<string, object>();
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
                        var rows = MiniExcelLibs.MiniExcel.Query(dialog.FileName, useHeaderRow: true).Cast<IDictionary<string, object>>();
                        var doc = app.ActiveUIDocument.Document;
                        int successCount = 0;

                        using (Transaction t = new Transaction(doc, "批量导入参数"))
                        {
                            t.Start();
                            foreach (var row in rows)
                            {
                                if (!row.ContainsKey("图元ID") || string.IsNullOrEmpty(row["图元ID"]?.ToString())) continue;
                                
                                if (int.TryParse(row["图元ID"].ToString(), out int idVal))
                                {
                                    ElementId eId = new ElementId(idVal);
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
            var selectedItems = TypeDataGrid.SelectedItems.Cast<TypeItemViewModel>().ToList();
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
                
                var ids = selectedItems.Where(x => x.TypeId != ElementId.InvalidElementId).Select(x => x.TypeId).ToList();
                
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
'@

# --- Family Manager Delete ---
$famManDeleteOld = @'
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
'@

$famManDeleteNew = @'
        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = TypeDataGrid.SelectedItems.Cast<TypeItemViewModel>().ToList();
            if (selectedItems.Count == 0)
            {
                var resultFam = TaskDialog.Show("确认", "没有选中类型，是否要删除当前选中的整个族？", TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);
                if (resultFam == TaskDialogResult.Yes && _selectedFamilyId != null)
                {
                    ExecuteDelete(new List<ElementId> { _selectedFamilyId }, "族");
                }
                return;
            }

            var typeIds = selectedItems.Where(x => x.TypeId != ElementId.InvalidElementId).Select(x => x.TypeId).ToList();
            var result = TaskDialog.Show("确认", $"确定要删除选中的 {selectedItems.Count} 个类型吗？", TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);
            if (result == TaskDialogResult.Yes)
            {
                ExecuteDelete(typeIds, "族类型");
            }
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
                                } catch {}
                            }
                            if (extraIds.Count > 15) extraDetails.AppendLine($"... 及其他 {extraIds.Count - 15} 个图元");

                            var td = new TaskDialog("警告");
                            td.MainInstruction = $"您选择的 {ids.Count} 个{sourceDesc}将连带删除 {extraIds.Count} 个图元！";
                            td.MainContent = "这通常是因为删除了族/类型，导致模型中的实例图元也被连带删除\n\n连带删除的图元如下：\n\n" + extraDetails.ToString();
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
                            Dispatcher.Invoke(() => {
                                if (sourceDesc == "族")
                                {
                                    LoadFamilyList();
                                    TypeList.Clear();
                                    TypeDataGrid.Visibility = System.Windows.Visibility.Collapsed;
                                    EmptyStatePanel.Visibility = System.Windows.Visibility.Visible;
                                    SelectedFamilyTitle.Text = "请选择一个族";
                                    _selectedFamilyId = null;
                                }
                                else
                                {
                                    LoadTypeData();
                                    var currentFamily = FamilyList.FirstOrDefault(x => x.FamilyId == _selectedFamilyId);
                                    if (currentFamily != null) {
                                        currentFamily.TypeCount -= ids.Count;
                                    }
                                }
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
'@


# --- Family Instance Export ---
$famInstExportCsv = @'
        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            if (AllItems.Count == 0) return;
            var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "CSV File (*.csv)|*.csv", FileName = "FamilyInstances.csv" };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var lines = new List<string>();
                    var headers = new List<string> { "图元ID", "类型" };
                    headers.AddRange(_parameterColumns.Where(p => !string.IsNullOrEmpty(p)));
                    lines.Add(string.Join(",", headers));

                    foreach (var item in AllItems)
                    {
                        var row = new List<string> { item.InstanceId.GetIdValue().ToString(), item.TypeName };
                        foreach (var col in _parameterColumns)
                        {
                            if (string.IsNullOrEmpty(col)) continue;
                            string val = item.Parameters.ContainsKey(col) ? item.Parameters[col].Value : "";
                            row.Add($"\"{val.Replace("\"", "\"\"")}\"");
                        }
                        lines.Add(string.Join(",", row));
                    }
                    System.IO.File.WriteAllLines(dialog.FileName, lines, System.Text.Encoding.UTF8);
                    TaskDialog.Show("提示", "导出成功！(Export Successful)");
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("错误", "导出失败: " + ex.Message);
                }
            }
        }
'@

$famInstExportExcel = @'
        private void ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            if (AllItems.Count == 0) return;
            var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Excel File (*.xlsx)|*.xlsx", FileName = "FamilyInstances.xlsx" };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var data = new List<Dictionary<string, object>>();
                    foreach (var item in AllItems)
                    {
                        var row = new Dictionary<string, object>();
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
                        var rows = MiniExcelLibs.MiniExcel.Query(dialog.FileName, useHeaderRow: true).Cast<IDictionary<string, object>>();
                        var doc = app.ActiveUIDocument.Document;
                        int successCount = 0;

                        using (Transaction t = new Transaction(doc, "批量导入实例参数"))
                        {
                            t.Start();
                            foreach (var row in rows)
                            {
                                if (!row.ContainsKey("图元ID") || string.IsNullOrEmpty(row["图元ID"]?.ToString())) continue;
                                
                                if (int.TryParse(row["图元ID"].ToString(), out int idVal))
                                {
                                    ElementId eId = new ElementId(idVal);
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
                            TaskDialog.Show("提示", $"成功更新了 {successCount} 个实例的参数！");
                            LoadData();
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
'@

# --- Family Instance Delete ---
$famInstDeleteOld = @'
        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = InstanceDataGrid.SelectedItems.Cast<InstanceItemViewModel>().ToList();
            if (selectedItems.Count == 0) return;

            var ids = selectedItems.Select(x => x.InstanceId).ToList();
            var result = TaskDialog.Show("确认", $"确定要删除选中的 {ids.Count} 个实例吗？", TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);
            if (result == TaskDialogResult.Yes)
            {
                _handler.SetAction(app =>
                {
                    var doc = app.ActiveUIDocument.Document;
                    bool success = false;
                    try
                    {
                        using (Transaction t = new Transaction(doc, "Delete Instances"))
                        {
                            t.Start();
                            foreach (var id in ids)
                            {
                                if (doc.GetElement(id) != null) doc.Delete(id);
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
                        Dispatcher.Invoke(() => LoadData());
                    }
                });
                _externalEvent.Raise();
            }
        }
'@

$famInstDeleteNew = @'
        private void SelectInView_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = InstanceDataGrid.SelectedItems.Cast<InstanceItemViewModel>().ToList();
            if (selectedItems.Count == 0) return;
            
            var ids = selectedItems.Where(x => x.InstanceId != ElementId.InvalidElementId).Select(x => x.InstanceId).ToList();
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
            var selectedItems = InstanceDataGrid.SelectedItems.Cast<InstanceItemViewModel>().ToList();
            if (selectedItems.Count == 0) return;

            var ids = selectedItems.Select(x => x.InstanceId).ToList();
            ExecuteDelete(ids, "实例");
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
                            Dispatcher.Invoke(() => LoadData());
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
'@

Write-Host "Patching Family Manager..."
PatchFile $famManPath $famManExportCsv $famManExportExcel
PatchFile $famManPath $famManDeleteOld $famManDeleteNew

Write-Host "Patching Family Instance Manager..."
PatchFile $famInstPath $famInstExportCsv $famInstExportExcel
PatchFile $famInstPath $famInstDeleteOld $famInstDeleteNew

Write-Host "Done."
