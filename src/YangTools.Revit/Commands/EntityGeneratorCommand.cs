using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using YangTools.Revit.Core;
using YangTools.Revit.Models;
using YangTools.Revit.UI;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton("模型修改区", "实体生成(Loft)", "基于轮廓生成放样/拉伸等三维实体", "Icons/entity_generator_32.png", "Icons/entity_generator_16.png")]
    public class EntityGeneratorCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiApp = commandData.Application;
                var uiDoc = uiApp.ActiveUIDocument;
                if (uiDoc == null)
                {
                    TaskDialog.Show("提示", "请先打开一个项目文档。");
                    return Result.Cancelled;
                }
                var doc = uiDoc.Document;

                var state = new EntityGeneratorState();

                while (true)
                {
                    var window = new EntityGeneratorWindow(uiApp, state);
                    var helper = new System.Windows.Interop.WindowInteropHelper(window)
                    {
                        Owner = uiApp.MainWindowHandle
                    };
                    window.ShowDialog();

                    state = window.State;

                    if (window.ActionToPerform == "PickProfiles")
                    {
                        try
                        {
                            var refs = uiDoc.Selection.PickObjects(ObjectType.Element, "请选择轮廓实例（可多选）");
                            state.Profiles = refs
                                .Select(r => doc.GetElement(r) as FamilyInstance)
                                .Where(fi => fi != null)
                                .ToList();
                        }
                        catch (OperationCanceledException) { }
                    }
                    else if (window.IsExecute)
                    {
                        if (state.Profiles.Count < 2)
                        {
                            TaskDialog.Show("提示", "请至少选择两个轮廓实例进行放样。");
                            continue;
                        }

                        try
                        {
                            EntityGeneratorEngine.Execute(uiApp, doc, state);
                            TaskDialog.Show("完成", "实体生成成功！可继续修改参数生成下一个族，或关闭窗口。");
                        }
                        catch (Exception ex)
                        {
                            TaskDialog.Show("错误", $"生成失败：{ex.Message}");
                        }
                    }
                    else
                    {
                        return Result.Cancelled;
                    }
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("错误", $"操作失败。\n错误信息: {ex.Message}");
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
