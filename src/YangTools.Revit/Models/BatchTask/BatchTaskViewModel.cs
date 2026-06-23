using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Models.BatchTask
{
    public class LinkItem : INotifyPropertyChanged
    {
        public string FilePath { get; set; }
        public string FileName => Path.GetFileName(FilePath);
        public bool IsCad => FilePath?.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase) == true || FilePath?.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase) == true;
        public bool IsRevit => FilePath?.EndsWith(".rvt", StringComparison.OrdinalIgnoreCase) == true;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class BatchTaskViewModel : INotifyPropertyChanged
    {
        private Document _doc;

        public BatchTaskViewModel(Document doc)
        {
            _doc = doc;
            LoadDocumentSettings();

            OutputFolder = @"C:\YangTools\BatchExport";
            
            // Defaults
            NwcFileName = $"{_doc.Title}_NWC";
            IfcFileName = $"{_doc.Title}_IFC";
            DwgFileName = $"{_doc.Title}_DWG";
            PdfFileName = $"{_doc.Title}_PDF";
        }

        private void LoadDocumentSettings()
        {
            // Load View/Sheet Sets
            var sets = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSheetSet))
                .Cast<ViewSheetSet>()
                .Select(s => s.Name)
                .OrderBy(n => n)
                .ToList();
            
            AvailableViewSheetSets = sets.Any() ? sets : new List<string> { "<当前视图>" };
            SelectedPdfViewSheetSet = AvailableViewSheetSets.FirstOrDefault();
            SelectedDwgViewSheetSet = AvailableViewSheetSets.FirstOrDefault();

            // Load DWG Setups
            var dwgSetups = new FilteredElementCollector(_doc)
                .OfClass(typeof(ExportDWGSettings))
                .Cast<ExportDWGSettings>()
                .Select(s => s.Name)
                .OrderBy(n => n)
                .ToList();
            
            AvailableDwgSetups = dwgSetups.Any() ? dwgSetups : new List<string> { "<默认内置设置>" };
            SelectedDwgSetup = AvailableDwgSetups.FirstOrDefault();

            // Load PDF Setups (only if Revit 2024+)
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

        public ObservableCollection<LinkItem> Links { get; set; } = new ObservableCollection<LinkItem>();

        private string _outputFolder;
        public string OutputFolder { get => _outputFolder; set { _outputFolder = value; OnPropertyChanged(); } }

        // --- NWC ---
        private bool _enableNwc;
        public bool EnableNwc
        {
            get => _enableNwc;
            set { _enableNwc = value; OnPropertyChanged(); }
        }

        private string _nwcFileName = "Model";
        public string NwcFileName
        {
            get => _nwcFileName;
            set { _nwcFileName = value; OnPropertyChanged(); }
        }

        private string _nwcViewSetName = "<当前视图>";
        public string NwcViewSetName
        {
            get => _nwcViewSetName;
            set { _nwcViewSetName = value; OnPropertyChanged(); }
        }

        private bool _enableIfc;
        public bool EnableIfc
        {
            get => _enableIfc;
            set { _enableIfc = value; OnPropertyChanged(); }
        }

        private string _ifcVersion = "IFC 4";
        public string IfcVersion
        {
            get => _ifcVersion;
            set { _ifcVersion = value; OnPropertyChanged(); }
        }

        private string _ifcFileName = "Model";
        public string IfcFileName
        {
            get => _ifcFileName;
            set { _ifcFileName = value; OnPropertyChanged(); }
        }

        private string _ifcViewSetName = "<当前视图>";
        public string IfcViewSetName
        {
            get => _ifcViewSetName;
            set { _ifcViewSetName = value; OnPropertyChanged(); }
        }

        public List<string> AvailableIfcVersions { get; } = new List<string> { "IFC 2x3", "IFC 4" };
        private string _selectedIfcVersion = "IFC 4";
        public string SelectedIfcVersion { get => _selectedIfcVersion; set { _selectedIfcVersion = value; OnPropertyChanged(); } }

        // --- DWG ---
        private bool _enableDwg;
        public bool EnableDwg { get => _enableDwg; set { _enableDwg = value; OnPropertyChanged(); } }

        private string _dwgFileName;
        public string DwgFileName { get => _dwgFileName; set { _dwgFileName = value; OnPropertyChanged(); } }

        public List<string> AvailableDwgViewSheetSets { get; private set; }
        private string _selectedDwgViewSheetSet;
        public string SelectedDwgViewSheetSet { get => _selectedDwgViewSheetSet; set { _selectedDwgViewSheetSet = value; OnPropertyChanged(); } }

        public List<string> AvailableDwgSetups { get; private set; }
        private string _selectedDwgSetup;
        public string SelectedDwgSetup { get => _selectedDwgSetup; set { _selectedDwgSetup = value; OnPropertyChanged(); } }

        // --- PDF ---
        private bool _enablePdf;
        public bool EnablePdf { get => _enablePdf; set { _enablePdf = value; OnPropertyChanged(); } }

        private string _pdfFileName = "Drawings";
        public string PdfFileName
        {
            get => _pdfFileName;
            set { _pdfFileName = value; OnPropertyChanged(); }
        }

        private bool _pdfCombine = true;
        public bool PdfCombine
        {
            get => _pdfCombine;
            set { _pdfCombine = value; OnPropertyChanged(); }
        }

        public List<string> AvailableViewSheetSets { get; private set; }
        private string _selectedPdfViewSheetSet;
        public string SelectedPdfViewSheetSet { get => _selectedPdfViewSheetSet; set { _selectedPdfViewSheetSet = value; OnPropertyChanged(); } }

        public List<string> AvailablePdfSetups { get; private set; }
        private string _selectedPdfSetup;
        public string SelectedPdfSetup { get => _selectedPdfSetup; set { _selectedPdfSetup = value; OnPropertyChanged(); } }


        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
