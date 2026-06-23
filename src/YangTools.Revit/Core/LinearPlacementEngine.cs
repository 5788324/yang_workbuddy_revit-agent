using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using YangTools.Revit.UI;

namespace YangTools.Revit.Core
{
    public class LinearPlacementEngine
    {
        private readonly UIDocument _uidoc;
        private readonly Document _doc;

        public LinearPlacementEngine(UIDocument uidoc)
        {
            _uidoc = uidoc;
            _doc = uidoc.Document;
        }

        public List<Curve>? PickAndExtractCurves()
        {
            IList<Element> selectedElems = _uidoc.Selection.PickElementsByRectangle(new GenericCurveFilter(), "框选路线 (支持模型线、体量、CAD链接等)");
            if (selectedElems == null || selectedElems.Count == 0) return null;

            List<Curve> allCurves = new List<Curve>();
            var geomOptions = new Options { ComputeReferences = true, DetailLevel = ViewDetailLevel.Fine };
            foreach (var elem in selectedElems)
            {
                var geom = elem.get_Geometry(geomOptions);
                if (geom != null)
                {
                    ExtractCurves(geom, Transform.Identity, allCurves);
                }
            }

            if (allCurves.Count == 0) return null;

            return SortAndChainCurves(allCurves);
        }

        public void ExecutePlacement(LinearPlacementViewModel viewModel)
        {
            if (viewModel.SelectedSymbol == null || viewModel.ChainedCurves == null || viewModel.ChainedCurves.Count == 0) return;

            // 4. Calculate points and tangents
            List<PlacementData> placements = CalculatePlacements(viewModel.ChainedCurves, viewModel);
            if (placements.Count == 0)
            {
                TaskDialog.Show("提示", "计算得出的布置点数量为 0。请检查起点/终点/间距设置是否合理。");
                return;
            }

            // 5. Place instances
            using (Transaction t = new Transaction(_doc, "线性布置族实例"))
            {
                t.Start();

                // Ensure family symbol is active
                if (!viewModel.SelectedSymbol.IsActive)
                {
                    viewModel.SelectedSymbol.Activate();
                    _doc.Regenerate();
                }

                // If family is hosted, try to find current level
                Level? level = null;
                if (_uidoc.ActiveView.GenLevel != null)
                {
                    level = _uidoc.ActiveView.GenLevel;
                }

                foreach (var data in placements)
                {
                    // Convert coordinates from mm to feet if user input was mm? 
                    // Wait, Revit internal units are feet. We should assume the points are in Revit internal feet.
                    // Oh! If the user input start/end/spacing in distance, they likely think in millimeters (if metric).
                    // We must convert distance inputs from mm to internal units (feet).
                    
                    FamilyInstance instance;
                    if (level != null)
                    {
                        // Place on level
                        instance = _doc.Create.NewFamilyInstance(data.Point, viewModel.SelectedSymbol, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                        
                        // Try to align Z elevation if placing on level doesn't match curve height exactly
                        var zOffsetParam = instance.get_Parameter(BuiltInParameter.INSTANCE_ELEVATION_PARAM) 
                                           ?? instance.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM);
                        if (zOffsetParam != null && !zOffsetParam.IsReadOnly)
                        {
                            zOffsetParam.Set(data.Point.Z - level.Elevation);
                        }
                    }
                    else
                    {
                        // Place by point in 3D view
                        instance = _doc.Create.NewFamilyInstance(data.Point, viewModel.SelectedSymbol, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                    }

                    if (viewModel.AlignToCurve)
                    {
                        var tangent = data.Tangent;
                        var zAxis = XYZ.BasisZ;

                        double offsetRad = viewModel.RotationOffset * Math.PI / 180.0;

                        if (viewModel.KeepVertical)
                        {
                            tangent = new XYZ(tangent.X, tangent.Y, 0).Normalize();
                            if (tangent.IsZeroLength()) tangent = XYZ.BasisX;

                            double angle = XYZ.BasisX.AngleOnPlaneTo(tangent, XYZ.BasisZ);
                            angle += offsetRad;

                            Line axis = Line.CreateBound(data.Point, data.Point + zAxis);
                            ElementTransformUtils.RotateElement(_doc, instance.Id, axis, angle);
                        }
                        else
                        {
                            // 3D rotation
                            XYZ flatTangent = new XYZ(tangent.X, tangent.Y, 0).Normalize();
                            if (flatTangent.IsZeroLength()) flatTangent = XYZ.BasisX;

                            double angleZ = XYZ.BasisX.AngleOnPlaneTo(flatTangent, XYZ.BasisZ);
                            angleZ += offsetRad;
                            Line axisZ = Line.CreateBound(data.Point, data.Point + XYZ.BasisZ);
                            ElementTransformUtils.RotateElement(_doc, instance.Id, axisZ, angleZ);

                            double pitchAngle = tangent.AngleTo(flatTangent);
                            if (tangent.Z < 0) pitchAngle = -pitchAngle;

                            if (Math.Abs(pitchAngle) > 0.001)
                            {
                                XYZ pitchAxisVec = XYZ.BasisZ.CrossProduct(flatTangent).Normalize();
                                Line pitchAxis = Line.CreateBound(data.Point, data.Point + pitchAxisVec);
                                try
                                {
                                    ElementTransformUtils.RotateElement(_doc, instance.Id, pitchAxis, pitchAngle);
                                }
                                catch (Exception) { }
                            }
                        }
                    }
                }

                t.Commit();
            }
        }

        private void ExtractCurves(GeometryObject geomObj, Transform transform, List<Curve> curves)
        {
            if (geomObj is Curve curve)
            {
                if (curve.Length > 0.001)
                {
                    curves.Add(curve.CreateTransformed(transform));
                }
            }
            else if (geomObj is GeometryElement geomElem)
            {
                foreach (var gObj in geomElem)
                {
                    ExtractCurves(gObj, transform, curves);
                }
            }
            else if (geomObj is GeometryInstance geomInst)
            {
                var instTransform = transform.Multiply(geomInst.Transform);
                var instGeom = geomInst.GetInstanceGeometry();
                if (instGeom != null)
                {
                    ExtractCurves(instGeom, instTransform, curves);
                }
            }
        }

        private List<Curve> SortAndChainCurves(List<Curve> inputCurves)
        {
            if (inputCurves.Count <= 1) return inputCurves;

            var result = new List<Curve>();
            var remaining = new List<Curve>(inputCurves);

            // Start with the longest curve as seed
            var seed = remaining.OrderByDescending(c => c.Length).First();
            result.Add(seed);
            remaining.Remove(seed);

            double tolerance = 0.005; // ~1.5mm

            bool added = true;
            while (added && remaining.Count > 0)
            {
                added = false;
                XYZ currentStart = result.First().GetEndPoint(0);
                XYZ currentEnd = result.Last().GetEndPoint(1);

                for (int i = 0; i < remaining.Count; i++)
                {
                    var c = remaining[i];
                    XYZ p0 = c.GetEndPoint(0);
                    XYZ p1 = c.GetEndPoint(1);

                    // Try attach to end
                    if (p0.DistanceTo(currentEnd) < tolerance)
                    {
                        result.Add(c);
                        remaining.RemoveAt(i);
                        added = true;
                        break;
                    }
                    if (p1.DistanceTo(currentEnd) < tolerance)
                    {
                        result.Add(c.CreateReversed());
                        remaining.RemoveAt(i);
                        added = true;
                        break;
                    }

                    // Try attach to start
                    if (p1.DistanceTo(currentStart) < tolerance)
                    {
                        result.Insert(0, c);
                        remaining.RemoveAt(i);
                        added = true;
                        break;
                    }
                    if (p0.DistanceTo(currentStart) < tolerance)
                    {
                        result.Insert(0, c.CreateReversed());
                        remaining.RemoveAt(i);
                        added = true;
                        break;
                    }
                }
            }

            return result;
        }

        private List<PlacementData> CalculatePlacements(List<Curve> chainedCurves, LinearPlacementViewModel vm)
        {
            var dataList = new List<PlacementData>();
            double totalLength = chainedCurves.Sum(c => c.Length);

            double[] curveLengths = chainedCurves.Select(c => c.Length).ToArray();
            double[] cumulativeLengths = new double[curveLengths.Length + 1];
            cumulativeLengths[0] = 0;
            for (int i = 0; i < curveLengths.Length; i++)
                cumulativeLengths[i + 1] = cumulativeLengths[i] + curveLengths[i];

            // Setup iteration parameters based on user input
            double start = vm.StartValue;
            double end = vm.EndValue;
            double step = vm.StepValue;

            // Unit conversion for Distance Mode: Assume user input is in mm, convert to Revit internal (feet)
            // If they input in parameter (0-1), no unit conversion needed.
            double conversionFactor = vm.IsByDistance ? (1.0 / 304.8) : 1.0; 
            
            double trueStart = start * conversionFactor;
            double trueEnd = end * conversionFactor;
            double trueStep = step;

            if (vm.IsByDistance)
            {
                if (vm.IsBySpacing)
                {
                    trueStep = step * conversionFactor;
                }
                else
                {
                    // By Count
                    if (step > 1)
                        trueStep = (trueEnd - trueStart) / (step - 1);
                    else
                        trueStep = trueEnd - trueStart;
                }
            }
            else
            {
                // By Parameter
                trueStart = start * totalLength;
                trueEnd = end * totalLength;
                if (vm.IsBySpacing)
                {
                    trueStep = step * totalLength;
                }
                else
                {
                    // By Count
                    if (step > 1)
                        trueStep = (trueEnd - trueStart) / (step - 1);
                    else
                        trueStep = trueEnd - trueStart;
                }
            }

            if (trueStep <= 0.001) return dataList; // Prevent infinite loop

            // Ensure start < end 
            if (trueStart > trueEnd)
            {
                double temp = trueStart;
                trueStart = trueEnd;
                trueEnd = temp;
            }

            // Cap at total length
            if (trueEnd > totalLength) trueEnd = totalLength;
            if (trueStart < 0) trueStart = 0;

            for (double d = trueStart; d <= trueEnd + 0.0001; d += trueStep)
            {
                double cappedD = Math.Min(d, totalLength);
                
                // Find which curve this distance falls on
                int curveIdx = 0;
                for (int i = 0; i < cumulativeLengths.Length - 1; i++)
                {
                    if (cappedD >= cumulativeLengths[i] && cappedD <= cumulativeLengths[i + 1] + 0.0001)
                    {
                        curveIdx = i;
                        break;
                    }
                }

                Curve targetCurve = chainedCurves[curveIdx];
                double localLength = cappedD - cumulativeLengths[curveIdx];
                
                double localParam = GetNormalizedParameterAtArcLength(targetCurve, localLength);
                
                XYZ pt = targetCurve.Evaluate(localParam, true);
                Transform derivatives = targetCurve.ComputeDerivatives(localParam, true);
                XYZ tangent = derivatives.BasisX.Normalize();

                dataList.Add(new PlacementData { Point = pt, Tangent = tangent });
            }

            return dataList;
        }

        private double GetNormalizedParameterAtArcLength(Curve curve, double targetLength)
        {
            if (targetLength <= 0) return 0.0;
            if (targetLength >= curve.Length - 1e-5) return 1.0;

            if (curve is Line || curve is Arc)
            {
                return targetLength / curve.Length;
            }

            int steps = 200;
            double[] dists = new double[steps + 1];
            dists[0] = 0;
            XYZ prevPt = curve.Evaluate(0.0, true);
            for (int i = 1; i <= steps; i++)
            {
                XYZ pt = curve.Evaluate(i / (double)steps, true);
                dists[i] = dists[i - 1] + prevPt.DistanceTo(pt);
                prevPt = pt;
            }

            double totalNumLen = dists[steps];
            double targetNumLen = targetLength / curve.Length * totalNumLen;

            for (int i = 0; i < steps; i++)
            {
                if (targetNumLen >= dists[i] && targetNumLen <= dists[i + 1])
                {
                    double segmentLen = dists[i + 1] - dists[i];
                    double fraction = (segmentLen < 1e-9) ? 0 : (targetNumLen - dists[i]) / segmentLen;
                    return (i + fraction) / steps;
                }
            }

            return 1.0;
        }
    }

    public class PlacementData
    {
        public XYZ Point { get; set; } = XYZ.Zero;
        public XYZ Tangent { get; set; } = XYZ.BasisX;
    }

    public class GenericCurveFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            if (elem.Category == null) return false;
            // Filter to only elements likely to contain curves (lines, links, masses, families)
            return elem is CurveElement || elem is ImportInstance || elem is FamilyInstance || elem is DirectShape || elem.Category.Id.GetIdValue() == (long)BuiltInCategory.OST_Mass;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return true;
        }
    }
}
