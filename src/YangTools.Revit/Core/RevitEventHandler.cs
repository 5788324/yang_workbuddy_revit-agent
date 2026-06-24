using System;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Core
{
    /// <summary>
    /// A reusable IExternalEventHandler that allows modeless WPF windows
    /// to queue Revit API work (Transactions, etc.) to be executed
    /// on Revit's main API thread via ExternalEvent.Raise().
    /// </summary>
    public class RevitEventHandler : IExternalEventHandler
    {
        private System.Collections.Concurrent.ConcurrentQueue<(Action<UIApplication> action, string name, bool showSuccess)> _queue 
            = new System.Collections.Concurrent.ConcurrentQueue<(Action<UIApplication>, string, bool)>();

        /// <summary>
        /// Sets the action to be executed on the next Raise() call.
        /// </summary>
        /// <param name="showSuccess">是否在操作成功后弹出 TaskDialog 提示。数据加载等自动操作应传 false。</param>
        public void SetAction(Action<UIApplication> action, string operationName = "YangTools 操作", bool showSuccess = true)
        {
            _queue.Enqueue((action, operationName, showSuccess));
        }

        /// <summary>
        /// Called by Revit on the main API thread when ExternalEvent.Raise() is invoked.
        /// </summary>
        public void Execute(UIApplication app)
        {
            Document doc = app.ActiveUIDocument?.Document;

            while (_queue.TryDequeue(out var item))
            {
                var currentAction = item.action;
                var currentName = item.name;
                var showSuccess = item.showSuccess;

                if (doc != null && !doc.IsReadOnly)
                {
                    using (TransactionGroup tg = new TransactionGroup(doc, currentName))
                    {
                        try
                        {
                            tg.Start();
                            currentAction.Invoke(app);
                            TransactionHelper.ShowSuccessAndCommit(tg, "成功", $"{currentName} 执行成功。", app.ActiveUIDocument, showSuccess);
                        }
                        catch (OperationCanceledException)
                        {
                            if (tg.GetStatus() == TransactionStatus.Started) tg.RollBack();
                        }
                        catch (Exception ex)
                        {
                            if (tg.GetStatus() == TransactionStatus.Started) tg.RollBack();
                            TaskDialog.Show("错误", $"{currentName} 失败，已撤销。\n错误信息: {ex.Message}");
                        }
                    }
                }
                else
                {
                    try
                    {
                        currentAction.Invoke(app);
                    }
                    catch (Exception ex)
                    {
                        TaskDialog.Show("错误", $"{currentName} 失败。\n错误信息: {ex.Message}");
                    }
                }
            }
        }

        public string GetName() => "YangTools RevitEventHandler";
    }
}
