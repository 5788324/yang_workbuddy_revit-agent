using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace YangTools.Revit.Core
{
    public static class EntityGeneratorEngine
    {
        public static void Execute(UIApplication uiApp, Document doc, Models.EntityGeneratorState state)
        {
            using (TransactionGroup tg = new TransactionGroup(doc, "生成Loft实体"))
            {
                tg.Start();

                try
                {
                    var instanceCurves = new List<List<Curve>>();
                    double minZ = double.MaxValue;

                    foreach (var inst in state.Profiles)
                    {
                        var geomElem = inst.get_Geometry(new Options { ComputeReferences = false });
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
                        throw new InvalidOperationException("选定的实例中未找到足够的有效轮廓曲线(至少需要2个)。");

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
                            throw new InvalidOperationException("无法将提取的曲线组合为闭合轮廓 (CurveLoop)。请确保线段首尾相连。\n" + ex.Message);
                        }
                    }

                    int expectedCurvesCount = profileLoops[0].NumberOfCurves();
                    foreach (var loop in profileLoops)
                    {
                        if (loop.NumberOfCurves() != expectedCurvesCount)
                            throw new InvalidOperationException($"所有轮廓必须具有相同数量的线段！发现一个轮廓有 {expectedCurvesCount} 条线段，另一个有 {loop.NumberOfCurves()} 条线段。");
                    }

                    if (profileLoops[0].HasPlane())
                    {
                        XYZ referenceNormal = profileLoops[0].GetPlane().Normal;
                        for (int i = 1; i < profileLoops.Count; i++)
                        {
                            if (profileLoops[i].HasPlane())
                            {
                                XYZ currentNormal = profileLoops[i].GetPlane().Normal;
                                if (referenceNormal.DotProduct(currentNormal) < 0)
                                    profileLoops[i].Flip();
                            }
                        }
                    }

                    Solid loftSolid;
                    try
                    {
                        loftSolid = GeometryCreationUtilities.CreateLoftGeometry(profileLoops,
                            new SolidOptions(ElementId.InvalidElementId, ElementId.InvalidElementId));
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException("Revit 底层生成放样实体失败，可能是因为轮廓存在自交、非共面或点映射扭曲。\n" + ex.Message);
                    }

                    if (loftSolid == null || loftSolid.Volume <= 0)
                        throw new InvalidOperationException("无法在内存中生成有效的 Loft 实体。");

                    Document famDoc = uiApp.Application.NewFamilyDocument(state.TemplatePath);
                    if (famDoc == null)
                        throw new InvalidOperationException("无法创建族文档。");

                    Family loadedFamily;
                    try
                    {
                        using (Transaction tFam = new Transaction(famDoc, "创建放样实体"))
                        {
                            tFam.Start();

                            FreeFormElement freeForm = FreeFormElement.Create(famDoc, loftSolid);

                            if (state.IsVoid && freeForm != null)
                            {
                                var cutParam = freeForm.get_Parameter(BuiltInParameter.ELEMENT_IS_CUTTING);
                                if (cutParam != null && !cutParam.IsReadOnly)
                                    cutParam.Set(1);

                                if (state.CutWithVoids)
                                {
                                    var cutWithVoidsParam = famDoc.OwnerFamily.get_Parameter(BuiltInParameter.FAMILY_ALLOW_CUT_WITH_VOIDS);
                                    if (cutWithVoidsParam != null && !cutWithVoidsParam.IsReadOnly)
                                        cutWithVoidsParam.Set(1);
                                }
                            }

                            try
                            {
                                Category category = famDoc.Settings.Categories.get_Item(state.TargetCategory);
                                if (category != null)
                                    famDoc.OwnerFamily.FamilyCategory = category;
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine("[YangTools] EntityGeneratorEngine: {0}", ex.Message);
                            }

                            if (!state.IsVoid)
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
                                        catch (Exception ex)
                                        {
                                            System.Diagnostics.Debug.WriteLine("[YangTools] EntityGeneratorEngine: {0}", ex.Message);
                                        }
                                    }
                                }
                            }

                            tFam.Commit();
                        }

                        string tempFile = Path.Combine(Path.GetTempPath(), state.FamilyName + ".rfa");
                        if (File.Exists(tempFile))
                            File.Delete(tempFile);
                        famDoc.SaveAs(tempFile);

                        using (Transaction tLoad = new Transaction(doc, "载入族"))
                        {
                            tLoad.Start();
                            doc.LoadFamily(tempFile, new FamilyOption(), out loadedFamily);
                            tLoad.Commit();
                        }
                    }
                    finally
                    {
                        famDoc.Close(false);
                    }

                    var settings = UserSettings.Load();
                    settings.EntityGeneratorTemplatePath = state.TemplatePath;
                    settings.Save();

                    using (Transaction tPlace = new Transaction(doc, "放置族实例"))
                    {
                        tPlace.Start();
                        var symbolId = loadedFamily.GetFamilySymbolIds().FirstOrDefault();
                        if (symbolId != ElementId.InvalidElementId)
                        {
                            var symbol = doc.GetElement(symbolId) as FamilySymbol;
                            if (!symbol.IsActive)
                                symbol.Activate();

                            Level targetLevel = new FilteredElementCollector(doc)
                                .OfClass(typeof(Level))
                                .Cast<Level>()
                                .FirstOrDefault();

                            FamilyInstance newInst = doc.Create.NewFamilyInstance(XYZ.Zero, symbol,
                                targetLevel, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

                            double offset = minZ - (targetLevel?.Elevation ?? 0);
                            Parameter offsetParam = newInst.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM)
                                ?? newInst.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM)
                                ?? newInst.get_Parameter(BuiltInParameter.INSTANCE_ELEVATION_PARAM);
                            if (offsetParam != null && !offsetParam.IsReadOnly)
                                offsetParam.Set(offset);

                            if (!string.IsNullOrWhiteSpace(state.MaterialName))
                            {
                                var mat = new FilteredElementCollector(doc)
                                    .OfClass(typeof(Material))
                                    .Cast<Material>()
                                    .FirstOrDefault(m => m.Name.Equals(state.MaterialName, StringComparison.OrdinalIgnoreCase));

                                if (mat != null)
                                {
                                    Parameter matParam = newInst.LookupParameter("Structural Material");
                                    if (matParam != null && matParam.StorageType == StorageType.ElementId)
                                        matParam.Set(mat.Id);
                                }
                            }
                        }
                        tPlace.Commit();
                    }

                    tg.Assimilate();
                }
                catch
                {
                    tg.RollBack();
                    throw;
                }
            }
        }

        private static void CloneCurvesFromGeometry(GeometryElement geomElem, List<Curve> clonedCurves)
        {
            foreach (var geomObj in geomElem)
            {
                if (geomObj is Line line)
                {
                    clonedCurves.Add(Line.CreateBound(
                        new XYZ(line.GetEndPoint(0).X, line.GetEndPoint(0).Y, line.GetEndPoint(0).Z),
                        new XYZ(line.GetEndPoint(1).X, line.GetEndPoint(1).Y, line.GetEndPoint(1).Z)));
                }
                else if (geomObj is Arc arc)
                {
                    double midParam = (arc.GetEndParameter(0) + arc.GetEndParameter(1)) / 2.0;
                    XYZ midPt = arc.Evaluate(midParam, false);
                    clonedCurves.Add(Arc.Create(
                        new XYZ(arc.GetEndPoint(0).X, arc.GetEndPoint(0).Y, arc.GetEndPoint(0).Z),
                        new XYZ(arc.GetEndPoint(1).X, arc.GetEndPoint(1).Y, arc.GetEndPoint(1).Z),
                        new XYZ(midPt.X, midPt.Y, midPt.Z)));
                }
                else if (geomObj is Curve curve && curve.IsBound)
                {
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
                    var instanceGeom = geomInst.GetInstanceGeometry();
                    if (instanceGeom != null)
                    {
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
                        }
                    }
                }
            }
        }
    }
}
