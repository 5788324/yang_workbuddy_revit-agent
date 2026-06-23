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
        private System.Collections.Concurrent.ConcurrentQueue<(Action<UIApplication> action, string name)> _queue 
            = new System.Collections.Concurrent.ConcurrentQueue<(Action<UIApplication>, string)>();

        /// <summary>
        /// Sets the action to be executed on the next Raise() call.
        /// </summary>
        public void SetAction(Action<UIApplication> action, string operationName = "YangTools 操作")
        {
            _queue.Enqueue((action, operationName));
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

                if (doc != null && !doc.IsReadOnly)
                {
                    using (TransactionGroup tg = new TransactionGroup(doc, currentName))
                    {
                        try
                        {
                            tg.Start();
                            currentAction.Invoke(app);
                            TransactionHelper.ShowSuccessAndCommit(tg, "成功", $"{currentName} 执行成功。", app.ActiveUIDocument);
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
