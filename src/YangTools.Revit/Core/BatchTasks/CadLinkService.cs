using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Core.BatchTasks
{
    public class CadLinkItem
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public bool SkipIfExists { get; set; } = true;
        public bool Pinned { get; set; } = true;
    }

    public class CadLinkService
    {
        /// <summary>
        /// 处理单个 CAD 链接任务。返回 null 成功，否则返回错误信息。
        /// </summary>
        public static string? Process(Document doc, CadLinkItem item)
        {
            if (!File.Exists(item.Path))
                return $"CAD 文件不存在: {item.Path}";

            // 检查是否已链接
            if (item.SkipIfExists)
            {
                var existingLinks = new FilteredElementCollector(doc)
                    .OfClass(typeof(CADLinkType))
                    .Cast<CADLinkType>()
                    .ToList();

                foreach (var link in existingLinks)
                {
                    try
                    {
                        var extRef = link.GetExternalFileReference();
                        var existingPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(extRef.GetPath());
                        if (existingPath != null && existingPath.Equals(item.Path, StringComparison.OrdinalIgnoreCase))
                            return null; // 已存在，跳过
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] CadLinkService.cs: {0}", ex.Message); }
                }
            }

            try
            {
                DWGImportOptions options = new DWGImportOptions
                {
                    ColorMode = ImportColorMode.Preserved,
                    CustomScale = 1.0,
                    OrientToView = true,
                    Placement = ImportPlacement.Origin,
                    Unit = ImportUnit.Default,
                    VisibleLayersOnly = false
                };

                using (Transaction t = new Transaction(doc, $"链接 CAD: {item.Name}"))
                {
                    t.Start();

#if REVIT2023_OR_GREATER
                    // Revit 2023+ 原生 Link API
                    bool success = doc.Link(item.Path, options, doc.ActiveView, out ElementId linkResultId);
                    if (success && linkResultId != ElementId.InvalidElementId && item.Pinned)
                    {
                        var instance = doc.GetElement(linkResultId) as ImportInstance;
                        if (instance != null) instance.Pinned = true;
                    }
#else
                    // Revit 2021-2022: 没有编程链接 CAD 的 API
                    throw new InvalidOperationException("CAD 文件链接功能需要 Revit 2023 及以上版本。请升级 Revit 或在 Revit 中手动链接 CAD 文件。");
#endif

                    t.Commit();
                }

                return null; // 成功
            }
            catch (Exception ex)
            {
                return $"CAD 链接失败 [{item.Name}]: {ex.Message}";
            }
        }
    }
}
