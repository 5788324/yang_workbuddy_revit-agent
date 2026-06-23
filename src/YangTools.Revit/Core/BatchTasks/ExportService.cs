using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Core.BatchTasks
{
    public class NwcExportConfig
    {
        public string FileName { get; set; }
        public string OutputFolder { get; set; }
        public string ViewSetName { get; set; }
    }

    public class IfcExportConfig
    {
        public string FileName { get; set; }
        public string OutputFolder { get; set; }
        public string IfcVersion { get; set; }
        public string ViewSetName { get; set; }
    }

    public class CadExportConfig
    {
        public string FileName { get; set; }
        public string OutputFolder { get; set; }
        public string ViewSetName { get; set; }
        public string ExportSetupName { get; set; }
    }

    public class PdfExportConfig
    {
        public string FileName { get; set; }
        public string OutputFolder { get; set; }
        public string ViewSetName { get; set; }
        public string SetupName { get; set; }
        public bool Combine { get; set; }
    }

    public class ExportService
    {
        public static void ExportNwcGui(Document doc, NwcExportConfig config)
        {
            try
            {
                List<ElementId> viewsToExport = new List<ElementId>();

                if (config.ViewSetName == "<当前视图>" || string.IsNullOrEmpty(config.ViewSetName))
                {
                    viewsToExport.Add(doc.ActiveView.Id);
                }
                else
                {
                    var viewSet = new FilteredElementCollector(doc)
                        .OfClass(typeof(ViewSheetSet))
                        .Cast<ViewSheetSet>()
                        .FirstOrDefault(s => s.Name == config.ViewSetName);

                    if (viewSet != null)
                    {
                        foreach (Autodesk.Revit.DB.View view in viewSet.Views)
                        {
                            viewsToExport.Add(view.Id);
                        }
                    }
                    else
                    {
                        viewsToExport.Add(doc.ActiveView.Id);
                    }
                }

                foreach (var viewId in viewsToExport)
                {
                    NavisworksExportOptions options = new NavisworksExportOptions
                    {
                        ExportScope = NavisworksExportScope.View,
                        ViewId = viewId
                    };
                    string name = viewsToExport.Count > 1 ? $"{config.FileName}_{doc.GetElement(viewId).Name}" : config.FileName;
                    doc.Export(config.OutputFolder, name, options);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        public static void ExportIfcGui(Document doc, IfcExportConfig config)
        {
            try
            {
                List<ElementId> viewsToExport = new List<ElementId>();

                if (config.ViewSetName == "<当前视图>" || string.IsNullOrEmpty(config.ViewSetName))
                {
                    viewsToExport.Add(doc.ActiveView.Id);
                }
                else
                {
                    var viewSet = new FilteredElementCollector(doc)
                        .OfClass(typeof(ViewSheetSet))
                        .Cast<ViewSheetSet>()
                        .FirstOrDefault(s => s.Name == config.ViewSetName);

                    if (viewSet != null)
                    {
                        foreach (Autodesk.Revit.DB.View view in viewSet.Views)
                        {
                            viewsToExport.Add(view.Id);
                        }
                    }
                    else
                    {
                        viewsToExport.Add(doc.ActiveView.Id);
                    }
                }

                foreach (var viewId in viewsToExport)
                {
                    IFCExportOptions options = new IFCExportOptions();
                    
                    if (config.IfcVersion == "IFC 4")
                    {
                        options.FileVersion = IFCVersion.IFC4;
                    }
                    else
                    {
                        options.FileVersion = IFCVersion.IFC2x3;
                    }
                    options.FilterViewId = viewId;

                    string name = viewsToExport.Count > 1 ? $"{config.FileName}_{doc.GetElement(viewId).Name}" : config.FileName;
                    doc.Export(config.OutputFolder, name, options);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        public static void ExportCadGui(Document doc, CadExportConfig config)
        {
            try
            {
                var dwgSettings = new FilteredElementCollector(doc)
                    .OfClass(typeof(ExportDWGSettings))
                    .Cast<ExportDWGSettings>()
                    .FirstOrDefault(s => s.Name == config.ExportSetupName);

                DWGExportOptions options = dwgSettings != null ? dwgSettings.GetDWGExportOptions() : new DWGExportOptions();

                List<ElementId> viewsToExport = new List<ElementId>();

                if (config.ViewSetName == "<当前视图>" || string.IsNullOrEmpty(config.ViewSetName))
                {
                    viewsToExport.Add(doc.ActiveView.Id);
                }
                else
                {
                    var viewSet = new FilteredElementCollector(doc)
                        .OfClass(typeof(ViewSheetSet))
                        .Cast<ViewSheetSet>()
                        .FirstOrDefault(s => s.Name == config.ViewSetName);

                    if (viewSet != null)
                    {
                        foreach (Autodesk.Revit.DB.View view in viewSet.Views)
                        {
                            viewsToExport.Add(view.Id);
                        }
                    }
                    else
                    {
                        viewsToExport.Add(doc.ActiveView.Id); // Fallback
                    }
                }

                doc.Export(config.OutputFolder, config.FileName, viewsToExport, options);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        public static void ExportPdfGui(Document doc, PdfExportConfig config)
        {
#if REVIT2024_OR_GREATER
            try
            {
                List<ElementId> viewsToExport = new List<ElementId>();

                if (config.ViewSetName == "<当前视图>" || string.IsNullOrEmpty(config.ViewSetName))
                {
                    viewsToExport.Add(doc.ActiveView.Id);
                }
                else
                {
                    var viewSet = new FilteredElementCollector(doc)
                        .OfClass(typeof(ViewSheetSet))
                        .Cast<ViewSheetSet>()
                        .FirstOrDefault(s => s.Name == config.ViewSetName);

                    if (viewSet != null)
                    {
                        foreach (Autodesk.Revit.DB.View view in viewSet.Views)
                        {
                            viewsToExport.Add(view.Id);
                        }
                    }
                    else
                    {
                        viewsToExport.Add(doc.ActiveView.Id); // Fallback
                    }
                }

                var pdfSettings = new FilteredElementCollector(doc)
                    .OfClass(typeof(ExportPDFSettings))
                    .Cast<ExportPDFSettings>()
                    .FirstOrDefault(s => s.Name == config.SetupName);

                PDFExportOptions options = pdfSettings != null ? pdfSettings.GetOptions() : new PDFExportOptions();
                options.FileName = config.FileName;
                options.Combine = config.Combine;

                doc.Export(config.OutputFolder, viewsToExport, options);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
#else
            System.Diagnostics.Debug.WriteLine("PDF Export is only supported in Revit 2024 and above.");
#endif
        }
    }
}
