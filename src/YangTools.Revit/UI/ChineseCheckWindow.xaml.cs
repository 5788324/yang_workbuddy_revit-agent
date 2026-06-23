using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;

namespace YangTools.Revit.UI
{
    /// <summary>
    /// 中文字符检查窗口 —— 扫描项目中族、参数、注释、视图的中文内容
    /// </summary>
    public partial class ChineseCheckWindow : Window
    {
        private readonly Document _doc;
        private readonly UIDocument _uiDoc;

        /// <summary>
        /// 检查结果数据项
        /// </summary>
        private class ChineseItem
        {
            public string DisplayText { get; set; }
            public ElementId Id { get; set; }
            public string Category { get; set; }

            public override string ToString() => DisplayText;
        }

        public ChineseCheckWindow(Document doc, UIDocument uiDoc)
        {
            InitializeComponent();
            _doc = doc;
            _uiDoc = uiDoc;

            Loaded += (s, e) => RunScan();
        }

        #region 中文检测

        /// <summary>
        /// 判断字符串是否包含中文字符（CJK统一汉字区）
        /// </summary>
        private static bool ContainsChinese(string s)
        {
            return !string.IsNullOrEmpty(s) && Regex.IsMatch(s, @"[\u4e00-\u9fff]");
        }

        #endregion

        #region 扫描逻辑

