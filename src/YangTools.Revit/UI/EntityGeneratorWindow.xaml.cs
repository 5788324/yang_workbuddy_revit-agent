using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using Microsoft.Win32;
using MediaColor = System.Windows.Media.Color;

namespace YangTools.Revit.UI
{
    public partial class EntityGeneratorWindow : Window
    {
        private readonly UIApplication _uiapp;
        private readonly List<FamilyInstance> _instances;
        private readonly Document _doc;

        public EntityGeneratorWindow(UIApplication uiapp, List<FamilyInstance> instances)
        {
            InitializeComponent();
            ThemeHelper.ApplyToWindow(this);
            _uiapp = uiapp;
            _instances = instances;
            _doc = uiapp.ActiveUIDocument.Document;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Auto detect family template
                var settings = YangTools.Revit.Core.UserSettings.Load();
                string templatePath = settings.EntityGeneratorTemplatePath;
                
                if (string.IsNullOrEmpty(templatePath) || !File.Exists(templatePath))
                {
                    string templateDir = _uiapp.Application.FamilyTemplatePath;
                    if (string.IsNullOrEmpty(templateDir))
                    {
                        templateDir = @"C:\ProgramData\Autodesk\RVT 2025\Family Templates\Chinese"; // Safe fallback
                    }

                    templatePath = Path.Combine(templateDir, "公制常规模型.rft");
                    if (!File.Exists(templatePath))
                    {
                        templatePath = Path.Combine(templateDir, "Generic Model.rft");
                    }
                }
                
                if (File.Exists(templatePath))
                {
                    TxtTemplatePath.Text = templatePath;
                }

                // Populate Levels
                var levels = new FilteredElementCollector(_doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(l => l.Elevation)
                    .ToList();

                foreach (var level in levels)
                {
                    var item = new System.Windows.Controls.ComboBoxItem
                    {
                        Content = level.Name,
                        Tag = level
                    };
                    CmbLevel.Items.Add(item);
                }

                if (CmbLevel.Items.Count > 0)
                    CmbLevel.SelectedIndex = 0;

                // Populate Categories
                var categories = new Dictionary<string, BuiltInCategory>();
                foreach (Category cat in _doc.Settings.Categories)
                {
                    if (cat != null && cat.CategoryType == CategoryType.Model && cat.AllowsBoundParameters && cat.CanAddSubcategory)
                    {
                        // Use Extension/Reflection safe ID getting
#if REVIT2024_OR_GREATER
                        long idVal = cat.Id.Value;
                        if (Enum.IsDefined(typeof(BuiltInCategory), idVal))
                        {
                            var builtIn = (BuiltInCategory)idVal;
                            if (!categories.ContainsKey(cat.Name))
                            {
                                categories.Add(cat.Name, builtIn);
                            }
                        }
#else
                        int idVal = cat.Id.IntegerValue;
                        if (Enum.IsDefined(typeof(BuiltInCategory), idVal))
                        {
                            var builtIn = (BuiltInCategory)idVal;
                            if (!categories.ContainsKey(cat.Name))
                            {
                                categories.Add(cat.Name, builtIn);
                            }
                        }
#endif
                    }
                }

                var sortedCategories = categories.OrderBy(c => c.Key).ToList();
                int defaultIdx = 0;
                for (int i = 0; i < sortedCategories.Count; i++)
                {
                    var cat = sortedCategories[i];
                    var item = new System.Windows.Controls.ComboBoxItem
                    {
                        Content = cat.Key,
                        Tag = cat.Value
                    };
                    CmbCategory.Items.Add(item);
                    if (cat.Value == BuiltInCategory.OST_GenericModel) defaultIdx = i;
                }
                if (CmbCategory.Items.Count > 0) CmbCategory.SelectedIndex = defaultIdx;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化窗口时发生错误：\n{ex.Message}\n\n{ex.StackTrace}", "初始化错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnBrowseTemplate_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Revit Family Template (*.rft)|*.rft",
                Title = "选择族模板"
            };

            if (dlg.ShowDialog() == true)
            {
                TxtTemplatePath.Text = dlg.FileName;
            }
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

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            string templatePath = TxtTemplatePath.Text;
            if (!File.Exists(templatePath))
            {
                MessageBox.Show("族模板路径无效！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (CmbLevel.SelectedItem is not System.Windows.Controls.ComboBoxItem selectedLevelItem ||
                selectedLevelItem.Tag is not Level targetLevel)
            {
                MessageBox.Show("请选择目标标高！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string familyName = TxtFamilyName.Text;
            if (string.IsNullOrWhiteSpace(familyName))
            {
                MessageBox.Show("请输入新建族名称！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string materialName = TxtMaterialName.Text;

            TxtStatus.Text = "正在生成实体...";
            TxtStatus.Foreground = new System.Windows.Media.SolidColorBrush(MediaColor.FromRgb(0x5A, 0x7A, 0x3A)); // success green

            // Perform generation
            try
            {
                using (TransactionGroup tg = new TransactionGroup(_doc, "生成Loft实体"))
                {
                    tg.Start();

                    // CRITICAL FIX: Extract curves and IMMEDIATELY clone them into new objects.
                    // GetInstanceGeometry() returns a transient GeometryElement. Once the GC collects it,
                    // any child Curve objects become dangling pointers, causing Revit fatal errors (access violation).
                    // Solution: deep-clone all curves right away so we own the memory.
                    var instanceCurves = new List<List<Curve>>();
                    double minZ = double.MaxValue;

                    foreach (var inst in _instances)
                    {
                        var geomElem = inst.get_Geometry(new Options() { ComputeReferences = false });
                        if (geomElem == null) continue;

                        var clonedCurves = new List<Curve>();
                        CloneCurvesFromGeometry(geomElem, clonedCurves);

                        if (clonedCurves.Count > 0)
                        {
                            instanceCurves.Add(clonedCurves);
                            foreach (var c in clonedCurves)
                            {
                                minZ = Math.Min(minZ, Math.Min(c.GetEndPoint(0).Z, c.GetEndPoint(1).Z));
                            }
                        }
                    }

                    if (instanceCurves.Count < 2)
                    {
                        throw new Exception("选定的实例中未找到足够的有效轮廓曲线(至少需要2个)。");
                    }

                    // Create CurveLoops for Lofting
                    IList<CurveLoop> profileLoops = new List<CurveLoop>();
                    Transform shiftTransform = Transform.CreateTranslation(new XYZ(0, 0, -minZ));

                    foreach (var curves in instanceCurves)
                    {
                        var shiftedCurves = curves.Select(c => c.CreateTransformed(shiftTransform)).ToList();
                        try 
                        {
                            CurveLoop loop = CurveLoop.Create(shiftedCurves);
                            profileLoops.Add(loop);
                        }
                        catch (Exception ex)
                        {
                            throw new Exception("无法将提取的曲线组合为闭合轮廓 (CurveLoop)。请确保线段首尾相连。\n" + ex.Message);
                        }
                    }

                    // SAFETY CHECK: Ensure all profiles have the same number of curves
                    int expectedCurvesCount = profileLoops[0].NumberOfCurves();
                    foreach (var loop in profileLoops)
                    {
                        if (loop.NumberOfCurves() != expectedCurvesCount)
                        {
                            throw new Exception($"所有轮廓必须具有相同数量的线段！发现一个轮廓有 {expectedCurvesCount} 条线段，另一个有 {loop.NumberOfCurves()} 条线段。");
                        }
                    }

                    // Ensure consistent winding direction
                    if (profileLoops[0].HasPlane())
                    {
                        XYZ referenceNormal = profileLoops[0].GetPlane().Normal;
                        for (int i = 1; i < profileLoops.Count; i++)
                        {
                            if (profileLoops[i].HasPlane())
                            {
                                XYZ currentNormal = profileLoops[i].GetPlane().Normal;
                                if (referenceNormal.DotProduct(currentNormal) < 0)
                                {
                                    profileLoops[i].Flip();
                                }
                            }
                        }
                    }

                    // Generate Solid in memory
                    Solid loftSolid = null;
                    try
                    {
                        loftSolid = GeometryCreationUtilities.CreateLoftGeometry(profileLoops, new SolidOptions(ElementId.InvalidElementId, ElementId.InvalidElementId));
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Revit 底层生成放样实体失败，可能是因为轮廓存在自交、非共面或点映射扭曲。\n" + ex.Message);
                    }

                    if (loftSolid == null || loftSolid.Volume <= 0)
                    {
                        throw new Exception("无法在内存中生成有效的 Loft 实体。");
                    }

                    // Create new family document
                    Document famDoc = _uiapp.Application.NewFamilyDocument(templatePath);
                    if (famDoc == null)
                    {
                        throw new Exception("无法创建族文档。");
                    }

                    Family loadedFamily;
                    try
                    {
                        using (Transaction tFam = new Transaction(famDoc, "创建放样实体"))
                        {
                            tFam.Start();

                            // Insert the Solid into the standard family
                            FreeFormElement freeForm = FreeFormElement.Create(famDoc, loftSolid);

                            // Set Void if requested
                            if (ChkIsVoid.IsChecked == true && freeForm != null)
                            {
                                var cutParam = freeForm.get_Parameter(BuiltInParameter.ELEMENT_IS_CUTTING);
                                if (cutParam != null && !cutParam.IsReadOnly)
                                {
                                    cutParam.Set(1);
                                }

                                if (ChkCutWithVoids.IsChecked == true)
                                {
                                    var cutWithVoidsParam = famDoc.OwnerFamily.get_Parameter(BuiltInParameter.FAMILY_ALLOW_CUT_WITH_VOIDS);
                                    if (cutWithVoidsParam != null && !cutWithVoidsParam.IsReadOnly)
                                    {
                                        cutWithVoidsParam.Set(1);
                                    }
                                }
                            }

                            // Set Family Category
                            if (CmbCategory.SelectedItem is System.Windows.Controls.ComboBoxItem catItem && catItem.Tag is BuiltInCategory targetCat)
                            {
                                try
                                {
                                    Category category = famDoc.Settings.Categories.get_Item(targetCat);
                                    if (category != null)
                                    {
                                        famDoc.OwnerFamily.FamilyCategory = category;
                                    }
                                }
                                catch { }
                            }

                            // Add parameter "Structural Material" ONLY IF NOT VOID
                            if (ChkIsVoid.IsChecked != true)
                            {
#if REVIT2022_OR_GREATER
                                FamilyParameter matParam = famDoc.FamilyManager.AddParameter("Structural Material", 
                                    GroupTypeId.Materials, SpecTypeId.Reference.Material, true);
#else
                                FamilyParameter matParam = famDoc.FamilyManager.AddParameter("Structural Material", 
                                    BuiltInParameterGroup.PG_MATERIALS, ParameterType.Material, true);
#endif

                                if (matParam != null && freeForm != null)
                                {
                                    Parameter formMatParam = freeForm.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM);
                                    if (formMatParam != null)
                                    {
                                        try 
                                        { 
                                            famDoc.FamilyManager.AssociateElementParameterToFamilyParameter(formMatParam, matParam); 
                                        } 
                                        catch { }
                                    }
                                }
                            }

                            tFam.Commit();
                        }

                        // Save famDoc to temp file
                        string tempFile = Path.Combine(Path.GetTempPath(), familyName + ".rfa");
                        if (File.Exists(tempFile))
                        {
                            File.Delete(tempFile);
                        }
                        famDoc.SaveAs(tempFile);

                        // Load into project
                        using (Transaction tLoad = new Transaction(_doc, "载入族"))
                        {
                            tLoad.Start();
                            _doc.LoadFamily(tempFile, new FamilyOption(), out loadedFamily);
                            tLoad.Commit();
                        }
                    }
                    finally
                    {
                        famDoc.Close(false);
                    }

                    // Save template path to settings
                    var settings = YangTools.Revit.Core.UserSettings.Load();
                    settings.EntityGeneratorTemplatePath = templatePath;
                    settings.Save();

                    // Place instance
                    using (Transaction tPlace = new Transaction(_doc, "放置族实例"))
                    {
                        tPlace.Start();
                        var symbolId = loadedFamily.GetFamilySymbolIds().FirstOrDefault();
                        if (symbolId != ElementId.InvalidElementId)
                        {
                            var symbol = _doc.GetElement(symbolId) as FamilySymbol;
                            if (!symbol.IsActive)
                                symbol.Activate();

                            // Insert exactly at XY zero point
                            FamilyInstance newInst = _doc.Create.NewFamilyInstance(XYZ.Zero, symbol, targetLevel, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

                            // Set Base Offset — 模板族的几何已经下移 minZ（底部对齐族原点）
                            // 实例放置在 targetLevel 上，需要正偏移使几何回到原标高
                            double offset = minZ - targetLevel.Elevation;
                            Parameter offsetParam = newInst.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM) 
                                                    ?? newInst.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM)
                                                    ?? newInst.get_Parameter(BuiltInParameter.INSTANCE_ELEVATION_PARAM);
                            if (offsetParam != null && !offsetParam.IsReadOnly)
                            {
                                offsetParam.Set(offset);
                            }

                            // Set material
                            if (!string.IsNullOrWhiteSpace(materialName))
                            {
                                var mat = new FilteredElementCollector(_doc)
                                    .OfClass(typeof(Material))
                                    .Cast<Material>()
                                    .FirstOrDefault(m => m.Name.Equals(materialName, StringComparison.OrdinalIgnoreCase));
                                
                                if (mat != null)
                                {
                                    Parameter newInstMatParam = newInst.LookupParameter("Structural Material");
                                    if (newInstMatParam != null && newInstMatParam.StorageType == StorageType.ElementId)
                                    {
                                        newInstMatParam.Set(mat.Id);
                                    }
                                }
                            }
                        }
                        tPlace.Commit();
                    }

                    tg.Assimilate();
                    
                    MessageBox.Show("实体生成成功！", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"生成过程中发生错误：\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                TxtStatus.Text = "生成失败";
                TxtStatus.Foreground = new System.Windows.Media.SolidColorBrush(MediaColor.FromRgb(0xA0, 0x30, 0x20)); // warning red
            }
        }

        /// <summary>
        /// Deep-clone curves from geometry into new Line/Arc objects.
        /// CRITICAL: GetInstanceGeometry() returns transient geometry that can be GC'd.
        /// If we store references to child Curves, they become dangling pointers = fatal error.
        /// Solution: immediately create NEW curve objects from endpoints so we own the memory.
        /// </summary>
        private void CloneCurvesFromGeometry(GeometryElement geomElem, List<Curve> clonedCurves)
        {
            foreach (var geomObj in geomElem)
            {
                if (geomObj is Line line)
                {
                    // Clone Line by creating a new one from endpoints
                    clonedCurves.Add(Line.CreateBound(
                        new XYZ(line.GetEndPoint(0).X, line.GetEndPoint(0).Y, line.GetEndPoint(0).Z),
                        new XYZ(line.GetEndPoint(1).X, line.GetEndPoint(1).Y, line.GetEndPoint(1).Z)));
                }
                else if (geomObj is Arc arc)
                {
                    // Clone Arc by creating a new one from 3 points (start, mid, end)
                    double midParam = (arc.GetEndParameter(0) + arc.GetEndParameter(1)) / 2.0;
                    XYZ midPt = arc.Evaluate(midParam, false);
                    clonedCurves.Add(Arc.Create(
                        new XYZ(arc.GetEndPoint(0).X, arc.GetEndPoint(0).Y, arc.GetEndPoint(0).Z),
                        new XYZ(arc.GetEndPoint(1).X, arc.GetEndPoint(1).Y, arc.GetEndPoint(1).Z),
                        new XYZ(midPt.X, midPt.Y, midPt.Z)));
                }
                else if (geomObj is Curve curve && curve.IsBound)
                {
                    // For other curve types (NurbSpline, HermiteSpline, etc.), tessellate and approximate with lines
                    var tessPoints = curve.Tessellate();
                    for (int i = 0; i < tessPoints.Count - 1; i++)
                    {
                        var p0 = tessPoints[i];
                        var p1 = tessPoints[i + 1];
                        if (p0.DistanceTo(p1) > 1e-9)
                        {
                            clonedCurves.Add(Line.CreateBound(
                                new XYZ(p0.X, p0.Y, p0.Z),
                                new XYZ(p1.X, p1.Y, p1.Z)));
                        }
                    }
                }
                else if (geomObj is GeometryInstance geomInst)
                {
                    // Recurse into instance geometry, but extract and clone immediately
                    var instanceGeom = geomInst.GetInstanceGeometry();
                    if (instanceGeom != null)
                    {
                        // Process ALL curves from instance geometry RIGHT NOW before instanceGeom can be GC'd
                        foreach (var subObj in instanceGeom)
                        {
                            if (subObj is Line subLine)
                            {
                                clonedCurves.Add(Line.CreateBound(
                                    new XYZ(subLine.GetEndPoint(0).X, subLine.GetEndPoint(0).Y, subLine.GetEndPoint(0).Z),
                                    new XYZ(subLine.GetEndPoint(1).X, subLine.GetEndPoint(1).Y, subLine.GetEndPoint(1).Z)));
                            }
                            else if (subObj is Arc subArc)
                            {
                                double midParam = (subArc.GetEndParameter(0) + subArc.GetEndParameter(1)) / 2.0;
                                XYZ midPt = subArc.Evaluate(midParam, false);
                                clonedCurves.Add(Arc.Create(
                                    new XYZ(subArc.GetEndPoint(0).X, subArc.GetEndPoint(0).Y, subArc.GetEndPoint(0).Z),
                                    new XYZ(subArc.GetEndPoint(1).X, subArc.GetEndPoint(1).Y, subArc.GetEndPoint(1).Z),
                                    new XYZ(midPt.X, midPt.Y, midPt.Z)));
                            }
                            else if (subObj is Curve subCurve && subCurve.IsBound)
                            {
                                var tessPoints = subCurve.Tessellate();
                                for (int i = 0; i < tessPoints.Count - 1; i++)
                                {
                                    var p0 = tessPoints[i];
                                    var p1 = tessPoints[i + 1];
                                    if (p0.DistanceTo(p1) > 1e-9)
                                    {
                                        clonedCurves.Add(Line.CreateBound(
                                            new XYZ(p0.X, p0.Y, p0.Z),
                                            new XYZ(p1.X, p1.Y, p1.Z)));
                                    }
                                }
                            }
                            // Ignore Solid, Point, etc.
                        }
                    }
                }
            }
        }
    }

    public class FamilyOption : IFamilyLoadOptions
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
