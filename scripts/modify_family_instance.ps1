$path = "E:\Antigravity\YANG TOOLS_REVIT\src\YangTools.Revit\UI\FamilyInstanceManagerWindow.xaml.cs"
$content = Get-Content $path -Raw -Encoding UTF8

$exportExcel = @"
        private void ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            if (AllItems.Count == 0) return;
            var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Excel File (*.xlsx)|*.xlsx", FileName = "FamilyInstances.xlsx" };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var data = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object>>();
                    foreach (var item in AllItems)
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
"@

$deleteExcel = @"
        private void SelectInView_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Cast<InstanceItemViewModel>(InstanceDataGrid.SelectedItems));
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
            var selectedItems = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Cast<InstanceItemViewModel>(InstanceDataGrid.SelectedItems));
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
                                        extraDetails.AppendLine(`"- [`{catName}`] `{el.Name}`"`);
                                    }
                                } catch {}
                            }
                            if (extraIds.Count > 15) extraDetails.AppendLine(`"... 及其他 {extraIds.Count - 15} 个图元`");

                            var td = new TaskDialog("警告");
                            td.MainInstruction = `"您选择的 {ids.Count} 个{sourceDesc}将连带删除 {extraIds.Count} 个图元！`";
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
"@

$content = $content -replace '(?s)        private void ExportCsv_Click\(object sender, RoutedEventArgs e\).*?\}\s+\}', $exportExcel
$content = $content -replace '(?s)        private void Delete_Click\(object sender, RoutedEventArgs e\).*?_externalEvent\.Raise\(\);\s+\}\s+\}', $deleteExcel

[System.IO.File]::WriteAllText($path, $content, [System.Text.Encoding]::UTF8)
