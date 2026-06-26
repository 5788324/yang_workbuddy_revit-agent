using System.ComponentModel;

namespace YangTools.Revit.Models
{
    public class ParameterViewModel : INotifyPropertyChanged
    {
        private string _value;
        public string Value { get => _value; set { _value = value; OnPropertyChanged(nameof(Value)); } }

        private bool _boolValue;
        public bool BoolValue { get => _boolValue; set { _boolValue = value; OnPropertyChanged(nameof(BoolValue)); } }

        private bool _isReadOnly;
        public bool IsReadOnly { get => _isReadOnly; set { _isReadOnly = value; OnPropertyChanged(nameof(IsReadOnly)); } }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }

    public class AvailableParamItem
    {
        public string Name { get; set; }
        public bool IsReadOnly { get; set; }
    }

    public class ParameterItem : INotifyPropertyChanged
    {
        private string _name;
        private string _value;

        public Action<string, string> OnValueChanged;

        public string Name { get => _name; set { _name = value; OnPropertyChanged(nameof(Name)); } }
        public string Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value;
                    OnPropertyChanged(nameof(Value));
                    OnValueChanged?.Invoke(Name, value);
                }
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
