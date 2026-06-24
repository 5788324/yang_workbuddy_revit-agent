using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton(panelName: "模型修改区", buttonText: "布尔几何", tooltip: "对两个族实例进行布尔运算(连接/剪切/融合)", largeIcon: "Icons/boolean_geometry_32.png", smallIcon: "Icons/boolean_geometry_16.png")]
    public class BooleanGeometryCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiapp = commandData.Application;
                var uidoc = uiapp.ActiveUIDocument;
                var doc = uidoc.Document;

                var state = new UI.BooleanGeometryState();

                while (true)
                {
                    var win = new UI.BooleanGeometryWindow(uiapp, state);
                    var helper = new System.Windows.Interop.WindowInteropHelper(win)
                    {
                        Owner = uiapp.MainWindowHandle
                    };
                    win.ShowDialog();

                    state = win.State;

                    if (win.ActionToPerform == "PickMain")
                    {
                        try
                        {
                            state.MainInst.Clear();
                            var r = uidoc.Selection.PickObject(Autodesk.Revit.UI.Selection.ObjectType.Element, "请选择主体实例");
                            if (doc.GetElement(r) is FamilyInstance fi) state.MainInst.Add(fi);
                        }
                        catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
                    }
                    else if (win.ActionToPerform == "PickUnion")
                    {
                        try
                        {
                            state.UnionInsts.Clear();
                            var rs = uidoc.Selection.PickObjects(Autodesk.Revit.UI.Selection.ObjectType.Element, "请选择融合实例(可多选)");
                            foreach (var r in rs) if (doc.GetElement(r) is FamilyInstance fi) state.UnionInsts.Add(fi);
                        }
                        catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
                    }
                    else if (win.ActionToPerform == "PickCut")
                    {
                        try
                        {
                            state.CutInsts.Clear();
                            var rs = uidoc.Selection.PickObjects(Autodesk.Revit.UI.Selection.ObjectType.Element, "请选择剪切实例(可多选)");
                            foreach (var r in rs) if (doc.GetElement(r) is FamilyInstance fi) state.CutInsts.Add(fi);
                        }
                        catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
                    }
                    else if (win.ActionToPerform == "PickJoin")
                    {
                        try
                        {
                            state.JoinInsts.Clear();
                            var rs = uidoc.Selection.PickObjects(Autodesk.Revit.UI.Selection.ObjectType.Element, "请选择连接实例(可多选)");
                            foreach (var r in rs) if (doc.GetElement(r) is FamilyInstance fi) state.JoinInsts.Add(fi);
                        }
                        catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
                    }
                    else if (win.IsExecute)
                    {
                        break; // Proceed to transaction
                    }
                    else
                    {
                        // User closed or cancelled
                        return Result.Cancelled;
                    }
                }

                if (state.MainInst.Count == 0)
                {
                    TaskDialog.Show("提示", "请先选择主体实例。");
                    return Result.Cancelled;
                }

                var mainFi = state.MainInst[0];
                Document? famDoc = null;
                string? tempFile = null;

                try
                {
                    famDoc = doc.EditFamily(mainFi.Symbol.Family);
                    if (famDoc == null) throw new InvalidOperationException("无法编辑主体实例的族。");

                    Transform mainInv = mainFi.GetTransform().Inverse;

                    List<Solid> cutSolids = GetLocalSolids(state.CutInsts, mainInv);
                    List<Solid> unionSolids = GetLocalSolids(state.UnionInsts, mainInv);
                    List<Solid> joinSolids = GetLocalSolids(state.JoinInsts, mainInv);

                    using (Transaction tFam = new Transaction(famDoc, "布尔运算"))
                    {
                        tFam.Start();

                        var forms = new FilteredElementCollector(famDoc).OfClass(typeof(GenericForm)).Cast<GenericForm>().ToList();

                        List<Solid> mergeableSolids = new List<Solid>();
                        foreach (var form in forms)
                        {
                            var geom = form.get_Geometry(new Options { ComputeReferences = true });
                            if (geom != null)
                            {
                                var solids = new List<Solid>();
                                ExtractSolids(geom, solids);
                                mergeableSolids.AddRange(solids.Where(s => s.Volume > 0));
                            }
                        }

                        if (mergeableSolids.Count == 0) throw new InvalidOperationException("无法获取主体实例的内部实体几何。");

                        List<Solid> independentSolids = new List<Solid>();

                        // 1. 先融合
                        foreach (var unionS in unionSolids)
                        {
                            Solid current = unionS;
                            List<Solid> nextMergeable = new List<Solid>();
                            foreach (var m in mergeableSolids)
                            {
                                try
                                {
                                    current = BooleanOperationsUtils.ExecuteBooleanOperation(current, m, BooleanOperationsType.Union);
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[YangTools] 布尔融合失败: {ex.Message}");
                                    nextMergeable.Add(m);
                                }
                            }
                            nextMergeable.Add(current);
                            mergeableSolids = nextMergeable;
                        }

                        // 2. 再剪切
                        foreach (var cutS in cutSolids)
                        {
                            bool intersected = false;
                            List<Solid> nextMergeable = new List<Solid>();
                            foreach (var m in mergeableSolids)
                            {
                                try
                                {
                                    var diff = BooleanOperationsUtils.ExecuteBooleanOperation(m, cutS, BooleanOperationsType.Difference);
                                    if (diff != null && diff.Volume > 0)
                                    {
                                        nextMergeable.Add(diff);
                                        intersected = true;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[YangTools] 布尔剪切失败: {ex.Message}");
                                    nextMergeable.Add(m);
                                }
                            }
                            mergeableSolids = nextMergeable;

                            if (!intersected)
                            {
                                independentSolids.Add(cutS);
                            }
                        }

                        // 3. 再连接
                        independentSolids.AddRange(joinSolids);

                        // 删除原有的几何体并重新生成主体
                        foreach (var form in forms)
                        {
                            try { famDoc.Delete(form.Id); }
                            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[YangTools] 删除旧几何体失败({form.Id}): {ex.Message}"); }
                        }

                        foreach (var m in mergeableSolids)
                        {
                            if (m.Volume > 0) FreeFormElement.Create(famDoc, m);
                        }

                        foreach (var ind in independentSolids)
                        {
                            if (ind.Volume > 0) FreeFormElement.Create(famDoc, ind);
                        }

                        tFam.Commit();
                    }

                    // Save and Load
                    tempFile = Path.Combine(Path.GetTempPath(), mainFi.Symbol.Family.Name + ".rfa");
                    if (File.Exists(tempFile)) File.Delete(tempFile);
                    famDoc.SaveAs(tempFile);

                    // 先关闭族文档再加载
                    famDoc.Close(false);
                    famDoc = null;

                    Family loadedFamily;
                    using (Transaction tLoad = new Transaction(doc, "载入族"))
                    {
                        tLoad.Start();
                        doc.LoadFamily(tempFile, new FamilyOption(), out loadedFamily);

                        // Handle deletion of targets
                        var allTargets = state.UnionInsts.Concat(state.CutInsts).Concat(state.JoinInsts).ToList();
                        if (state.DeleteTargetInst && !state.DeleteTargetFam)
                        {
                            foreach (var target in allTargets)
                            {
                                try { doc.Delete(target.Id); }
                                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[YangTools] 删除目标实例失败({target.Id}): {ex.Message}"); }
                            }
                        }
                        if (state.DeleteTargetFam)
                        {
                            foreach (var target in allTargets)
                            {
                                try { doc.Delete(target.Symbol.Family.Id); }
                                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[YangTools] 删除目标族失败: {ex.Message}"); }
                            }
                        }

                        tLoad.Commit();
                    }
                }
                finally
                {
                    // 确保族文档在任何情况下都被关闭
                    if (famDoc != null)
                    {
                        try { famDoc.Close(false); }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[YangTools] 关闭族文档失败: {ex.Message}"); }
                    }
                }

                TaskDialog.Show("完成", "批处理执行成功！");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("错误", $"操作失败。\n错误信息: {ex.Message}");
                message = ex.Message;
                return Result.Failed;
            }
        }

        private List<Solid> GetLocalSolids(List<FamilyInstance> instances, Transform mainInv)
        {
            List<Solid> result = new List<Solid>();
            foreach (var inst in instances)
            {
                var geom = inst.get_Geometry(new Options { ComputeReferences = true });
                if (geom != null)
                {
                    var solids = new List<Solid>();
                    ExtractSolids(geom, solids);
                    foreach (var s in solids)
                    {
                        if (s.Volume > 0)
                        {
                            result.Add(SolidUtils.CreateTransformed(s, mainInv));
                        }
                    }
                }
            }
            return result;
        }

        private void ExtractSolids(GeometryElement geomElem, List<Solid> solids)
        {
            foreach (var geomObj in geomElem)
            {
                if (geomObj is Solid solid)
                {
                    if (solid.Volume > 0) solids.Add(solid);
                }
                else if (geomObj is GeometryInstance geomInst)
                {
                    ExtractSolids(geomInst.GetInstanceGeometry(), solids);
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
