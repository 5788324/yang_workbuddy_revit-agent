using System.Collections.ObjectModel;
using System.ComponentModel;
using Autodesk.Revit.DB;
using YangTools.Revit.Core;

namespace YangTools.Revit.Models
{
    public class AssetItemViewModel : INotifyPropertyChanged
    {
        private string _assetName;
        public string AssetName
        {
            get => _assetName;
            set { _assetName = value; OnPropertyChanged(nameof(AssetName)); }
        }

        public ElementId AssetId { get; set; }
        public long AssetIdValue => AssetId?.GetIdValue() ?? -1;

        private bool _isRowVisible = true;
        public bool IsRowVisible
        {
            get => _isRowVisible;
            set { _isRowVisible = value; OnPropertyChanged(nameof(IsRowVisible)); }
        }

        public Dictionary<string, ParameterViewModel> Parameters { get; set; } = new Dictionary<string, ParameterViewModel>();

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }

    public class AssetTreeNode : INotifyPropertyChanged
    {
        private bool? _isChecked = false;

        public string Name { get; set; }
        public object Tag { get; set; }
        public string Type { get; set; }

        public AssetTreeNode Parent { get; set; }

        public bool? IsChecked
        {
            get => _isChecked;
            set => SetIsChecked(value, true, true);
        }

        public void SetIsChecked(bool? value, bool updateChildren, bool updateParent)
        {
            if (value == _isChecked) return;
            _isChecked = value;

            if (updateChildren && _isChecked.HasValue)
            {
                foreach (var child in Children)
                {
                    child.SetIsChecked(_isChecked.Value, true, false);
                }
            }

            if (updateParent && Parent != null)
            {
                Parent.VerifyCheckState();
            }

            OnPropertyChanged(nameof(IsChecked));
        }

        private void VerifyCheckState()
        {
            bool? state = null;
            for (int i = 0; i < Children.Count; ++i)
            {
                bool? current = Children[i].IsChecked;
                if (i == 0) state = current;
                else if (state != current)
                {
                    state = null;
                    break;
                }
            }
            SetIsChecked(state, false, true);
        }

        public ObservableCollection<AssetTreeNode> Children { get; set; } = new ObservableCollection<AssetTreeNode>();

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }

    public class PatternComboItem
    {
        public ElementId Id { get; set; }
        public string Name { get; set; }
        public System.Windows.Media.ImageSource Preview { get; set; }
    }

    public class LineStyleViewModel : INotifyPropertyChanged
    {
        public ElementId Id { get; set; }
        public string Name { get; set; }
        public int LineWeight { get; set; }
        public System.Windows.Media.Brush ColorBrush { get; set; }
        public ElementId LinePatternId { get; set; }
        public bool IsBuiltIn { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public class FilledRegionViewModel : INotifyPropertyChanged
    {
        public ElementId Id { get; set; }
        public string Name { get; set; }
        public System.Windows.Media.Brush ForeColorBrush { get; set; }
        public ElementId ForePatternId { get; set; }
        public System.Windows.Media.Brush BackColorBrush { get; set; }
        public ElementId BackPatternId { get; set; }
        public bool IsMasking { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
