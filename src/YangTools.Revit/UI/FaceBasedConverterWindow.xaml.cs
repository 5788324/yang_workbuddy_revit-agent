using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;

namespace YangTools.Revit.UI
{
    public partial class FaceBasedConverterWindow : Window
    {
        private readonly UIApplication _uiapp;
        private readonly Document _doc;
        private List<ElementId> _lastScannedIds = new List<ElementId>();

        public ObservableCollection<FaceBasedTypeItem> TypeItems { get; set; } = new ObservableCollection<FaceBasedTypeItem>();

        public FaceBasedConverterWindow(UIApplication uiapp)
        {
            InitializeComponent();
            ThemeHelper.ApplyToWindow(this);
            _uiapp = uiapp;
            _doc = uiapp.ActiveUIDocument.Document;

            FamilyDataGrid.ItemsSource = TypeItems;

            // Resolve template path
            ResolveTemplatePath();

            // Auto scan
            ScanFaceBasedFamilies();
        }

        private void ResolveTemplatePath()
        {
            try
            {
                string templateDir = _uiapp.Application.FamilyTemplatePath;
                if (string.IsNullOrEmpty(templateDir))
                {
                    templateDir = @"C:\ProgramData\Autodesk\RVT " + _uiapp.Application.VersionNumber + @"\Family Templates\Chinese";
                }

                string templatePath = Path.Combine(templateDir, "公制常规模型.rft");
                if (!File.Exists(templatePath))
                {
                    templatePath = Path.Combine(templateDir, "Generic Model.rft");
                }
                if (!File.Exists(templatePath))
                {
                    // Try English path
                    string engDir = templateDir.Replace("Chinese", "English");
                    templatePath = Path.Combine(engDir, "Metric Generic Model.rft");
                }

                if (File.Exists(templatePath))
                {
                    TxtTemplatePath.Text = templatePath;
                }
            }
            catch { }
        }

        private void ScanFaceBasedFamilies()
        {
            TypeItems.Clear();

            try
            {
                // Collect all FamilyInstance elements
                var instances = new FilteredElementCollector(_doc)
                    .OfClass(typeof(FamilyInstance))
                    .Cast<FamilyInstance>()
                    .Where(fi =>
                    {
                        try
                        {
                            // Check if the instance has a host (face-based indicator)
                            if (fi.Host != null) return true;

                            // Also check hosting behavior parameter on the family
                            var fam = fi.Symbol?.Family;
                            if (fam == null) return false;
                            var hostParam = fam.get_Parameter(BuiltInParameter.FAMILY_HOSTING_BEHAVIOR);
                            if (hostParam != null)
                            {
                                long val = hostParam.AsInteger();
                                return val == 5; // 5 = Face-Based
                            }
                            return false;
                        }
                        catch { return false; }
                    })
                    .ToList();

                // Group by FamilySymbol
                var grouped = instances
                    .GroupBy(fi => fi.Symbol?.Id ?? ElementId.InvalidElementId)
                    .Where(g => g.Key != ElementId.InvalidElementId)
                    .ToList();

                foreach (var group in grouped)
                {
                    var firstInst = group.First();
                    var symbol = firstInst.Symbol;
                    if (symbol == null) continue;

                    string hostCat = "";
                    try
                    {
                        var host = firstInst.Host;
                        hostCat = host?.Category?.Name ?? "未知";
                    }
                    catch { hostCat = "未知"; }

                    TypeItems.Add(new FaceBasedTypeItem
                    {
                        IsSelected = true,
                        FamilyName = symbol.Family?.Name ?? "未知",
                        TypeName = symbol.Name ?? "未知",
                        InstanceCount = group.Count(),
                        HostCategory = hostCat,
                        SymbolId = symbol.Id,
                        InstanceIds = group.Select(fi => fi.Id).ToList()
                    });
                }

                TxtCount.Text = $"共 {TypeItems.Count} 个基于面族类型 ({instances.Count} 个实例)";
                TxtStatus.Text = $"扫描完成，发现 {TypeItems.Count} 个基于面的族类型";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "扫描失败: " + ex.Message;
            }
        }

