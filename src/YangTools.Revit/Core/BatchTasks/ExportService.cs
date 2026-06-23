using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Core.BatchTasks
{
    public class NwcExportConfig
    {
        public string FileName { get; set; } = "";
        public string OutputFolder { get; set; } = "";
        public string ViewSetName { get; set; } = "";
    }

    public class IfcExportConfig
    {
        public string FileName { get; set; } = "";
        public string OutputFolder { get; set; } = "";
        public string IfcVersion { get; set; } = "";
        public string ViewSetName { get; set; } = "";
    }

    public class CadExportConfig
    {
        public string FileName { get; set; } = "";
        public string OutputFolder { get; set; } = "";
        public string ViewSetName { get; set; } = "";
        public string ExportSetupName { get; set; } = "";
    }

    public class PdfExportConfig
    {
        public string FileName { get; set; } = "";
        public string OutputFolder { get; set; } = "";
        public string ViewSetName { get; set; } = "";
        public string SetupName { get; set; } = "";
        public bool Combine { get; set; }
    }

    /// <summary>
    /// 导出结果
    /// </summary>
    public class ExportResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public List<string> ExportedFiles { get; set; } = new();
    }

    public class ExportService
    {
        /// <summary>
        /// 从视图集名称获取视图 ID 列表
        /// </summary>
        private static List<ElementId> GetViewIds(Document doc, string viewSetName)
        {
            var ids = new List<ElementId>();
            if (viewSetName == "<当前视图>" || string.IsNullOrEmpty(viewSetName))
            {
                ids.Add(doc.ActiveView.Id);
                return ids;
            }

            var viewSet = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheetSet))
                .Cast<ViewSheetSet>()
                .FirstOrDefault(s => s.Name == viewSetName);

            if (viewSet != null)
            {
                foreach (Autodesk.Revit.DB.View view in viewSet.Views)
                    ids.Add(view.Id);
            }
            else
            {
                ids.Add(doc.ActiveView.Id);
            }

            return ids;
        }

        /// <summary>
        /// 安全获取视图名称
        /// </summary>
        private static string GetViewNameSafe(Document doc, ElementId viewId)
        {
            var v = doc.GetElement(viewId);
            return v?.Name ?? $"视图_{viewId.ToString()}";
        }

        public static ExportResult ExportNwc(Document doc, NwcExportConfig config)
        {
            var result = new ExportResult();
            try
            {
                var viewsToExport = GetViewIds(doc, config.ViewSetName);
                Directory.CreateDirectory(config.OutputFolder);

                foreach (var viewId in viewsToExport)
                {
                    NavisworksExportOptions options = new NavisworksExportOptions
                    {
                        ExportScope = NavisworksExportScope.View,
                        ViewId = viewId
                    };
                    string name = viewsToExport.Count > 1
                        ? $"{config.FileName}_{GetViewNameSafe(doc, viewId)}"
                        : config.FileName;
                    string fullPath = Path.Combine(config.OutputFolder, name + ".nwc");
                    doc.Export(config.OutputFolder, name, options);
                    result.ExportedFiles.Add(fullPath);
                }

                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"NWC 导出失败: {ex.Message}";
            }
            return result;
        }

        public static ExportResult ExportIfc(Document doc, IfcExportConfig config)
        {
            var result = new ExportResult();
            try
            {
                var viewsToExport = GetViewIds(doc, config.ViewSetName);
                Directory.CreateDirectory(config.OutputFolder);

                foreach (var viewId in viewsToExport)
                {
                    IFCExportOptions options = new IFCExportOptions();
                    if (config.IfcVersion == "IFC 4")
                        options.FileVersion = IFCVersion.IFC4;
                    else
                        options.FileVersion = IFCVersion.IFC2x3;
                    options.FilterViewId = viewId;

                    string name = viewsToExport.Count > 1
                        ? $"{config.FileName}_{GetViewNameSafe(doc, viewId)}"
                        : config.FileName;
                    string fullPath = Path.Combine(config.OutputFolder, name + ".ifc");
                    doc.Export(config.OutputFolder, name, options);
                    result.ExportedFiles.Add(fullPath);
                }

                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"IFC 导出失败: {ex.Message}";
            }
            return result;
        }

        public static ExportResult ExportCad(Document doc, CadExportConfig config)
        {
            var result = new ExportResult();
            try
            {
                var viewsToExport = GetViewIds(doc, config.ViewSetName);
                Directory.CreateDirectory(config.OutputFolder);

                DWGExportOptions options;
                if (!string.IsNullOrEmpty(config.ExportSetupName))
                {
                    var dwgSettings = new FilteredElementCollector(doc)
                        .OfClass(typeof(ExportDWGSettings))
                        .Cast<ExportDWGSettings>()
                        .FirstOrDefault(s => s.Name == config.ExportSetupName);
                    options = dwgSettings?.GetDWGExportOptions() ?? new DWGExportOptions();
                }
                else
                {
                    options = new DWGExportOptions();
                }

                doc.Export(config.OutputFolder, config.FileName, viewsToExport, options);
                result.ExportedFiles.Add(Path.Combine(config.OutputFolder, config.FileName));
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"DWG 导出失败: {ex.Message}";
            }
            return result;
        }

        public static ExportResult ExportPdf(Document doc, PdfExportConfig config)
        {
            var result = new ExportResult();
#if REVIT2024_OR_GREATER
            try
            {
                var viewsToExport = GetViewIds(doc, config.ViewSetName);
                Directory.CreateDirectory(config.OutputFolder);

                PDFExportOptions options;
                if (!string.IsNullOrEmpty(config.SetupName))
                {
                    var pdfSettings = new FilteredElementCollector(doc)
                        .OfClass(typeof(ExportPDFSettings))
                        .Cast<ExportPDFSettings>()
                        .FirstOrDefault(s => s.Name == config.SetupName);
                    options = pdfSettings?.GetOptions() ?? new PDFExportOptions();
                }
                else
                {
                    options = new PDFExportOptions();
                }
                options.FileName = config.FileName;
                options.Combine = config.Combine;

                doc.Export(config.OutputFolder, viewsToExport, options);
                result.ExportedFiles.Add(Path.Combine(config.OutputFolder, config.FileName));
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"PDF 导出失败: {ex.Message}";
            }
#else
            result.Success = false;
            result.ErrorMessage = "PDF 导出仅支持 Revit 2024 及以上版本。";
#endif
            return result;
        }
    }
}