        /// <summary>
        /// 执行全项目中文扫描
        /// </summary>
        private void RunScan()
        {
            ListFamilyTypes.Items.Clear();
            ListFamilyInstances.Items.Clear();
            ListProjectParams.Items.Clear();
            ListProjectEnv.Items.Clear();

            try
            {
                ScanFamiliesAndTypes();
                ScanFamilyInstances();
                ScanProjectParameters();
                ScanProjectEnvironment();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"扫描过程中出现异常：\n{ex.Message}", "YangTools 中文检查",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Panel 1: 扫描族名、族类型名/参数、材质名、文字注释
        /// </summary>
        private void ScanFamiliesAndTypes()
        {
            // --- 族名 ---
            var families = new FilteredElementCollector(_doc)
                .OfClass(typeof(Family))
                .Cast<Family>();

            foreach (var fam in families)
            {
                try
                {
                    if (ContainsChinese(fam.Name))
                    {
                        AddItem(ListFamilyTypes, $"{fam.Name} -{fam.FamilyCategory?.Name ?? "无类别"}- (族名)", fam.Id, "族名");
                    }
                }
                catch { /* 跳过无法访问的族 */ }
            }

            // --- 族类型名及参数 ---
            var symbols = new FilteredElementCollector(_doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>();

            foreach (var sym in symbols)
            {
                try
                {
                    if (ContainsChinese(sym.Name))
                    {
                        string catName = sym.Category?.Name ?? "无类别";
                        AddItem(ListFamilyTypes, $"{sym.Name} -{catName}- (族类型名)", sym.Id, "族类型名");
                    }

                    // 检查类型参数名和参数值
                    foreach (Parameter param in sym.Parameters)
                    {
                        try
                        {
                            string paramName = param.Definition?.Name;
                            if (ContainsChinese(paramName))
                            {
                                AddItem(ListFamilyTypes, $"{sym.Name} -{paramName}- (族参数名)", sym.Id, "族参数名");
                            }

                            string paramValue = GetParameterDisplayValue(param);
                            if (ContainsChinese(paramValue))
                            {
                                AddItem(ListFamilyTypes, $"{sym.Name} -{paramName}: {Truncate(paramValue, 30)}- (族参数值)", sym.Id, "族参数值");
                            }
                        }
                        catch { /* 跳过无法读取的参数 */ }
                    }
                }
                catch { /* 跳过无法访问的族类型 */ }
            }

            // --- 材质名 ---
            var materials = new FilteredElementCollector(_doc)
                .OfClass(typeof(Material))
                .Cast<Material>();

            foreach (var mat in materials)
            {
                try
                {
                    if (ContainsChinese(mat.Name))
                    {
                        AddItem(ListFamilyTypes, $"{mat.Name} -Material- (材质名)", mat.Id, "材质名");
                    }
                }
                catch { /* 跳过 */ }
            }

            // --- 文字注释 ---
            var textNotes = new FilteredElementCollector(_doc)
                .OfClass(typeof(TextNote))
                .Cast<TextNote>();

            foreach (var tn in textNotes)
            {
                try
                {
                    if (ContainsChinese(tn.Text))
                    {
                        AddItem(ListFamilyTypes, $"{Truncate(tn.Text, 40)} -TextNote- (文字注释)", tn.Id, "文字注释");
                    }
                }
                catch { /* 跳过 */ }
            }
        }

        /// <summary>
        /// Panel 2: 扫描族实例的参数名和参数值
        /// </summary>
        private void ScanFamilyInstances()
        {
            var instances = new FilteredElementCollector(_doc)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Take(5000); // 限制数量以避免性能问题

            foreach (var inst in instances)
            {
                try
                {
                    foreach (Parameter param in inst.Parameters)
                    {
                        try
                        {
                            string paramName = param.Definition?.Name;
                            if (ContainsChinese(paramName))
                            {
                                string instName = inst.Name ?? "(未命名)";
                                AddItem(ListFamilyInstances, $"{instName} -{paramName}- (实例参数名)", inst.Id, "实例参数名");
                            }

                            string paramValue = GetParameterDisplayValue(param);
                            if (ContainsChinese(paramValue))
                            {
                                string instName = inst.Name ?? "(未命名)";
                                AddItem(ListFamilyInstances, $"{instName} -{paramName}: {Truncate(paramValue, 25)}- (实例参数值)", inst.Id, "实例参数值");
                            }
                        }
                        catch { /* 跳过无法读取的参数 */ }
                    }
                }
                catch { /* 跳过无法访问的实例 */ }
            }
        }

        /// <summary>
        /// Panel 3: 扫描项目参数
        /// </summary>
        private void ScanProjectParameters()
        {
            var paramElements = new FilteredElementCollector(_doc)
                .OfClass(typeof(ParameterElement))
                .Cast<ParameterElement>();

            foreach (var pe in paramElements)
            {
                try
                {
                    // 排除共享参数，只保留项目参数
                    if (pe is SharedParameterElement) continue;

                    Definition def = pe.GetDefinition();
                    if (def != null && ContainsChinese(def.Name))
                    {
                        AddItem(ListProjectParams, $"{def.Name} -ProjectParam- (项目参数)", pe.Id, "项目参数");
                    }
                }
                catch { /* 跳过 */ }
            }
        }

        /// <summary>
        /// Panel 4: 扫描项目环境（视图名称、项目信息等）
        /// </summary>
        private void ScanProjectEnvironment()
        {
            // --- 视图名称 ---
            var views = new FilteredElementCollector(_doc)
                .OfClass(typeof(View))
                .Cast<View>();

            foreach (var view in views)
            {
                try
                {
                    // 跳过视图模板
                    if (view.IsTemplate) continue;

                    if (ContainsChinese(view.Name))
                    {
                        string viewType = view.ViewType.ToString();
                        AddItem(ListProjectEnv, $"{view.Name} -{viewType}- (视图名称)", view.Id, "视图名称");
                    }
                }
                catch { /* 跳过 */ }
            }

            // --- 项目信息 ---
            try
            {
                ProjectInfo info = _doc.ProjectInformation;
                if (info != null)
                {
                    if (ContainsChinese(info.Name))
                        AddItem(ListProjectEnv, $"{info.Name} -ProjectInfo- (项目名称)", info.Id, "项目名称");

                    if (ContainsChinese(info.Number))
                        AddItem(ListProjectEnv, $"{info.Number} -ProjectInfo- (项目编号)", info.Id, "项目编号");

                    if (ContainsChinese(info.Author))
                        AddItem(ListProjectEnv, $"{info.Author} -ProjectInfo- (项目作者)", info.Id, "项目作者");

                    if (ContainsChinese(info.OrganizationName))
                        AddItem(ListProjectEnv, $"{info.OrganizationName} -ProjectInfo- (组织名称)", info.Id, "组织名称");

                    if (ContainsChinese(info.BuildingName))
                        AddItem(ListProjectEnv, $"{info.BuildingName} -ProjectInfo- (建筑名称)", info.Id, "建筑名称");
                }
            }
            catch { /* 项目信息不可用 */ }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取参数的显示值
        /// </summary>
        private static string GetParameterDisplayValue(Parameter param)
        {
            if (param == null || !param.HasValue) return null;

            switch (param.StorageType)
            {
                case StorageType.String:
                    return param.AsString();
                case StorageType.Integer:
                case StorageType.Double:
                    return param.AsValueString();
                case StorageType.ElementId:
                    return param.AsValueString();
                default:
                    return null;
            }
        }

        /// <summary>
        /// 截断字符串到指定长度
        /// </summary>
        private static string Truncate(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text)) return text;
            // 将换行替换为空格，方便单行显示
            text = text.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
            return text.Length <= maxLen ? text : text.Substring(0, maxLen) + "…";
        }

        /// <summary>
        /// 向 ListBox 添加一条检查结果
        /// </summary>
        private void AddItem(ListBox listBox, string displayText, ElementId id, string category)
        {
            var item = new ChineseItem
            {
                DisplayText = displayText,
                Id = id,
                Category = category
            };

            var listBoxItem = new ListBoxItem
            {
                Content = displayText,
                Tag = item,
                ToolTip = $"ElementId: {id.GetIdValue()}  |  类别: {category}"
            };

            listBox.Items.Add(listBoxItem);
        }

        /// <summary>
        /// 从 ListBox 中获取选中项的 ChineseItem 列表
        /// </summary>
        private List<ChineseItem> GetSelectedItems(ListBox listBox)
        {
            var result = new List<ChineseItem>();
            foreach (var selected in listBox.SelectedItems)
            {
                if (selected is ListBoxItem lbi && lbi.Tag is ChineseItem ci)
                {
                    result.Add(ci);
                }
            }
            return result;
        }

        #endregion

        #region 删除操作

        private void DeleteElements(ListBox listBox, string panelName)
        {
            var items = GetSelectedItems(listBox);
            if (items.Count == 0)
            {
                MessageBox.Show("请先在列表中选择要删除的项目。", "YangTools 中文检查",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 过滤掉 InvalidElementId
            var validIds = items
                .Where(i => i.Id != ElementId.InvalidElementId)
                .Select(i => i.Id)
                .Distinct()
                .ToList();

            if (validIds.Count == 0)
            {
                MessageBox.Show("所选项目没有可删除的图元（可能是共享参数定义，请从项目参数中手动删除）。",
                    "YangTools 中文检查", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show(
                $"确定要删除 [{panelName}] 中选中的 {validIds.Count} 个图元吗？\n此操作不可撤销！",
                "YangTools 中文检查", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            int deleted = 0;
            int failed = 0;

            using (Transaction tx = new Transaction(_doc, $"YangTools: 删除中文图元 ({panelName})"))
            {
                tx.Start();
                foreach (var id in validIds)
                {
                    try
                    {
                        _doc.Delete(id);
                        deleted++;
                    }
                    catch
                    {
                        failed++;
                    }
                }
                tx.Commit();
            }

            string msg = $"成功删除 {deleted} 个图元。";
            if (failed > 0) msg += $"\n{failed} 个图元删除失败（可能被锁定或为系统图元）。";

            MessageBox.Show(msg, "YangTools 中文检查", MessageBoxButton.OK, MessageBoxImage.Information);
            RunScan(); // 刷新列表
        }

        private void BtnDeleteFamilyTypes_Click(object sender, RoutedEventArgs e)
        {
            DeleteElements(ListFamilyTypes, "族/类型/材质/注释");
        }

        private void BtnDeleteInstances_Click(object sender, RoutedEventArgs e)
        {
            DeleteElements(ListFamilyInstances, "族实例");
        }

        /// <summary>
        /// 删除项目参数
        /// </summary>
        private void BtnDeleteProjectParams_Click(object sender, RoutedEventArgs e)
        {
            DeleteElements(ListProjectParams, "项目参数");
        }

        private void BtnDeleteProjectEnv_Click(object sender, RoutedEventArgs e)
        {
            DeleteElements(ListProjectEnv, "项目环境");
        }

        #endregion

        #region 导出 ID

        private void ExportElementIds(ListBox listBox, string panelName)
        {
            var items = GetSelectedItems(listBox);
            if (items.Count == 0)
            {
                // 如果没有选中，则导出全部
                items = new List<ChineseItem>();
                foreach (var obj in listBox.Items)
                {
                    if (obj is ListBoxItem lbi && lbi.Tag is ChineseItem ci)
                    {
                        items.Add(ci);
                    }
                }
            }

            if (items.Count == 0)
            {
                MessageBox.Show("列表为空，无可导出的 ID。", "YangTools 中文检查",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var ids = items
                .Where(i => i.Id != ElementId.InvalidElementId)
                .Select(i => i.Id.GetIdValue().ToString())
                .Distinct()
                .ToList();

            if (ids.Count == 0)
            {
                MessageBox.Show("所选项目没有有效的 ElementId。", "YangTools 中文检查",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string idText = string.Join(", ", ids);
            System.Windows.Clipboard.SetText(idText);

            MessageBox.Show($"已复制 {ids.Count} 个 ElementId 到剪贴板。\n\n{Truncate(idText, 200)}",
                "YangTools 中文检查", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnExportFamilyTypeIds_Click(object sender, RoutedEventArgs e)
        {
            ExportElementIds(ListFamilyTypes, "族/类型/材质/注释");
        }

        private void BtnExportInstanceIds_Click(object sender, RoutedEventArgs e)
        {
            ExportElementIds(ListFamilyInstances, "族实例");
        }

        private void BtnExportProjectParamIds_Click(object sender, RoutedEventArgs e)
        {
            ExportElementIds(ListProjectParams, "项目参数");
        }

        private void BtnExportProjectEnvIds_Click(object sender, RoutedEventArgs e)
        {
            ExportElementIds(ListProjectEnv, "项目环境");
        }

        #endregion

        #region 窗口事件

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            RunScan();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        #endregion
    }
}
