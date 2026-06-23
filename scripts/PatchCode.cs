using System;
using System.IO;

namespace Patcher
{
    class Program
    {
        static void Main()
        {
            try
            {
                string famMan = @"E:\Antigravity\YANG TOOLS_REVIT\src\YangTools.Revit\UI\FamilyManagerWindow.xaml.cs";
                string famInst = @"E:\Antigravity\YANG TOOLS_REVIT\src\YangTools.Revit\UI\FamilyInstanceManagerWindow.xaml.cs";

                string fmContent = File.ReadAllText(famMan, System.Text.Encoding.UTF8);
                string fiContent = File.ReadAllText(famInst, System.Text.Encoding.UTF8);

                // Find and replace in FamilyManagerWindow
                int fmExpStart = fmContent.IndexOf("private void ExportCsv_Click");
                int fmExpEnd = fmContent.IndexOf("private void Duplicate_Click");
                if (fmExpStart != -1 && fmExpEnd != -1)
                {
                    fmContent = fmContent.Substring(0, fmExpStart) + GetFamManExportCode() + fmContent.Substring(fmExpEnd);
                }

                int fmDelStart = fmContent.IndexOf("private void Delete_Click");
                int fmDelEnd = fmContent.IndexOf("public class FamilyItemViewModel");
                if (fmDelStart != -1 && fmDelEnd != -1)
                {
                    // Find the last closing brace before public class
                    int endBrace = fmContent.LastIndexOf("}", fmDelEnd);
                    endBrace = fmContent.LastIndexOf("}", endBrace - 1); // Get to the class closing brace, actually let's just find the exact index.
                    
                    // A better way: just replace from Delete_Click to the end of class
                    int realEnd = fmContent.IndexOf("public class FamilyItemViewModel");
                    // We need to keep the closing brace of the class
                    string afterDelete = "\r\n    }\r\n\r\n    public class FamilyItemViewModel";
                    
                    fmContent = fmContent.Substring(0, fmDelStart) + GetFamManDeleteCode() + afterDelete + fmContent.Substring(realEnd + afterDelete.Length - "\r\n    public class FamilyItemViewModel".Length);
                }

                File.WriteAllText(famMan, fmContent, new System.Text.UTF8Encoding(true));

                // Find and replace in FamilyInstanceManagerWindow
                int fiExpStart = fiContent.IndexOf("private void ExportCsv_Click");
                int fiExpEnd = fiContent.IndexOf("private void SearchChanged");
                if (fiExpStart != -1 && fiExpEnd != -1)
                {
                    fiContent = fiContent.Substring(0, fiExpStart) + GetFamInstExportCode() + fiContent.Substring(fiExpEnd);
                }

                int fiDelStart = fiContent.IndexOf("private void Delete_Click");
                int fiDelEnd = fiContent.IndexOf("public class FamilyFilterItem");
                if (fiDelStart != -1 && fiDelEnd != -1)
                {
                    string afterDelete = "\r\n    }\r\n\r\n    public class FamilyFilterItem";
                    fiContent = fiContent.Substring(0, fiDelStart) + GetFamInstDeleteCode() + afterDelete + fiContent.Substring(fiDelEnd + afterDelete.Length - "\r\n    public class FamilyFilterItem".Length);
                }

                File.WriteAllText(famInst, fiContent, new System.Text.UTF8Encoding(true));

                Console.WriteLine("Patching successful");
            }
            catch(Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }

        static string GetFamManExportCode()
        {
            return @"private void ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            if (TypeList.Count == 0) return;
            var dialog = new Microsoft.Win32.SaveFileDialog { Filter = ""Excel File (*.xlsx)|*.xlsx"", FileName = ""FamilyTypes.xlsx"" };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var data = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object>>();
                    foreach (var item in TypeList)
                    {
                        var row = new System.Collections.Generic.Dictionary<string, object>();
                        row[""类型名称 (Type)""] = item.TypeName;
                        row[""图元ID""] = item.TypeId.ToString();
                        
                        foreach (var col in _parameterColumns)
                        {
                            if (string.IsNullOrEmpty(col)) continue;
                            row[col] = item.Parameters.ContainsKey(col) ? item.Parameters[col].Value : """";
                        }
                        data.Add(row);
                    }
                    MiniExcelLibs.MiniExcel.SaveAs(dialog.FileName, data);
                    TaskDialog.Show(""提示"", ""导出成功 (Export Successful)"");
                }
                catch (Exception ex)
                {
                    TaskDialog.Show(""错误"", ""导出失败: "" + ex.Message);
                }
            }
        }

        private void ImportExcel_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = ""Excel File (*.xlsx)|*.xlsx"" };
            if (dialog.ShowDialog() == true)
            {
                _handler.SetAction(app =>
                {
                    try
                    {
                        var rows = System.Linq.Enumerable.Cast<System.Collections.Generic.IDictionary<string, object>>(MiniExcelLibs.MiniExcel.Query(dialog.FileName, useHeaderRow: true));
                        var doc = app.ActiveUIDocument.Document;
                        int successCount = 0;

                        using (Transaction t = new Transaction(doc, ""批量导入参数""))
                        {
                            t.Start();
                            foreach (var row in rows)
                            {
                                if (!row.ContainsKey(""图元ID"") || row[""图元ID""] == null || string.IsNullOrEmpty(row[""图元ID""].ToString())) continue;
                                
                                if (int.TryParse(row[""图元ID""].ToString(), out int idVal))
                                {
                                    ElementId eId = new ElementId(idVal);
                                    var elem = doc.GetElement(eId);
                                    if (elem == null) continue;

                                    if (row.ContainsKey(""类型名称 (Type)"") && row[""类型名称 (Type)""] != null)
                                    {
                                        string newName = row[""类型名称 (Type)""].ToString();
                                        if (elem.Name != newName && !string.IsNullOrWhiteSpace(newName))
                                        {
                                            try { elem.Name = newName; } catch { }
                                        }
                                    }

                                    foreach (var kvp in row)
                                    {
                                        if (kvp.Key == ""图元ID"" || kvp.Key == ""类型名称 (Type)"" || kvp.Value == null) continue;
                                        
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
                            TaskDialog.Show(""提示"", $""成功更新了 {successCount} 个类型的参数！"");
                            if (_selectedFamilyId != null) LoadTypeData();
                        });
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() => TaskDialog.Show(""错误"", ""Import failed: "" + ex.Message));
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
                TaskDialog.Show(""提示"", ""请先在右侧列表中选择要重命名的族类型。"");
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
                        using (Transaction t = new Transaction(doc, ""批量重命名族类型""))
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
                                            newName = newName.Replace(find, replace ?? """");
                                        
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
                            TaskDialog.Show(""提示"", $""成功重命名 {count} 个族类型！"");
                            if (_selectedFamilyId != null) LoadTypeData();
                        });
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() => TaskDialog.Show(""错误"", ""Batch rename failed: "" + ex.Message));
                    }
                });
                _externalEvent.Raise();
            }
        }

        ";
        }

        static string GetFamManDeleteCode()
        {
            return @"private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Cast<TypeItemViewModel>(TypeDataGrid.SelectedItems));
            if (selectedItems.Count == 0)
            {
                var resultFam = TaskDialog.Show(""确认"", ""没有选中类型，是否要删除当前选中的整个族？"", TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);
                if (resultFam == TaskDialogResult.Yes && _selectedFamilyId != null)
                {
                    ExecuteDelete(new System.Collections.Generic.List<ElementId> { _selectedFamilyId }, ""族"");
                }
                return;
            }

            var typeIds = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(System.Linq.Enumerable.Where(selectedItems, x => x.TypeId != ElementId.InvalidElementId), x => x.TypeId));
            var result = TaskDialog.Show(""确认"", $""确定要删除选中的 {selectedItems.Count} 个类型吗？"", TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);
            if (result == TaskDialogResult.Yes)
            {
                ExecuteDelete(typeIds, ""族类型"");
            }
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

                    using (TransactionGroup tg = new TransactionGroup(doc, ""级联删除(YANG TOOLS)""))
                    {
                        tg.Start();
                        System.Collections.Generic.List<ElementId> extraIds = new System.Collections.Generic.List<ElementId>();
                        using (Transaction t1 = new Transaction(doc, ""Trial Delete""))
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
                                        extraDetails.AppendLine($""- [{catName}] {el.Name}"");
                                    }
                                } catch {}
                            }
                            if (extraIds.Count > 15) extraDetails.AppendLine($""... 及其他 {extraIds.Count - 15} 个图元"");

                            var td = new TaskDialog(""警告"");
                            td.MainInstruction = $""您选择的 {ids.Count} 个{sourceDesc}将连带删除 {extraIds.Count} 个图元！"";
                            td.MainContent = ""这通常是因为删除了族/类型，导致模型中的实例图元也被连带删除\n\n连带删除的图元如下：\n\n"" + extraDetails.ToString();
                            td.ExpandedContent = ""请确认您是否要连带这些图元一起删除？"";
                            td.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No;
                            td.DefaultButton = TaskDialogResult.No;
                            var result = td.Show();
                            
                            if (result != TaskDialogResult.Yes) proceed = false;
                        }

                        if (proceed)
                        {
                            using (Transaction t2 = new Transaction(doc, ""Execute Delete""))
                            {
                                t2.Start();
                                doc.Delete(ids);
                                t2.Commit();
                            }
                            tg.Assimilate();
                            Dispatcher.Invoke(() => {
                                if (sourceDesc == ""族"")
                                {
                                    LoadFamilyList();
                                    TypeList.Clear();
                                    TypeDataGrid.Visibility = System.Windows.Visibility.Collapsed;
                                    EmptyStatePanel.Visibility = System.Windows.Visibility.Visible;
                                    SelectedFamilyTitle.Text = ""请选择一个族"";
                                    _selectedFamilyId = null;
                                }
                                else
                                {
                                    LoadTypeData();
                                    var currentFamily = System.Linq.Enumerable.FirstOrDefault(FamilyList, x => x.FamilyId == _selectedFamilyId);
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
                    Dispatcher.Invoke(() => TaskDialog.Show(""错误"", ""删除失败: "" + ex.Message));
                }
            });
            _externalEvent.Raise();
        }";
        }

        static string GetFamInstExportCode()
        {
            return @"private void ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            if (AllItems.Count == 0) return;
            var dialog = new Microsoft.Win32.SaveFileDialog { Filter = ""Excel File (*.xlsx)|*.xlsx"", FileName = ""FamilyInstances.xlsx"" };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var data = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object>>();
                    foreach (var item in AllItems)
                    {
                        var row = new System.Collections.Generic.Dictionary<string, object>();
                        row[""图元ID""] = item.InstanceId.GetIdValue().ToString();
                        row[""类型 (Type)""] = item.TypeName;
                        
                        foreach (var col in _parameterColumns)
                        {
                            if (string.IsNullOrEmpty(col)) continue;
                            row[col] = item.Parameters.ContainsKey(col) ? item.Parameters[col].Value : """";
                        }
                        data.Add(row);
                    }
                    MiniExcelLibs.MiniExcel.SaveAs(dialog.FileName, data);
                    TaskDialog.Show(""提示"", ""导出成功 (Export Successful)"");
                }
                catch (Exception ex)
                {
                    TaskDialog.Show(""错误"", ""导出失败: "" + ex.Message);
                }
            }
        }

        private void ImportExcel_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = ""Excel File (*.xlsx)|*.xlsx"" };
            if (dialog.ShowDialog() == true)
            {
                _handler.SetAction(app =>
                {
                    try
                    {
                        var rows = System.Linq.Enumerable.Cast<System.Collections.Generic.IDictionary<string, object>>(MiniExcelLibs.MiniExcel.Query(dialog.FileName, useHeaderRow: true));
                        var doc = app.ActiveUIDocument.Document;
                        int successCount = 0;

                        using (Transaction t = new Transaction(doc, ""批量导入实例参数""))
                        {
                            t.Start();
                            foreach (var row in rows)
                            {
                                if (!row.ContainsKey(""图元ID"") || row[""图元ID""] == null || string.IsNullOrEmpty(row[""图元ID""].ToString())) continue;
                                
                                if (int.TryParse(row[""图元ID""].ToString(), out int idVal))
                                {
                                    ElementId eId = new ElementId(idVal);
                                    var elem = doc.GetElement(eId);
                                    if (elem == null) continue;

                                    foreach (var kvp in row)
                                    {
                                        if (kvp.Key == ""图元ID"" || kvp.Key == ""类型 (Type)"" || kvp.Value == null) continue;
                                        
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
                            TaskDialog.Show(""提示"", $""成功更新了 {successCount} 个实例的参数！"");
                            LoadData();
                        });
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() => TaskDialog.Show(""错误"", ""Import failed: "" + ex.Message));
                    }
                });
                _externalEvent.Raise();
            }
        }

        ";
        }

        static string GetFamInstDeleteCode()
        {
            return @"private void SelectInView_Click(object sender, RoutedEventArgs e)
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
            ExecuteDelete(ids, ""实例"");
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

                    using (TransactionGroup tg = new TransactionGroup(doc, ""级联删除(YANG TOOLS)""))
                    {
                        tg.Start();
                        System.Collections.Generic.List<ElementId> extraIds = new System.Collections.Generic.List<ElementId>();
                        using (Transaction t1 = new Transaction(doc, ""Trial Delete""))
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
                                        extraDetails.AppendLine($""- [{catName}] {el.Name}"");
                                    }
                                } catch {}
                            }
                            if (extraIds.Count > 15) extraDetails.AppendLine($""... 及其他 {extraIds.Count - 15} 个图元"");

                            var td = new TaskDialog(""警告"");
                            td.MainInstruction = $""您选择的 {ids.Count} 个{sourceDesc}将连带删除 {extraIds.Count} 个图元！"";
                            td.MainContent = ""这通常是因为删除了主体，导致依附于其上的标记或其他图元也被连带删除\n\n连带删除的图元如下：\n\n"" + extraDetails.ToString();
                            td.ExpandedContent = ""请确认您是否要连带这些图元一起删除？"";
                            td.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No;
                            td.DefaultButton = TaskDialogResult.No;
                            var result = td.Show();
                            
                            if (result != TaskDialogResult.Yes) proceed = false;
                        }

                        if (proceed)
                        {
                            using (Transaction t2 = new Transaction(doc, ""Execute Delete""))
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
                    Dispatcher.Invoke(() => TaskDialog.Show(""错误"", ""删除失败: "" + ex.Message));
                }
            });
            _externalEvent.Raise();
        }";
        }
    }
}
