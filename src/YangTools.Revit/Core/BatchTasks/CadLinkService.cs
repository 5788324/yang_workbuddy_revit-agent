using System;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Core.BatchTasks
{
    public class CadLinkItem
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public bool SkipIfExists { get; set; } = true;
        public bool Pinned { get; set; } = true;
    }

    public class CadLinkService
    {
        public static void Process(Document doc, CadLinkItem item)
        {
            try
            {
                if (!File.Exists(item.Path))
                    throw new FileNotFoundException($"CAD file not found: {item.Path}");

                if (item.SkipIfExists)
                {
                    var existingCadLinks = new FilteredElementCollector(doc)
                        .OfClass(typeof(CADLinkType))
                        .Cast<CADLinkType>()
                        .ToList();

                    foreach (var cadLink in existingCadLinks)
                    {
                        if (cadLink.IsExternalFileReference())
                        {
                            var extRef = cadLink.GetExternalFileReference();
                            if (extRef.ExternalFileReferenceType == ExternalFileReferenceType.CADLink)
                            {
                                var existingPath = extRef.GetPath() != null ? ModelPathUtils.ConvertModelPathToUserVisiblePath(extRef.GetPath()) : null;
                                if (existingPath != null && existingPath.Equals(item.Path, StringComparison.OrdinalIgnoreCase))
                                {
                                    return; // Already linked
                                }
                            }
                        }
                    }
                }

                DWGImportOptions options = new DWGImportOptions
                {
                    ColorMode = ImportColorMode.Preserved,
                    CustomScale = 1.0,
                    OrientToView = true,
                    Placement = ImportPlacement.Origin,
                    Unit = ImportUnit.Default,
                    VisibleLayersOnly = false
                };

                using (Transaction t = new Transaction(doc, $"Link CAD: {item.Name}"))
                {
                    t.Start();

                    ElementId linkElementId = ElementId.InvalidElementId;
                    bool success = doc.Link(item.Path, options, doc.ActiveView, out linkElementId);

                    if (success && linkElementId != ElementId.InvalidElementId)
                    {
                        var instance = doc.GetElement(linkElementId) as ImportInstance;
                        if (item.Pinned && instance != null)
                        {
                            instance.Pinned = true;
                        }
                    }

                    t.Commit();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }
    }
}
