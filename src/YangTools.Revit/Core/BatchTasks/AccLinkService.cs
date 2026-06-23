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
        /// <summary>
        /// 处理单个 Revit 链接。返回 null = 成功，否则返回错误信息。
        /// </summary>
        public static string? Process(Document doc, CloudLinkItem item)
        {
            try
            {
                if (!File.Exists(item.Path))
                    return $"Revit 文件不存在: {item.Path}";

                if (item.SkipIfExists)
                {
                    var existingLinks = new FilteredElementCollector(doc)
                        .OfClass(typeof(RevitLinkType))
                        .Cast<RevitLinkType>()
                        .ToList();

                    foreach (var link in existingLinks)
                    {
                        try
                        {
                            var extRef = link.GetExternalFileReference();
                            var existingPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(extRef.GetPath());
                            if (existingPath != null && existingPath.Equals(item.Path, StringComparison.OrdinalIgnoreCase))
                                return null; // 已存在
                        }
                        catch { }
                    }
                }

                ModelPath modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(item.Path);
                RevitLinkOptions options = new RevitLinkOptions(false);

                using (Transaction t = new Transaction(doc, $"链接 Revit: {item.Name}"))
                {
                    t.Start();
                    LinkLoadResult loadResult = RevitLinkType.Create(doc, modelPath, options);
                    if (loadResult.ElementId != ElementId.InvalidElementId)
                    {
                        RevitLinkInstance instance = RevitLinkInstance.Create(doc, loadResult.ElementId);
                        if (item.Pinned && instance != null)
                            instance.Pinned = true;
                    }
                    t.Commit();
                }

                return null; // 成功
            }
            catch (Exception ex)
            {
                return $"Revit 链接失败 [{item.Name}]: {ex.Message}";
            }
        }

        public static void ProcessGui(Document doc, CloudLinkItem item)
        {
            var err = Process(doc, item);
            if (err != null)
                System.Diagnostics.Debug.WriteLine(err);
        }
    }
}
