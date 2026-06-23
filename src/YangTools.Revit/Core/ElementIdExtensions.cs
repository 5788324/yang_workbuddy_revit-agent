using Autodesk.Revit.DB;

namespace YangTools.Revit.Core
{
    public static class ElementIdExtensions
    {
        /// <summary>
        /// 获取 ElementId 的长整型值，自动适配不同年份的 Revit API
        /// </summary>
        public static long GetIdValue(this ElementId id)
        {
#if REVIT2024_OR_GREATER
            return id.Value;
#else
            return id.IntegerValue;
#endif
        }

        public static ElementId CreateId(long idValue)
        {
#if REVIT2024_OR_GREATER
            return new ElementId(idValue);
#else
            return new ElementId((int)idValue);
#endif
        }

        public static long GetIdValue(this WorksetId id)
        {
            return id.IntegerValue;
        }
    }
}
