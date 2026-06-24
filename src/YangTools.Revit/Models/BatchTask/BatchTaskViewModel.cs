using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace YangTools.Revit.Models.BatchTask
{
    public class LinkItem : INotifyPropertyChanged
    {
        public string FilePath { get; set; } = "";
        public string FileName => Path.GetFileName(FilePath);
        public bool IsCad => FilePath?.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase) == true 
                          || FilePath?.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase) == true;
        public bool IsRevit => FilePath?.EndsWith(".rvt", StringComparison.OrdinalIgnoreCase) == true;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// 批处理目标文档信息
    /// </summary>
    public class BatchDocumentInfo : INotifyPropertyChanged
    {
        public string FilePath { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public bool IsCurrentDocument { get; set; }
        public bool Enabled { get; set; } = true;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class BatchTaskViewModel : INotifyPropertyChanged
    {
        private Document _doc;
        private UIApplication? _uiApp;

        public BatchTaskViewModel(UIApplication uiApp)
        {
            _uiApp = uiApp;
            _doc = uiApp.ActiveUIDocument.Document;
            LoadDocumentSettings();

            OutputFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "YangTools_BatchExport");

            // 默认包含当前文档
            Documents.Add(new BatchDocumentInfo
            {
                FilePath = _doc.PathName,
                DisplayName = $"📄 当前: {_doc.Title}",
                IsCurrentDocument = true,
                Enabled = true
            });

            NwcFileName = $"{_doc.Title}";
            IfcFileName = $"{_doc.Title}";
            DwgFileName = $"{_doc.Title}";
            PdfFileName = $"{_doc.Title}";
        }

        private void LoadDocumentSettings()
        {
            // 3D视图列表 (给 NWC/IFC 用)
            var threeDViews = new FilteredElementCollector(_doc)
                .OfClass(typeof(View3D))
                .Cast<View3D>()
                .Where(v => !v.IsTemplate)
                .Select(v => v.Name)
                .OrderBy(n => n)
                .ToList();
            Available3DViews = threeDViews.Any() ? threeDViews : new List<string> { "<当前视图>" };
            SelectedNwc3DView = Available3DViews.FirstOrDefault();
            SelectedIfc3DView = Available3DViews.FirstOrDefault();

            // 图纸/视图集 (给 CAD/PDF 用)
            var sets = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSheetSet))
                .Cast<ViewSheetSet>()
                .Select(s => s.Name)
                .OrderBy(n => n)
                .ToList();
            AvailableViewSheetSets = sets.Any() ? sets : new List<string> { "<当前视图>" };
            SelectedPdfViewSheetSet = AvailableViewSheetSets.FirstOrDefault();
            SelectedDwgViewSheetSet = AvailableViewSheetSets.FirstOrDefault();

            // DWG 导出设置
            var dwgSetups = new FilteredElementCollector(_doc)
                .OfClass(typeof(ExportDWGSettings))
                .Cast<ExportDWGSettings>()
                .Select(s => s.Name)
                .OrderBy(n => n)
                .ToList();
            AvailableDwgSetups = dwgSetups.Any() ? dwgSetups : new List<string> { "<默认内置设置>" };
            SelectedDwgSetup = AvailableDwgSetups.FirstOrDefault();

            // PDF 设置 (2024+)
#if REVIT2024_OR_GREATER
            var pdfSetups = new FilteredElementCollector(_doc)
                .OfClass(typeof(ExportPDFSettings))
                .Cast<ExportPDFSettings>()
                .Select(s => s.Name)
                .OrderBy(n => n)
                .ToList();
            AvailablePdfSetups = pdfSetups.Any() ? pdfSetups : new List<string> { "<默认内置设置>" };
            SelectedPdfSetup = AvailablePdfSetups.FirstOrDefault();
#else
            AvailablePdfSetups = new List<string> { "Revit版本过低不支持PDF" };
            SelectedPdfSetup = AvailablePdfSetups.FirstOrDefault();
#endif
        }

        // ===== 多文档支持 =====
        public ObservableCollection<BatchDocumentInfo> Documents { get; set; } = new();
        public ObservableCollection<LinkItem> Links { get; set; } = new();

        public void AddDocument(string filePath)
        {
            if (Documents.Any(d => d.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
                return;
            Documents.Add(new BatchDocumentInfo
            {
                FilePath = filePath,
                DisplayName = $"📁 {Path.GetFileNameWithoutExtension(filePath)}",
                IsCurrentDocument = false,
                Enabled = true
            });
        }

        // ===== 基础 =====
        private string _outputFolder = "";
        public string OutputFolder { get => _outputFolder; set { _outputFolder = value; OnPropertyChanged(); } }

        // ===== NWC =====
        private bool _enableNwc;
        public bool EnableNwc { get => _enableNwc; set { _enableNwc = value; OnPropertyChanged(); } }

        private string _nwcFileName = "";
        public string NwcFileName { get => _nwcFileName; set { _nwcFileName = value; OnPropertyChanged(); } }

        public List<string> Available3DViews { get; private set; } = new();
        private string? _selectedNwc3DView;
        public string? SelectedNwc3DView { get => _selectedNwc3DView; set { _selectedNwc3DView = value; OnPropertyChanged(); } }

        // ===== IFC =====
        private bool _enableIfc;
        public bool EnableIfc { get => _enableIfc; set { _enableIfc = value; OnPropertyChanged(); } }

        private string _ifcFileName = "";
        public string IfcFileName { get => _ifcFileName; set { _ifcFileName = value; OnPropertyChanged(); } }

        private string? _selectedIfc3DView;
        public string? SelectedIfc3DView { get => _selectedIfc3DView; set { _selectedIfc3DView = value; OnPropertyChanged(); } }

        public List<string> AvailableIfcVersions { get; } = new() { "IFC 4", "IFC 4 DTV", "IFC 4 RV", "IFC 2x3", "IFC 2x3 CV 2.0", "IFC 2x2", "IFC BCA", "IFC COBie", "IFC Rail" };
        private string _selectedIfcVersion = "IFC 4";
        public string SelectedIfcVersion { get => _selectedIfcVersion; set { _selectedIfcVersion = value; OnPropertyChanged(); } }

        // ===== DWG =====
        private bool _enableDwg;
        public bool EnableDwg { get => _enableDwg; set { _enableDwg = value; OnPropertyChanged(); } }

        private string _dwgFileName = "";
        public string DwgFileName { get => _dwgFileName; set { _dwgFileName = value; OnPropertyChanged(); } }

        public List<string> AvailableViewSheetSets { get; private set; } = new();
        private string? _selectedDwgViewSheetSet;
        public string? SelectedDwgViewSheetSet { get => _selectedDwgViewSheetSet; set { _selectedDwgViewSheetSet = value; OnPropertyChanged(); } }

        public List<string> AvailableDwgSetups { get; private set; } = new();
        private string? _selectedDwgSetup;
        public string? SelectedDwgSetup { get => _selectedDwgSetup; set { _selectedDwgSetup = value; OnPropertyChanged(); } }

        // ===== PDF =====
        private bool _enablePdf;
        public bool EnablePdf { get => _enablePdf; set { _enablePdf = value; OnPropertyChanged(); } }

        private string _pdfFileName = "";
        public string PdfFileName { get => _pdfFileName; set { _pdfFileName = value; OnPropertyChanged(); } }

        private bool _pdfCombine = true;
        public bool PdfCombine { get => _pdfCombine; set { _pdfCombine = value; OnPropertyChanged(); } }

        private string? _selectedPdfViewSheetSet;
        public string? SelectedPdfViewSheetSet { get => _selectedPdfViewSheetSet; set { _selectedPdfViewSheetSet = value; OnPropertyChanged(); } }

        public List<string> AvailablePdfSetups { get; private set; } = new();
        private string? _selectedPdfSetup;
        public string? SelectedPdfSetup { get => _selectedPdfSetup; set { _selectedPdfSetup = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
