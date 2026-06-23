using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using YangTools.Revit.Core;

namespace YangTools.Revit.Commands
{
    public class LineSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            if (elem is CurveElement curveElem)
            {
                return curveElem.GeometryCurve is Line;
            }
            return false;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }

    [Transaction(TransactionMode.Manual)]
    [RibbonButton(panelName: "视图修改区", buttonText: "剖面(By Line)", tooltip: "根据选择的模型线或详图线生成剖面视图", largeIcon: "Icons/generic_32.png", smallIcon: "Icons/generic_16.png")]
    public class SectionByLineCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                Reference selRef = uidoc.Selection.PickObject(ObjectType.Element, new LineSelectionFilter(), "请选择一根模型线或详图线");
                if (selRef == null) return Result.Cancelled;

                CurveElement curveElement = doc.GetElement(selRef) as CurveElement;
                if (curveElement == null) return Result.Cancelled;

                Line line = curveElement.GeometryCurve as Line;
                if (line == null)
                {
                    TaskDialog.Show("错误", "所选图元不是直线。");
                    return Result.Failed;
                }

                XYZ p0 = line.GetEndPoint(0);
                XYZ p1 = line.GetEndPoint(1);
                double length = p0.DistanceTo(p1);

                XYZ dir = (p1 - p0).Normalize();
                // 恢复原点为中心点
                XYZ origin = (p0 + p1) / 2.0;

                XYZ basisX = dir;
                XYZ basisY;
                XYZ basisZ;

                // 判断线是否几乎垂直
                if (Math.Abs(dir.DotProduct(XYZ.BasisZ)) > 0.99)
                {
                    // 垂直线，利用当前视图的右方向来推导剖面的深度方向
                    XYZ viewRight = doc.ActiveView.RightDirection;
                    basisZ = dir.CrossProduct(viewRight).Normalize();
                    if (basisZ.IsZeroLength())
                    {
                        basisZ = dir.CrossProduct(XYZ.BasisX).Normalize();
                    }
                    basisY = basisZ.CrossProduct(dir).Normalize();
                }
                else
                {
                    // 水平或倾斜线，强制剖面向上方向为全局 Z
                    basisZ = dir.CrossProduct(XYZ.BasisZ).Normalize();
                    basisY = basisZ.CrossProduct(dir).Normalize();
                }

                Transform t = Transform.Identity;
                t.Origin = origin;
                t.BasisX = basisX;
                t.BasisY = basisY;
                t.BasisZ = basisZ;

                BoundingBoxXYZ bbox = new BoundingBoxXYZ();
                bbox.Transform = t;

                double halfLength = length / 2.0;
                // X 范围：-L/2 到 L/2 (宽度为 L，原点在中点，完美覆盖 p0 到 p1)
                // Y 范围：-L/2 到 L/2 (高度为 L，以线为中心上下均分)
                // Z 范围：0 到 L (根据用户反馈修改方向)
                bbox.Min = new XYZ(-halfLength, -halfLength, 0);
                bbox.Max = new XYZ(halfLength, halfLength, length);

                // 寻找“剖面”类型的视图族
                ViewFamilyType sectionType = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .FirstOrDefault(x => x.ViewFamily == ViewFamily.Section);

                if (sectionType == null)
                {
                    TaskDialog.Show("错误", "当前项目没有剖面类型的视图族！");
                    return Result.Failed;
                }

                ViewSection newSection = null;

                using (Transaction trans = new Transaction(doc, "创建剖面(By Line)"))
                {
                    trans.Start();
                    newSection = ViewSection.CreateSection(doc, sectionType.Id, bbox);
                    trans.Commit();
                }

                if (newSection != null)
                {
                    // 成功静默返回，不弹窗打断用户工作流
                }

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("错误", "生成剖面失败: " + ex.Message);
                return Result.Failed;
            }
        }
    }
}
