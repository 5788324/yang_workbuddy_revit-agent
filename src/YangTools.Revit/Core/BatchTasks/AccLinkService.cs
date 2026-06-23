using System;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Core.BatchTasks
{
    public class CloudLinkItem
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public bool SkipIfExists { get; set; } = true;
        public bool Pinned { get; set; } = true;
    }

    public class AccLinkService
    {
        public static void ProcessGui(Document doc, CloudLinkItem item)
        {
            try
            {
                if (!File.Exists(item.Path))
                    throw new FileNotFoundException($"Revit file not found: {item.Path}");

                if (item.SkipIfExists)
                {
                    var existingLinks = new FilteredElementCollector(doc)
                        .OfClass(typeof(RevitLinkType))
                        .Cast<RevitLinkType>()
                        .ToList();

                    foreach (var link in existingLinks)
                    {
                        if (link.IsExternalFileReference())
                        {
                            var extRef = link.GetExternalFileReference();
                            if (extRef.ExternalFileReferenceType == ExternalFileReferenceType.RevitLink)
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

                ModelPath modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(item.Path);
                RevitLinkOptions options = new RevitLinkOptions(false); 

                using (Transaction t = new Transaction(doc, $"Link Revit Model: {item.Name}"))
                {
                    t.Start();

                    LinkLoadResult loadResult = RevitLinkType.Create(doc, modelPath, options);
                    
                    if (loadResult.ElementId != ElementId.InvalidElementId)
                    {
                        RevitLinkInstance instance = RevitLinkInstance.Create(doc, loadResult.ElementId);
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