        private void ScanFromSelection(ICollection<ElementId> selectedIds)
        {
            _lastScannedIds = selectedIds.ToList();
            RefreshUIList();
        }

        private string GetOrientationHash(FamilyInstance inst)
        {
            try
            {
                var t = inst.GetTransform();
                return $"{Math.Round(t.BasisX.X, 3)}_{Math.Round(t.BasisX.Y, 3)}_{Math.Round(t.BasisX.Z, 3)}_" +
                       $"{Math.Round(t.BasisZ.X, 3)}_{Math.Round(t.BasisZ.Y, 3)}_{Math.Round(t.BasisZ.Z, 3)}";
            }
            catch { return "default"; }
        }

        private void RefreshUIList()
        {
            TypeItems.Clear();

            try
            {
                var instances = _lastScannedIds
                    .Select(id => _doc.GetElement(id))
                    .OfType<FamilyInstance>()
                    .Where(fi =>
                    {
                        try { return fi.Host != null || IsFamilyFaceBased(fi.Symbol?.Family); }
                        catch { return false; }
                    })
                    .ToList();

                bool bakeRotation = cbBakeRotation.IsChecked == true;

                var groupedBySymbol = instances
                    .GroupBy(fi => fi.Symbol?.Id ?? ElementId.InvalidElementId)
                    .Where(g => g.Key != ElementId.InvalidElementId)
                    .ToList();

                foreach (var symbolGroup in groupedBySymbol)
                {
                    if (bakeRotation)
                    {
                        var orientationGroups = symbolGroup.GroupBy(fi => GetOrientationHash(fi)).ToList();
                        int index = 1;
                        bool multiple = orientationGroups.Count > 1;
                        
                        foreach (var og in orientationGroups)
                        {
                            var firstInst = og.First();
                            var symbol = firstInst.Symbol;
                            string hostCat = "";
                            try { hostCat = firstInst.Host?.Category?.Name ?? "未知"; } catch { hostCat = "未知"; }
                            
                            string suffix = multiple ? $"_{index}" : "";
                            
                            TypeItems.Add(new FaceBasedTypeItem
                            {
                                IsSelected = true,
                                FamilyName = symbol.Family?.Name ?? "未知",
                                TypeName = symbol.Name + (multiple ? $" (朝向 {index})" : ""),
                                ConvertedSuffix = suffix,
                                InstanceCount = og.Count(),
                                HostCategory = hostCat,
                                SymbolId = symbol.Id,
                                InstanceIds = og.Select(i => i.Id).ToList()
                            });
                            index++;
                        }
                    }
                    else
                    {
                        var firstInst = symbolGroup.First();
                        var symbol = firstInst.Symbol;
                        string hostCat = "";
                        try { hostCat = firstInst.Host?.Category?.Name ?? "未知"; } catch { hostCat = "未知"; }

                        TypeItems.Add(new FaceBasedTypeItem
                        {
                            IsSelected = true,
                            FamilyName = symbol.Family?.Name ?? "未知",
                            TypeName = symbol.Name ?? "未知",
                            ConvertedSuffix = "",
                            HostCategory = hostCat,
                            InstanceCount = symbolGroup.Count(),
                            SymbolId = symbol.Id,
                            InstanceIds = symbolGroup.Select(i => i.Id).ToList()
                        });
                    }
                }

                TxtCount.Text = $"共 {TypeItems.Count} 个基于面族类型";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "扫描失败: " + ex.Message;
            }
        }

        private bool IsFamilyFaceBased(Family fam)
        {
            if (fam == null) return false;
            try
            {
                var hostParam = fam.get_Parameter(BuiltInParameter.FAMILY_HOSTING_BEHAVIOR);
                if (hostParam != null) return hostParam.AsInteger() == 5;
            }
            catch { }
            return false;
        }

