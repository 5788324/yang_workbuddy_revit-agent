using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Autodesk.Revit.DB;
using YangTools.Revit.Models.BatchTask;

namespace YangTools.Revit.Core.BatchTasks
{
    public class BatchTaskEngine
    {
        /// <summary>
        /// 运行批处理流程。返回错误列表（空 = 全部成功）。
        /// </summary>
        public static List<string> RunBatch(Document doc, BatchTaskViewModel viewModel, Action<string> progressCallback)
        {
            var errors = new List<string>();
            string rootFolder = viewModel.OutputFolder;
            if (string.IsNullOrWhiteSpace(rootFolder))
                rootFolder = @"C:\YangTools\BatchExport";
            Directory.CreateDirectory(rootFolder);

            // 1. AccuLink (Revit 云端链接)
            if (viewModel.Links.Count > 0)
            {
                foreach (var link in viewModel.Links)
                {
                    progressCallback?.Invoke($"正在链接: {link.FileName}");

                    if (link.IsRevit)
                    {
                        var cfg = new CloudLinkItem
                        {
                            Name = link.FileName, Path = link.FilePath,
                            SkipIfExists = true, Pinned = true
                        };
                        string? err = AccLinkService.Process(doc, cfg);
                        if (err != null) errors.Add(err);
                    }
                    else if (link.IsCad)
                    {
                        var cfg = new CadLinkItem
                        {
                            Name = link.FileName, Path = link.FilePath,
                            SkipIfExists = true, Pinned = true
                        };
                        string? err = CadLinkService.Process(doc, cfg);
                        if (err != null) errors.Add(err);
                    }
                }
            }

            // 2. NWC 导出
            if (viewModel.EnableNwc)
            {
                progressCallback?.Invoke("正在导出 NWC...");
                var result = ExportService.ExportNwc(doc, new NwcExportConfig
                {
                    FileName = viewModel.NwcFileName,
                    OutputFolder = rootFolder,
                    ViewSetName = viewModel.NwcViewSetName
                });
                if (!result.Success) errors.Add(result.ErrorMessage ?? "NWC 导出失败");
            }

            // 3. PDF 导出
            if (viewModel.EnablePdf)
            {
                progressCallback?.Invoke("正在导出 PDF...");
                var result = ExportService.ExportPdf(doc, new PdfExportConfig
                {
                    FileName = viewModel.PdfFileName,
                    OutputFolder = rootFolder,
                    ViewSetName = viewModel.SelectedPdfViewSheetSet,
                    SetupName = viewModel.SelectedPdfSetup,
                    Combine = viewModel.PdfCombine
                });
                if (!result.Success) errors.Add(result.ErrorMessage ?? "PDF 导出失败");
            }

            // 4. IFC 导出
            if (viewModel.EnableIfc)
            {
                progressCallback?.Invoke("正在导出 IFC...");
                var result = ExportService.ExportIfc(doc, new IfcExportConfig
                {
                    FileName = viewModel.IfcFileName,
                    OutputFolder = rootFolder,
                    IfcVersion = viewModel.SelectedIfcVersion,
                    ViewSetName = viewModel.IfcViewSetName
                });
                if (!result.Success) errors.Add(result.ErrorMessage ?? "IFC 导出失败");
            }

            // 5. DWG 导出
            if (viewModel.EnableDwg)
            {
                progressCallback?.Invoke("正在导出 CAD (DWG)...");
                var result = ExportService.ExportCad(doc, new CadExportConfig
                {
                    FileName = viewModel.DwgFileName,
                    OutputFolder = rootFolder,
                    ViewSetName = viewModel.SelectedDwgViewSheetSet,
                    ExportSetupName = viewModel.SelectedDwgSetup
                });
                if (!result.Success) errors.Add(result.ErrorMessage ?? "DWG 导出失败");
            }

            return errors;
        }
    }
}
