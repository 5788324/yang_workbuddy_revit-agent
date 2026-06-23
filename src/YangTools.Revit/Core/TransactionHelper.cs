using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace YangTools.Revit.Core
{
    public static class TransactionHelper
    {
        /// <summary>
        /// 弹窗提示成功，并提供撤销功能。
        /// </summary>
        public static void ShowSuccessAndCommit(Transaction t, string title, string message, UIDocument uidoc = null)
        {
            if (t.GetStatus() != TransactionStatus.Started) return;

            if (uidoc != null)
            {
                uidoc.Document.Regenerate();
                uidoc.RefreshActiveView();
            }

            t.Commit();
        }

        /// <summary>
        /// 弹窗提示成功，并提供撤销功能 (针对 TransactionGroup)。
        /// </summary>
        public static void ShowSuccessAndCommit(TransactionGroup tg, string title, string message, UIDocument uidoc = null)
        {
            if (tg.GetStatus() != TransactionStatus.Started) return;

            if (uidoc != null)
            {
                try
                {
                    uidoc.Document.Regenerate();
                }
                catch (Autodesk.Revit.Exceptions.InvalidOperationException)
                {
                    // 如果没有处于打开状态的普通事务，则跳过 Regen
                }
                uidoc.RefreshActiveView();
            }

            try
            {
                tg.Assimilate();
            }
            catch (System.InvalidOperationException)
            {
                if (tg.GetStatus() == TransactionStatus.Started)
                {
                    tg.RollBack();
                }
            }
            catch (Autodesk.Revit.Exceptions.InvalidOperationException)
            {
                if (tg.GetStatus() == TransactionStatus.Started)
                {
                    tg.RollBack();
                }
            }
        }
    }
}
