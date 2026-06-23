using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;

namespace YangTools.Revit.UI
{
    public partial class LinearPlacementWindow : Window
    {
        private readonly UIApplication _uiapp;
        private LinearPlacementViewModel _viewModel;
        
        private RevitEventHandler _handler;
        private ExternalEvent _externalEvent;

        public LinearPlacementWindow(UIApplication uiapp)
        {
            InitializeComponent();
            _uiapp = uiapp;

            _handler = new RevitEventHandler();
            _externalEvent = ExternalEvent.Create(_handler);

            _viewModel = new LinearPlacementViewModel();
            DataContext = _viewModel;

            LoadFamilySymbols();
        }

        private void LoadFamilySymbols()
        {
            _handler.SetAction(app =>
            {
                var doc = app.ActiveUIDocument.Document;
                var symbols = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .OrderBy(x => x.FamilyName)
                    .ThenBy(x => x.Name)
                    .ToList();

                Dispatcher.Invoke(() =>
                {
                    _viewModel.AvailableSymbols = symbols;
                    if (symbols.Any())
                    {
                        _viewModel.SelectedSymbol = symbols.FirstOrDefault();
                    }
                });
            }, "加载族类型");
            _externalEvent.Raise();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void PickPath_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();

            _handler.SetAction(app =>
            {
                try
                {
                    var engine = new LinearPlacementEngine(app.ActiveUIDocument);
                    var curves = engine.PickAndExtractCurves();
                    
                    Dispatcher.Invoke(() =>
                    {
                        if (curves != null && curves.Count > 0)
                        {
                            _viewModel.ChainedCurves = curves;
                            double totalLenMm = curves.Sum(c => c.Length) * 304.8;
                            _viewModel.PathInfoText = $"已拾取 {curves.Count} 段曲线, 总长 {totalLenMm:F0} mm";
                        }
                    });
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => TaskDialog.Show("错误", "拾取发生错误: " + ex.Message));
                }
                finally
                {
                    Dispatcher.Invoke(() => this.Show());
                }
            }, "拾取路线");
            _externalEvent.Raise();
        }

        private void StartPlacement_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedSymbol == null)
            {
                TaskDialog.Show("提示", "请选择要布置的族类型。");
                return;
            }

            if (_viewModel.ChainedCurves == null || _viewModel.ChainedCurves.Count == 0)
            {
                TaskDialog.Show("提示", "请先拾取要布置的路线。");
                return;
            }

            // Run placement logic in an IExternalEventHandler to safely execute transactions
            _handler.SetAction(app =>
            {
                try
                {
                    var engine = new LinearPlacementEngine(app.ActiveUIDocument);
                    engine.ExecutePlacement(_viewModel);
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    // User canceled picking, ignore
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => TaskDialog.Show("错误", "布置过程中发生错误: " + ex.Message));
                }
            }, "线性布置");
            _externalEvent.Raise();
        }
    }

    public class LinearPlacementViewModel : INotifyPropertyChanged
    {
        public List<Curve>? ChainedCurves { get; set; }

        private string _pathInfoText = "未选择路线";
        public string PathInfoText
        {
            get => _pathInfoText;
            set { _pathInfoText = value; OnPropertyChanged(); }
        }

        private List<FamilySymbol> _availableSymbols = new List<FamilySymbol>();
        public List<FamilySymbol> AvailableSymbols
        {
            get => _availableSymbols;
            set { _availableSymbols = value; OnPropertyChanged(); }
        }

        private FamilySymbol? _selectedSymbol;
        public FamilySymbol? SelectedSymbol
        {
            get => _selectedSymbol;
            set { _selectedSymbol = value; OnPropertyChanged(); }
        }

        private bool _isByDistance = true;
        public bool IsByDistance
        {
            get => _isByDistance;
            set
            {
                if (_isByDistance != value)
                {
                    _isByDistance = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsByParameter));
                    if (_isByDistance)
                    {
                        StartValue = 0;
                        EndValue = 10000;
                        StepValue = 1000;
                    }
                    else
                    {
                        StartValue = 0;
                        EndValue = 1;
                        StepValue = 0.1;
                    }
                }
            }
        }
        public bool IsByParameter
        {
            get => !_isByDistance;
            set => IsByDistance = !value;
        }

        private bool _isBySpacing = true;
        public bool IsBySpacing
        {
            get => _isBySpacing;
            set
            {
                if (_isBySpacing != value)
                {
                    _isBySpacing = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsByCount));
                    OnPropertyChanged(nameof(SpacingOrCountLabel));
                    StepValue = _isBySpacing ? (IsByDistance ? 1000 : 0.1) : 10;
                }
            }
        }
        public bool IsByCount
        {
            get => !_isBySpacing;
            set => IsBySpacing = !value;
        }

        public string SpacingOrCountLabel => IsBySpacing ? "间距数值 (Spacing Value):" : "个数数值 (Count):";

        private double _startValue = 0;
        public double StartValue
        {
            get => _startValue;
            set { _startValue = value; OnPropertyChanged(); }
        }

        private double _endValue = 10000;
        public double EndValue
        {
            get => _endValue;
            set { _endValue = value; OnPropertyChanged(); }
        }

        private double _stepValue = 1000;
        public double StepValue
        {
            get => _stepValue;
            set { _stepValue = value; OnPropertyChanged(); }
        }

        private bool _alignToCurve = true;
        public bool AlignToCurve
        {
            get => _alignToCurve;
            set { _alignToCurve = value; OnPropertyChanged(); }
        }

        private bool _keepVertical = true;
        public bool KeepVertical
        {
            get => _keepVertical;
            set { _keepVertical = value; OnPropertyChanged(); }
        }

        private double _rotationOffset = 0;
        public double RotationOffset
        {
            get => _rotationOffset;
            set { _rotationOffset = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
