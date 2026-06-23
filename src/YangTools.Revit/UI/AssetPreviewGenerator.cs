using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Autodesk.Revit.DB;

namespace YangTools.Revit.UI
{
    public static class AssetPreviewGenerator
    {
        public static ImageSource CreateLinePatternPreview(LinePatternElement patternElem, System.Windows.Media.Color color, double width = 200, double height = 20)
        {
            var drawingGroup = new DrawingGroup();
            using (var dc = drawingGroup.Open())
            {
                var brush = new SolidColorBrush(color);
                var pen = new System.Windows.Media.Pen(brush, 1.5);
                
                if (patternElem == null || patternElem.Name == "Solid" || patternElem.Name == "实线" || patternElem.Name.Contains("Solid"))
                {
                    dc.DrawLine(pen, new System.Windows.Point(0, height / 2), new System.Windows.Point(width, height / 2));
                }
                else
                {
                    var pattern = patternElem.GetLinePattern();
                    var segments = pattern.GetSegments();
                    if (segments.Count == 0)
                    {
                        dc.DrawLine(pen, new System.Windows.Point(0, height / 2), new System.Windows.Point(width, height / 2));
                    }
                    else
                    {
                        var dashCollection = new DoubleCollection();
                        foreach (var seg in segments)
                        {
                            double len = seg.Length * 304.8 * 5; // convert feet to mm and scale up for display
                            if (seg.Type == LinePatternSegmentType.Dot)
                            {
                                dashCollection.Add(0.5);
                            }
                            else if (seg.Type == LinePatternSegmentType.Dash || seg.Type == LinePatternSegmentType.Space)
                            {
                                dashCollection.Add(Math.Max(1.0, len));
                            }
                        }
                        pen.DashStyle = new DashStyle(dashCollection, 0);
                        dc.DrawLine(pen, new System.Windows.Point(0, height / 2), new System.Windows.Point(width, height / 2));
                    }
                }
            }
            return new DrawingImage(drawingGroup);
        }

        public static ImageSource CreateFillPatternPreview(FillPatternElement patternElem, System.Windows.Media.Color color, double width = 80, double height = 30)
        {
            var drawingGroup = new DrawingGroup();
            using (var dc = drawingGroup.Open())
            {
                var brush = new SolidColorBrush(color);
                var pen = new System.Windows.Media.Pen(brush, 1);
                
                // Draw a boundary box to simulate the cell
                dc.DrawRectangle(System.Windows.Media.Brushes.Transparent, new System.Windows.Media.Pen(new SolidColorBrush(System.Windows.Media.Color.FromArgb(50, 0,0,0)), 1), new Rect(0, 0, width, height));

                if (patternElem == null)
                {
                    return new DrawingImage(drawingGroup); // Empty
                }

                var pattern = patternElem.GetFillPattern();
                if (pattern.IsSolidFill)
                {
                    dc.DrawRectangle(brush, null, new Rect(0, 0, width, height));
                    return new DrawingImage(drawingGroup);
                }

                // Simplified robust vector-based drawing for FillGrid
                var grids = pattern.GetFillGrids();
                dc.PushClip(new RectangleGeometry(new Rect(0, 0, width, height)));
                
                double centerX = width / 2;
                double centerY = height / 2;
                double maxR = Math.Sqrt(centerX * centerX + centerY * centerY) * 1.5; // buffer
                
                foreach (var grid in grids)
                {
                    double angle = grid.Angle;
                    double offset = grid.Offset * 304.8 * 10; // scale factor
                    if (offset < 2) offset = 2; // prevent extreme density
                    
                    double dirX = Math.Cos(angle);
                    double dirY = Math.Sin(angle);
                    double normX = -dirY; // Normal vector X
                    double normY = dirX;  // Normal vector Y
                    
                    int steps = (int)(maxR / offset) + 1;
                    
                    // Limit max lines per grid to prevent CPU hogging
                    if (steps > 50) steps = 50;
                    
                    for (int i = -steps; i <= steps; i++)
                    {
                        double px = centerX + i * offset * normX;
                        double py = centerY + i * offset * normY;
                        
                        double x1 = px - maxR * dirX;
                        double y1 = py - maxR * dirY;
                        double x2 = px + maxR * dirX;
                        double y2 = py + maxR * dirY;
                        
                        dc.DrawLine(pen, new System.Windows.Point(x1, y1), new System.Windows.Point(x2, y2));
                    }
                }
                dc.Pop();
            }
            return new DrawingImage(drawingGroup);
        }
    }
}
