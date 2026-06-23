using System;
using Autodesk.Revit.UI;

namespace YangTools.Revit.Core.BatchTasks
{
    public class BatchTaskHandler : IExternalEventHandler
    {
        private Action<UIApplication> _action;
        private Action _callback;

        public void SetAction(Action<UIApplication> action)
        {
            _action = action;
        }

        public void SetCallback(Action callback)
        {
            _callback = callback;
        }

        public void Execute(UIApplication app)
        {
            try
            {
                _action?.Invoke(app);
            }
            finally
            {
                _callback?.Invoke();
            }
        }

        public string GetName() => "BatchTaskHandler";
    }
}
