using Autodesk.Revit.DB;

namespace YangTools.Revit.Core
{
    public static class TextNoteHelper
    {
        public static double GetTextNoteAngle(TextNote textNote)
        {
            XYZ baseDirection = ((TextElement)textNote).BaseDirection;
            double angle = XYZ.BasisX.AngleTo(new XYZ(baseDirection.X, baseDirection.Y, 0.0).Normalize());
            if (baseDirection.Y < 0.0)
            {
                angle = -angle;
            }
            return angle;
        }
    }
}
