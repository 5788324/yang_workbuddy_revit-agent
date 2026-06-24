using System;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using YangTools.Revit.Core;

namespace YangTools.Revit.Commands
{
    public class PipeSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is Pipe;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }

    public class TextSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is TextElement || elem is TextNote;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }

    [Transaction(TransactionMode.Manual)]
    [RibbonButton("MEP工具", "管道修改 TypeA", "根据文字内容（例如 A1 4.18-225）自动修改管道的注释、尺寸和一端高程", "Icons/pipe_modify_32.png", "Icons/pipe_modify_16.png")]
    public class PipeModifyTypeACommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null)
            {
                TaskDialog.Show("提示", "请先打开一个项目文档。");
                return Result.Cancelled;
            }
            Document doc = uidoc.Document;

            try
            {
                while (true)
                {
                    // 1. Select Pipe
                    Reference pipeRef = uidoc.Selection.PickObject(ObjectType.Element, new PipeSelectionFilter(), "请选择一根管道 (按 ESC 退出)");
                    Pipe pipe = doc.GetElement(pipeRef) as Pipe;
                    if (pipe == null) continue;

                    // 2. Select TextNote
                    Reference textRef = uidoc.Selection.PickObject(ObjectType.Element, new TextSelectionFilter(), "请选择包含参数信息的文字 (例如: A1 4.18-225)");
                    TextElement textElement = doc.GetElement(textRef) as TextElement;
                    if (textElement == null) continue;

                    string textStr = textElement.Text;
                    if (string.IsNullOrWhiteSpace(textStr))
                    {
                        TaskDialog.Show("错误", "所选文字内容为空！");
                        continue;
                    }

                    // 3. Parse Text
                    // Format: "A1 +4.18-225" or "A1 4.18-225"
                    var match = Regex.Match(textStr.Trim(), @"^([A-Za-z0-9]+)\s+([+-]?[\d\.]+)-([\d\.]+)$");
                    if (!match.Success)
                    {
                        // 容错处理：有时可能有多个空格，或稍微不同的格式，如没有前缀等。
                        // 为了最大兼容性，尝试在整个字符串中寻找类似的模式： Prefix Height-Diameter
                        match = Regex.Match(textStr, @"([A-Za-z0-9]+)[\s\n]+([+-]?[\d\.]+)\s*-\s*([\d\.]+)");
                        if (!match.Success)
                        {
                            TaskDialog.Show("错误", $"未能从文字中提取出需要的信息。\n文字内容: '{textStr}'\n期望格式: 类似 'A1 4.18-225'");
                            continue;
                        }
                    }

                    string prefix = match.Groups[1].Value;
                    double heightMeters = double.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                    double diameterMm = double.Parse(match.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);

                    double heightFeet = heightMeters * 3.280839895013123;
                    double diameterFeet = diameterMm * 3.280839895013123 / 1000.0;

                    XYZ textPos = textElement.Coord;

                    using (Transaction t = new Transaction(doc, "管道修改 TypeA"))
                    {
                        t.Start();

                        // 4. Update Comments
                        Parameter commentsParam = pipe.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                        if (commentsParam != null && !commentsParam.IsReadOnly)
                        {
                            commentsParam.Set(prefix);
                        }

                        // 5. Update Diameter
                        Parameter diameterParam = pipe.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
                        if (diameterParam != null && !diameterParam.IsReadOnly)
                        {
                            diameterParam.Set(diameterFeet);
                        }

                        // 6. Update Height of nearest end
                        LocationCurve locCurve = pipe.Location as LocationCurve;
                        if (locCurve != null && locCurve.Curve is Line line)
                        {
                            XYZ pt0 = line.GetEndPoint(0);
                            XYZ pt1 = line.GetEndPoint(1);

                            // If the pipe is drawn in a plan view, textPos is 2D/3D. We should measure 2D distance just in case Z differs wildly
                            double dist0 = new XYZ(pt0.X, pt0.Y, 0).DistanceTo(new XYZ(textPos.X, textPos.Y, 0));
                            double dist1 = new XYZ(pt1.X, pt1.Y, 0).DistanceTo(new XYZ(textPos.X, textPos.Y, 0));

                            if (dist0 < dist1)
                            {
                                XYZ newPt0 = new XYZ(pt0.X, pt0.Y, heightFeet);
                                locCurve.Curve = Line.CreateBound(newPt0, pt1);
                            }
                            else
                            {
                                XYZ newPt1 = new XYZ(pt1.X, pt1.Y, heightFeet);
                                locCurve.Curve = Line.CreateBound(pt0, newPt1);
                            }
                        }

                        t.Commit();
                    }
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("错误", "修改管道时发生错误: " + ex.Message);
                return Result.Failed;
            }
        }
    }
}
