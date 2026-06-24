using System;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using YangTools.Revit.Core;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton("MEP工具", "管井标高修改", "选择管井族和文字（例如 CL 5.70），自动修改族的标高偏移量", "Icons/manhole_elevation_32.png", "Icons/manhole_elevation_16.png")]
    public class ManholeElevationCommand : IExternalCommand
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
                    // 1. Select Family Instance
                    Reference fiRef = uidoc.Selection.PickObject(ObjectType.Element, new FamilyInstanceSelectionFilter(), "请选择一个管井族 (按 ESC 退出)");
                    FamilyInstance fi = doc.GetElement(fiRef) as FamilyInstance;
                    if (fi == null) continue;

                    // 2. Select TextNote
                    Reference textRef = uidoc.Selection.PickObject(ObjectType.Element, new TextSelectionFilter(), "请选择包含标高信息的文字 (例如: CL 5.70)");
                    TextElement textElement = doc.GetElement(textRef) as TextElement;
                    if (textElement == null) continue;

                    string textStr = textElement.Text;
                    if (string.IsNullOrWhiteSpace(textStr))
                    {
                        TaskDialog.Show("错误", "所选文字内容为空！");
                        continue;
                    }

                    // 3. Parse Text
                    // Format: "CL 5.70" or "GL +5.60"
                    var match = Regex.Match(textStr.Trim(), @"([A-Za-z]+)\s+([+-]?[\d\.]+)");
                    if (!match.Success)
                    {
                        TaskDialog.Show("错误", $"未能从文字中提取出需要的信息。\n文字内容: '{textStr}'\n期望格式: 类似 'CL 5.70'");
                        continue;
                    }

                    double elevationMeters = double.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                    double elevationFeet = elevationMeters * 3.280839895013123;

                    using (Transaction t = new Transaction(doc, "管井标高修改"))
                    {
                        t.Start();

                        // Try to find the elevation parameter
                        Parameter elevParam = fi.get_Parameter(BuiltInParameter.INSTANCE_ELEVATION_PARAM); // "Elevation from Level"
                        if (elevParam == null || elevParam.IsReadOnly)
                        {
                            elevParam = fi.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM); // "Offset" (for free hosted / face based)
                        }
                        
                        if (elevParam == null || elevParam.IsReadOnly)
                        {
                            // fallback try by name
                            elevParam = fi.LookupParameter("偏移") ?? fi.LookupParameter("Offset") ?? fi.LookupParameter("标高") ?? fi.LookupParameter("Elevation");
                        }

                        if (elevParam != null && !elevParam.IsReadOnly)
                        {
                            elevParam.Set(elevationFeet);
                        }
                        else
                        {
                            TaskDialog.Show("警告", "未能在所选族上找到可修改的标高或偏移参数！");
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
                TaskDialog.Show("错误", "修改管井标高时发生错误: " + ex.Message);
                return Result.Failed;
            }
        }
    }
}
