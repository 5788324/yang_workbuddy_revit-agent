using System.Collections.ObjectModel;
using System.ComponentModel;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Models
{
    public class SheetItemViewModel : INotifyPropertyChanged
    {
        private string _sheetNumber;
        private string _sheetName;
        private string _revisions;

        public ElementId SheetId { get; set; }

        public int Index { get; set; }

        public Action<string, string> OnSheetInfoChanged;

        public string SheetNumber
        {
            get => _sheetNumber;
            set {
                if (_sheetNumber != value) {
                    _sheetNumber = value;
                    OnPropertyChanged(nameof(SheetNumber));
                    OnSheetInfoChanged?.Invoke("SheetNumber", value);
                }
            }
        }

        public string SheetName
        {
            get => _sheetName;
            set {
                if (_sheetName != value) {
                    _sheetName = value;
                    OnPropertyChanged(nameof(SheetName));
                    OnSheetInfoChanged?.Invoke("SheetName", value);
                }
            }
        }

        public string Revisions
        {
            get => _revisions;
            set { _revisions = value; OnPropertyChanged(nameof(Revisions)); }
        }

        public Dictionary<string, ParameterItem> Parameters { get; set; } = new Dictionary<string, ParameterItem>();

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }

    public class TitleblockItemViewModel : INotifyPropertyChanged
    {
        public ElementId InstanceId { get; set; }
        public long InstanceIdValue { get; set; }
        public ElementId SheetId { get; set; }
        public string SheetNumber { get; set; }
        public string SheetName { get; set; }

        private ElementId _typeId;
        public ElementId TypeId
        {
            get => _typeId;
            set { _typeId = value; OnPropertyChanged(nameof(TypeId)); }
        }

        public string TypeName { get; set; }

        public Dictionary<string, ParameterItem> Parameters { get; set; } = new Dictionary<string, ParameterItem>();

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