        // ========== Extract Geometry from a FamilyInstance ==========
        private Tuple<List<Solid>, List<Curve>> ExtractGeometry(FamilyInstance instance, bool bakeRotation)
        {
            var solids = new List<Solid>();
            var curves = new List<Curve>();
            try
            {
                var options = new Options
                {
                    ComputeReferences = false,
                    DetailLevel = ViewDetailLevel.Fine
                };

                var geomElem = instance.get_Geometry(options);
                if (geomElem == null) return Tuple.Create(solids, curves);

                Transform tApply;
                if (bakeRotation)
                {
                    XYZ locationPoint = XYZ.Zero;
                    if (instance.Location is LocationPoint lp)
                    {
                        locationPoint = lp.Point;
                    }
                    else
                    {
                        var bbox = instance.get_BoundingBox(null);
                        if (bbox != null) locationPoint = (bbox.Min + bbox.Max) / 2.0;
                    }
                    tApply = Transform.CreateTranslation(-locationPoint);
                }
                else
                {
                    tApply = instance.GetTransform().Inverse;
                }

                CollectGeometry(geomElem, tApply, solids, curves);
            }
            catch { }
            return Tuple.Create(solids, curves);
        }

        private void CollectGeometry(GeometryElement geomElem, Transform transform, List<Solid> solids, List<Curve> curves)
        {
            foreach (GeometryObject geomObj in geomElem)
            {
                if (geomObj is Solid solid && solid.Volume > 0)
                {
                    // Transform solid back to origin coordinates
                    try
                    {
                        Solid transformed = SolidUtils.CreateTransformed(solid, transform);
                        if (transformed != null && transformed.Volume > 0)
                            solids.Add(transformed);
                        else
                            solids.Add(solid);
                    }
                    catch
                    {
                        solids.Add(solid);
                    }
                }
                else if (geomObj is Curve curve)
                {
                    try
                    {
                        Curve transformed = curve.CreateTransformed(transform);
                        if (transformed != null)
                            curves.Add(transformed);
                        else
                            curves.Add(curve);
                    }
                    catch
                    {
                        curves.Add(curve);
                    }
                }
                else if (geomObj is GeometryInstance geomInst)
                {
                    var instGeom = geomInst.GetInstanceGeometry();
                    if (instGeom != null)
                    {
                        CollectGeometry(instGeom, transform, solids, curves);
                    }
                }
            }
        }

        // ========== Create new non-face-based family ==========
        private string CreateConvertedFamily(string familyName, string typeName, string suffix, List<Solid> solids, List<Curve> curves, string templatePath)
        {
            string newFamilyName = familyName + "_Converted" + suffix;
            string safeName = string.Join("_", newFamilyName.Split(Path.GetInvalidFileNameChars()));
            string tempFile = Path.Combine(Path.GetTempPath(), "YangTools_FBConvert", safeName + ".rfa");

            Directory.CreateDirectory(Path.GetDirectoryName(tempFile));

            if (File.Exists(tempFile)) File.Delete(tempFile);

            Document famDoc = _uiapp.Application.NewFamilyDocument(templatePath);
            if (famDoc == null) throw new Exception($"无法使用模板 [{templatePath}] 创建族文档。");

            try
            {
                using (Transaction t = new Transaction(famDoc, "创建转换族"))
                {
                    t.Start();

                    foreach (var solid in solids)
                    {
                        try
                        {
                            FreeFormElement.Create(famDoc, solid);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[YangTools] FreeFormElement.Create failed for solid: {ex.Message}");
                        }
                    }

                    // Build ModelCurves for extracted lines
                    foreach (var curve in curves)
                    {
                        try
                        {
                            Plane plane = null;
                            if (curve is Line line)
                            {
                                XYZ pt1 = line.GetEndPoint(0);
                                XYZ dir = line.Direction;
                                XYZ up = XYZ.BasisZ;
                                if (dir.IsAlmostEqualTo(up) || dir.IsAlmostEqualTo(-up)) up = XYZ.BasisX;
                                XYZ normal = dir.CrossProduct(up).Normalize();
                                plane = Plane.CreateByNormalAndOrigin(normal, pt1);
                            }
                            else if (curve is Arc arc)
                            {
                                plane = Plane.CreateByNormalAndOrigin(arc.Normal, arc.Center);
                            }
                            else
                            {
                                // For other curve types like splines, attempt an arbitrary normal
                                XYZ pt1 = curve.GetEndPoint(0);
                                XYZ pt2 = curve.GetEndPoint(1);
                                XYZ dir = (pt2 - pt1).Normalize();
                                XYZ up = XYZ.BasisZ;
                                if (dir.IsAlmostEqualTo(up) || dir.IsAlmostEqualTo(-up)) up = XYZ.BasisX;
                                XYZ normal = dir.CrossProduct(up).Normalize();
                                plane = Plane.CreateByNormalAndOrigin(normal, pt1);
                            }

                            if (plane != null)
                            {
                                SketchPlane sp = SketchPlane.Create(famDoc, plane);
                                famDoc.FamilyCreate.NewModelCurve(curve, sp);
                            }
                        }
                        catch (Exception) { /* Skip if a specific curve fails to rebuild */ }
                    }

                    // Set category to match original (Generic Models)
                    try
                    {
                        var category = famDoc.Settings.Categories.get_Item(BuiltInCategory.OST_GenericModel);
                        if (category != null) famDoc.OwnerFamily.FamilyCategory = category;

                        // Allow free 3D rotation
                        var alwaysVertical = famDoc.OwnerFamily.get_Parameter(BuiltInParameter.FAMILY_ALWAYS_VERTICAL);
                        if (alwaysVertical != null && !alwaysVertical.IsReadOnly) alwaysVertical.Set(0);
                        
                        var workPlaneBased = famDoc.OwnerFamily.get_Parameter(BuiltInParameter.FAMILY_WORK_PLANE_BASED);
                        if (workPlaneBased != null && !workPlaneBased.IsReadOnly) workPlaneBased.Set(0);
                    }
                    catch { }

                    t.Commit();
                }

                famDoc.SaveAs(tempFile);
            }
            finally
            {
                famDoc.Close(false);
            }

            return tempFile;
        }

