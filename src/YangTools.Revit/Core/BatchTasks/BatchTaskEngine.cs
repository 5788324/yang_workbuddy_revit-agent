using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                rootFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "YangTools_BatchExport");
            Directory.CreateDirectory(rootFolder);

            // 1. 链接管理 (仅对当前文档执行)
            if (viewModel.Links.Count > 0)
            {
                foreach (var link in viewModel.Links)
                {
                    progressCallback?.Invoke($"链接: {link.FileName}");
                    if (link.IsRevit)
                    {
                        var err = AccLinkService.Process(doc, new CloudLinkItem
                        { Name = link.FileName, Path = link.FilePath, SkipIfExists = true, Pinned = true });
                        if (err != null) errors.Add(err);
                    }
                    else if (link.IsCad)
                    {
                        var err = CadLinkService.Process(doc, new CadLinkItem
                        { Name = link.FileName, Path = link.FilePath, SkipIfExists = true, Pinned = true });
                        if (err != null) errors.Add(err);
                    }
                }
            }

            // 2. NWC (三维视图导出)
            if (viewModel.EnableNwc)
            {
                progressCallback?.Invoke("导出 NWC...");
                var view3D = FindView3D(doc, viewModel.SelectedNwc3DView);
                if (view3D != null)
                {
                    try
                    {
                        var options = new NavisworksExportOptions { ExportScope = NavisworksExportScope.View, ViewId = view3D.Id };
                        string outName = viewModel.NwcFileName ?? doc.Title;
                        doc.Export(rootFolder, outName, options);
                    }
                    catch (Exception ex) { errors.Add($"NWC 导出失败: {ex.Message}"); }
                }
                else { errors.Add($"NWC: 找不到三维视图 '{viewModel.SelectedNwc3DView}'"); }
            }

            // 3. IFC (三维视图导出)
            if (viewModel.EnableIfc)
            {
                progressCallback?.Invoke("导出 IFC...");
                var view3D = FindView3D(doc, viewModel.SelectedIfc3DView);
                if (view3D != null)
                {
                    try
                    {
                        IFCExportOptions opt = new IFCExportOptions();
                        opt.FileVersion = GetIFCVersion(viewModel.SelectedIfcVersion);
                        opt.FilterViewId = view3D.Id;
                        using (Transaction t = new Transaction(doc, "导出 IFC"))
                        {
                            t.Start();
                            doc.Export(rootFolder, viewModel.IfcFileName ?? doc.Title, opt);
                            t.Commit();
                        }
                    }
                    catch (Exception ex) { errors.Add($"IFC 导出失败: {ex.Message}"); }
                }
                else { errors.Add($"IFC: 找不到三维视图 '{viewModel.SelectedIfc3DView}'"); }
            }

            // 4. DWG
            if (viewModel.EnableDwg)
            {
                progressCallback?.Invoke("导出 DWG...");
                var viewIds = GetViewIds(doc, viewModel.SelectedDwgViewSheetSet);
                if (viewIds.Count > 0)
                {
                    try
                    {
                        DWGExportOptions opt;
                        if (!string.IsNullOrEmpty(viewModel.SelectedDwgSetup) && viewModel.SelectedDwgSetup != "<默认内置设置>")
                        {
                            var dwgSettings = new FilteredElementCollector(doc).OfClass(typeof(ExportDWGSettings))
                                .Cast<ExportDWGSettings>().FirstOrDefault(s => s.Name == viewModel.SelectedDwgSetup);
                            opt = dwgSettings?.GetDWGExportOptions() ?? new DWGExportOptions();
                        }
                        else opt = new DWGExportOptions();

                        // 单图纸 = 直接文件名；多图纸 = 导出设置决定命名
                        if (viewIds.Count == 1)
                            doc.Export(rootFolder, viewModel.DwgFileName ?? doc.Title, viewIds, opt);
                        else
                            doc.Export(rootFolder, viewModel.DwgFileName ?? doc.Title, viewIds, opt);
                    }
                    catch (Exception ex) { errors.Add($"DWG 导出失败: {ex.Message}"); }
                }
                else { errors.Add($"DWG: 视图集为空"); }
            }

            // 5. PDF
            if (viewModel.EnablePdf)
            {
                progressCallback?.Invoke("导出 PDF...");
                var viewIds = GetViewIds(doc, viewModel.SelectedPdfViewSheetSet);
                if (viewIds.Count > 0)
                {
                    try
                    {
#if REVIT2024_OR_GREATER
                        PDFExportOptions opt;
                        if (!string.IsNullOrEmpty(viewModel.SelectedPdfSetup) && viewModel.SelectedPdfSetup != "<默认内置设置>")
                        {
                            var pdfSettings = new FilteredElementCollector(doc).OfClass(typeof(ExportPDFSettings))
                                .Cast<ExportPDFSettings>().FirstOrDefault(s => s.Name == viewModel.SelectedPdfSetup);
                            opt = pdfSettings?.GetOptions() ?? new PDFExportOptions();
                        }
                        else opt = new PDFExportOptions();

                        opt.FileName = viewModel.PdfFileName ?? doc.Title;
                        opt.Combine = viewModel.PdfCombine;
                        doc.Export(rootFolder, viewIds, opt);
#else
                        errors.Add("PDF 导出仅支持 Revit 2024+");
#endif
                    }
                    catch (Exception ex) { errors.Add($"PDF 导出失败: {ex.Message}"); }
                }
                else { errors.Add($"PDF: 视图集为空"); }
            }

            return errors;
        }

        private static View3D? FindView3D(Document doc, string? viewName)
        {
            if (string.IsNullOrEmpty(viewName) || viewName == "<当前视图>")
            {
                if (doc.ActiveView is View3D av) return av;
                return new FilteredElementCollector(doc).OfClass(typeof(View3D)).Cast<View3D>()
                    .FirstOrDefault(v => !v.IsTemplate);
            }
            return new FilteredElementCollector(doc).OfClass(typeof(View3D)).Cast<View3D>()
                .FirstOrDefault(v => v.Name == viewName && !v.IsTemplate);
        }

        private static IFCVersion GetIFCVersion(string? versionName)
        {
            if (string.IsNullOrEmpty(versionName))
                return IFCVersion.IFC4;

            switch (versionName)
            {
                case "IFC 2x2": return IFCVersion.IFC2x2;
                case "IFC 2x3": return IFCVersion.IFC2x3;
                case "IFC 2x3 CV 2.0": return IFCVersion.IFC2x3CV2;
                case "IFC BCA": return IFCVersion.IFCBCA;
                case "IFC COBie": return IFCVersion.IFCCOBIE;
                case "IFC Rail": return IFCVersion.IFCRail;
                case "IFC 4": return IFCVersion.IFC4;
                case "IFC 4 DTV": return IFCVersion.IFC4DTV;
                case "IFC 4 RV": return IFCVersion.IFC4RV;
                default: return IFCVersion.IFC4;
            }
        }

        private static List<ElementId> GetViewIds(Document doc, string? viewSetName)
        {
            var ids = new List<ElementId>();
            if (string.IsNullOrEmpty(viewSetName) || viewSetName == "<当前视图>")
            {
                ids.Add(doc.ActiveView.Id);
                return ids;
            }
            var viewSet = new FilteredElementCollector(doc).OfClass(typeof(ViewSheetSet))
                .Cast<ViewSheetSet>().FirstOrDefault(s => s.Name == viewSetName);
            if (viewSet != null)
                foreach (Autodesk.Revit.DB.View view in viewSet.Views)
                    ids.Add(view.Id);
            else
                ids.Add(doc.ActiveView.Id);
            return ids;
        }
    }
}
