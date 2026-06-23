using System;
using System.Collections.Concurrent;
using Autodesk.Revit.UI;

namespace YangTools.Revit.Core
{
    /// <summary>
    /// A reusable IExternalEventHandler that allows modeless WPF windows
    /// to queue Revit API read-only work to be executed on Revit's main API thread.
    /// This does NOT start any Transactions.
    /// </summary>
    public class RevitReadEventHandler : IExternalEventHandler
    {
        private ConcurrentQueue<Action<UIApplication>> _queue = new ConcurrentQueue<Action<UIApplication>>();

        /// <summary>
        /// Sets the action to be executed on the next Raise() call.
        /// </summary>
        public void SetAction(Action<UIApplication> action)
        {
            _queue.Enqueue(action);
        }

        /// <summary>
        /// Called by Revit on the main API thread when ExternalEvent.Raise() is invoked.
        /// </summary>
        public void Execute(UIApplication app)
        {
            while (_queue.TryDequeue(out var action))
            {
                try
                {
                    action.Invoke(app);
                }
                catch (Exception ex)
                {
                    // For background read tasks, we typically don't pop up a TaskDialog 
                    // because it might disrupt the user. The caller should use try-catch inside the action to handle errors.
                    System.Diagnostics.Debug.WriteLine($"RevitReadEventHandler error: {ex.Message}");
                }
            }
        }

        public string GetName() => "YangTools RevitReadEventHandler";
    }
}
