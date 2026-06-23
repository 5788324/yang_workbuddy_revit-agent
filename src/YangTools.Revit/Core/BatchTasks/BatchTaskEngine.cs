using System;
using System.IO;
using Autodesk.Revit.DB;
using YangTools.Revit.Models.BatchTask;

namespace YangTools.Revit.Core.BatchTasks
{
    public class BatchTaskEngine
    {
        public static void RunBatchGui(Document doc, BatchTaskViewModel viewModel, Action<string> progressCallback)
        {
            string rootFolder = viewModel.OutputFolder;
            if (string.IsNullOrWhiteSpace(rootFolder))
            {
                rootFolder = @"C:\YangTools\BatchExport";
            }
            Directory.CreateDirectory(rootFolder);

            // 1. Process Links
            if (viewModel.Links.Count > 0)
            {
                foreach (var link in viewModel.Links)
                {
                    progressCallback?.Invoke($"正在链接: {link.FileName}");
                    
                    if (link.IsRevit)
                    {
                        var config = new CloudLinkItem { Name = link.FileName, Path = link.FilePath, SkipIfExists = true, Pinned = true };
                        AccLinkService.ProcessGui(doc, config);
                    }
                    else if (link.IsCad)
                    {
                        var config = new CadLinkItem { Name = link.FileName, Path = link.FilePath, SkipIfExists = true, Pinned = true };
                        CadLinkService.Process(doc, config);
                    }
                }
            }

            // 2. Export NWC
            if (viewModel.EnableNwc)
            {
                progressCallback?.Invoke("正在导出 NWC...");
                var config = new NwcExportConfig { 
                    FileName = viewModel.NwcFileName, 
                    OutputFolder = rootFolder,
                    ViewSetName = viewModel.NwcViewSetName
                };
                ExportService.ExportNwcGui(doc, config);
            }

            // 3. Export PDF
            if (viewModel.EnablePdf)
            {
                progressCallback?.Invoke("正在导出 PDF...");
                var config = new PdfExportConfig 
                { 
                    FileName = viewModel.PdfFileName, 
                    OutputFolder = rootFolder,
                    ViewSetName = viewModel.SelectedPdfViewSheetSet,
                    SetupName = viewModel.SelectedPdfSetup,
                    Combine = viewModel.PdfCombine
                };
                ExportService.ExportPdfGui(doc, config);
            }

            // 4. Export IFC
            if (viewModel.EnableIfc)
            {
                progressCallback?.Invoke("正在导出 IFC...");
                var config = new IfcExportConfig 
                { 
                    FileName = viewModel.IfcFileName, 
                    OutputFolder = rootFolder,
                    IfcVersion = viewModel.SelectedIfcVersion,
                    ViewSetName = viewModel.IfcViewSetName
                };
                ExportService.ExportIfcGui(doc, config);
            }

            // 5. Export DWG
            if (viewModel.EnableDwg)
            {
                progressCallback?.Invoke("正在导出 CAD (DWG)...");
                var config = new CadExportConfig 
                { 
                    FileName = viewModel.DwgFileName, 
                    OutputFolder = rootFolder,
                    ViewSetName = viewModel.SelectedDwgViewSheetSet,
                    ExportSetupName = viewModel.SelectedDwgSetup
                };
                ExportService.ExportCadGui(doc, config);
            }
        }
    }
}