        // ========== Main Conversion Logic ==========
        private void StartConvert_Click(object sender, RoutedEventArgs e)
        {
            var selectedTypes = TypeItems.Where(t => t.IsSelected).ToList();
            if (selectedTypes.Count == 0)
            {
                TaskDialog.Show("提示", "请先勾选要转换的族类型。");
                return;
            }

            string templatePath = TxtTemplatePath.Text;
            if (!File.Exists(templatePath))
            {
                TaskDialog.Show("提示", "请先设置有效的族模板路径（公制常规模型.rft）。");
                return;
            }

            bool deleteOld = ChkDeleteOld.IsChecked == true;

            TxtStatus.Text = "正在转换...";
            IsEnabled = false;

            try
            {
                int totalConverted = 0;
                int totalFailed = 0;
                var failMessages = new List<string>();

                using (TransactionGroup tg = new TransactionGroup(_doc, "基于面族批量转换"))
                {
                    tg.Start();

                    foreach (var typeItem in selectedTypes)
                    {
                        try
                        {
                            TxtStatus.Text = $"正在转换: {typeItem.FamilyName} - {typeItem.TypeName}...";

                            // 1. Get a representative instance to extract geometry
                            var repInstance = _doc.GetElement(typeItem.InstanceIds.First()) as FamilyInstance;
                            if (repInstance == null) continue;

                            // 2. Extract geometry (Solids & Curves)
                            bool bakeRotation = cbBakeRotation.IsChecked == true;
                            var geom = ExtractGeometry(repInstance, bakeRotation);
                            var solids = geom.Item1;
                            var curves = geom.Item2;
                            if (solids.Count == 0 && curves.Count == 0)
                            {
                                failMessages.Add($"[{typeItem.FamilyName}-{typeItem.TypeName}]: 无法提取任何几何体或线条");
                                totalFailed++;
                                continue;
                            }

                            // 3. Create new family
                            string rfaPath = CreateConvertedFamily(typeItem.FamilyName, typeItem.TypeName, typeItem.ConvertedSuffix, solids, curves, templatePath);

                            // 4. Load into project
                            Family loadedFamily;
                            using (Transaction tLoad = new Transaction(_doc, "载入转换族"))
                            {
                                tLoad.Start();
                                _doc.LoadFamily(rfaPath, new OverwriteFamilyOption(), out loadedFamily);
                                tLoad.Commit();
                            }

                            if (loadedFamily == null)
                            {
                                failMessages.Add($"[{typeItem.FamilyName}-{typeItem.TypeName}]: 载入族失败");
                                totalFailed++;
                                continue;
                            }

                            // 5. Get the new FamilySymbol
                            var newSymbolId = loadedFamily.GetFamilySymbolIds().FirstOrDefault();
                            if (newSymbolId == null || newSymbolId == ElementId.InvalidElementId)
                            {
                                failMessages.Add($"[{typeItem.FamilyName}-{typeItem.TypeName}]: 无法获取新族类型");
                                totalFailed++;
                                continue;
                            }

                            var newSymbol = _doc.GetElement(newSymbolId) as FamilySymbol;

                            // 6. Place new instances and optionally delete old
                            using (Transaction tReplace = new Transaction(_doc, "替换实例"))
                            {
                                tReplace.Start();

                                // Rename type to original type name + _Converted
                                try
                                {
                                    string newTypeName = typeItem.TypeName + "_Converted";
                                    if (newSymbol.Name != newTypeName)
                                    {
                                        newSymbol.Name = newTypeName;
                                    }
                                }
                                catch { }

                                if (!newSymbol.IsActive) newSymbol.Activate();

                                foreach (var oldId in typeItem.InstanceIds)
                                {
                                    try
                                    {
                                        var oldInst = _doc.GetElement(oldId) as FamilyInstance;
                                        if (oldInst == null) continue;

                                        // Get position
                                        XYZ location = XYZ.Zero;
                                        double rotation = 0;
                                        if (oldInst.Location is LocationPoint lp)
                                        {
                                            location = lp.Point;
                                            rotation = lp.Rotation;
                                        }
                                        else
                                        {
                                            // Fallback: use bounding box center
                                            var bb = oldInst.get_BoundingBox(null);
                                            if (bb != null) location = (bb.Min + bb.Max) * 0.5;
                                        }

                                        // Get level
                                        Level level = null;
                                        try
                                        {
                                            var levelId = oldInst.LevelId;
                                            if (levelId != null && levelId != ElementId.InvalidElementId)
                                                level = _doc.GetElement(levelId) as Level;
                                        }
                                        catch { }

                                        if (level == null)
                                        {
                                            // Fallback to lowest level
                                            level = new FilteredElementCollector(_doc)
                                                .OfClass(typeof(Level))
                                                .Cast<Level>()
                                                .OrderBy(l => l.Elevation)
                                                .FirstOrDefault();
                                        }

                                        // Place new instance
                                        FamilyInstance newInst = _doc.Create.NewFamilyInstance(
                                            location, newSymbol, level,
                                            Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

                                        // Set rotation
                                        if (!bakeRotation && Math.Abs(rotation) > 1e-6)
                                        {
                                            XYZ axis1 = location;
                                            XYZ axis2 = location + XYZ.BasisZ;
                                            Line rotAxis = Line.CreateBound(axis1, axis2);
                                            ElementTransformUtils.RotateElement(_doc, newInst.Id, rotAxis, rotation);
                                        }

                                        // Compute correct base offset
                                        try
                                        {
                                            var newOffset = newInst.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM);
                                            if (newOffset == null) newOffset = newInst.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM);

                                            if (newOffset != null && !newOffset.IsReadOnly)
                                            {
                                                double targetElevation = location.Z;
                                                double levelElevation = level != null ? level.Elevation : 0;
                                                newOffset.Set(targetElevation - levelElevation);
                                            }
                                        }
                                        catch { }

                                        // Copy writable instance parameters
                                        CopyInstanceParameters(oldInst, newInst);

                                        totalConverted++;
                                    }
                                    catch { }
                                }

                                // Delete old instances if requested
                                if (deleteOld)
                                {
                                    foreach (var oldId in typeItem.InstanceIds)
                                    {
                                        try { _doc.Delete(oldId); } catch { }
                                    }
                                }

                                tReplace.Commit();
                            }
                        }
                        catch (Exception ex)
                        {
                            failMessages.Add($"[{typeItem.FamilyName}-{typeItem.TypeName}]: {ex.Message}");
                            totalFailed++;
                        }
                    }

                    tg.Assimilate();
                }

                // Report results
                string resultMsg = $"转换完成！\n\n成功转换: {totalConverted} 个实例\n失败: {totalFailed} 个类型";
                if (failMessages.Count > 0)
                {
                    resultMsg += "\n\n失败详情:\n" + string.Join("\n", failMessages);
                }

                TaskDialog.Show("转换结果", resultMsg);
                TxtStatus.Text = $"完成: 成功 {totalConverted} 个, 失败 {totalFailed} 个";

                if (totalConverted > 0)
                {
                    ScanFaceBasedFamilies(); // Refresh the list
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("错误", "转换过程中发生错误:\n" + ex.Message);
                TxtStatus.Text = "转换失败";
            }
            finally
            {
                IsEnabled = true;
            }
        }

        private void CopyInstanceParameters(FamilyInstance source, FamilyInstance target)
        {
            try
            {
                foreach (Parameter srcParam in source.GetOrderedParameters())
                {
                    try
                    {
                        if (srcParam == null || srcParam.IsReadOnly) continue;
                        string name = srcParam.Definition?.Name;
                        if (string.IsNullOrEmpty(name)) continue;

                        // Skip built-in structural params that don't apply
                        if (srcParam.Id.GetIdValue() < 0) continue; // Built-in parameter, skip

                        var tgtParam = target.LookupParameter(name);
                        if (tgtParam == null || tgtParam.IsReadOnly) continue;
                        if (tgtParam.StorageType != srcParam.StorageType) continue;

                        switch (srcParam.StorageType)
                        {
                            case StorageType.String:
                                tgtParam.Set(srcParam.AsString() ?? "");
                                break;
                            case StorageType.Double:
                                tgtParam.Set(srcParam.AsDouble());
                                break;
                            case StorageType.Integer:
                                tgtParam.Set(srcParam.AsInteger());
                                break;
                            case StorageType.ElementId:
                                tgtParam.Set(srcParam.AsElementId());
                                break;
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        // ========== UI Event Handlers ==========
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) this.DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void RefreshList_Click(object sender, RoutedEventArgs e)
        {
            ScanFaceBasedFamilies();
        }

        private void SelectFromView_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            try
            {
                var sel = _uiapp.ActiveUIDocument.Selection;
                var picked = sel.PickObjects(Autodesk.Revit.UI.Selection.ObjectType.Element,
                    "请选择要转换的基于面族实例 (选择完成后按ESC或右键确认)");

                if (picked != null && picked.Count > 0)
                {
                    var ids = picked.Select(p => p.ElementId).ToList();
                    ScanFromSelection(ids);
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // User cancelled - that's fine
            }
            catch (Exception ex)
            {
                TaskDialog.Show("错误", ex.Message);
            }
            finally
            {
                this.ShowDialog();
            }
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            bool selectAll = ChkSelectAll.IsChecked == true;
            foreach (var item in TypeItems)
            {
                item.IsSelected = selectAll;
            }
        }

        private void cbBakeRotation_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (_lastScannedIds != null && _lastScannedIds.Count > 0)
            {
                RefreshUIList();
            }
        }

        private void BrowseTemplate_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Revit Family Template (*.rft)|*.rft",
                Title = "选择常规模型族模板"
            };
            if (!string.IsNullOrEmpty(TxtTemplatePath.Text))
            {
                dlg.InitialDirectory = Path.GetDirectoryName(TxtTemplatePath.Text);
            }
            if (dlg.ShowDialog() == true)
            {
                TxtTemplatePath.Text = dlg.FileName;
            }
        }
    }

    // ========== View Models ==========
    public class FaceBasedTypeItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }

        public string FamilyName { get; set; }
        public string TypeName { get; set; }
        public string ConvertedSuffix { get; set; }
        public int InstanceCount { get; set; }
        public string HostCategory { get; set; }
        public ElementId SymbolId { get; set; }
        public List<ElementId> InstanceIds { get; set; } = new List<ElementId>();

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class OverwriteFamilyOption : IFamilyLoadOptions
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
