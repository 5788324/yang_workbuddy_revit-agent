using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Autodesk.Revit.DB;
using YangTools.Revit.Commands;

namespace YangTools.Revit.UI
{
    public partial class ViewGraphicCleanerWindow : Window
    {
        private readonly Document _doc;
        private ObservableCollection<CleanerViewItem> _allViews;
        private ObservableCollection<CleanerViewItem> _filteredViews;

        public ViewGraphicCleanerWindow(Document doc)
        {
            InitializeComponent();
            ThemeHelper.ApplyToWindow(this);
            _doc = doc;
            _allViews = new ObservableCollection<CleanerViewItem>();
            _filteredViews = new ObservableCollection<CleanerViewItem>();
            ViewsList.ItemsSource = _filteredViews;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadViews();
        }

        private void LoadViews()
        {
            var views = new FilteredElementCollector(_doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate && 
                           (v.ViewType == ViewType.FloorPlan ||
                            v.ViewType == ViewType.Section ||
                            v.ViewType == ViewType.ThreeD ||
                            v.ViewType == ViewType.Elevation ||
                            v.ViewType == ViewType.CeilingPlan ||
                            v.ViewType == ViewType.Detail ||
                            v.ViewType == ViewType.DraftingView))
                .OrderBy(v => v.Name)
                .ToList();

            foreach (var view in views)
            {
                var item = new CleanerViewItem(view);
                _allViews.Add(item);
                _filteredViews.Add(item);
            }

            StatusText.Text = $"共加载 {_allViews.Count} 个图形视图";
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            
            string query = SearchBox.Text.ToLower();
            _filteredViews.Clear();
            
            foreach(var item in _allViews)
            {
                if (item.ViewName.ToLower().Contains(query) || item.ViewTypeName.ToLower().Contains(query))
                {
                    _filteredViews.Add(item);
                }
            }
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _filteredViews) item.IsSelected = true;
        }

        private void InvertSelection_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _filteredViews) item.IsSelected = !item.IsSelected;
        }

        private void CalculateSelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = _filteredViews.Where(x => x.IsSelected && !x.IsCalculated).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("没有选中未计算的视图。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            StatusText.Text = "正在计算...";
            int totalChecked = 0;

            foreach (var item in selected)
            {
                item.OverrideCount = "计算中...";
                
                // Force UI refresh without yielding API context
                System.Windows.Application.Current.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(delegate { }));

                int count = CountOverrides(item.View);
                item.ActualCount = count;
                item.OverrideCount = count > 0 ? $"有 {count} 个覆盖元素" : "无";
                item.IsCalculated = true;
                totalChecked++;
            }

            StatusText.Text = $"计算完成，共检查了 {totalChecked} 个视图。";
        }

        private int CountOverrides(View view)
        {
            try
            {
                int count = 0;
                var elements = new FilteredElementCollector(_doc, view.Id)
                    .WhereElementIsNotElementType()
                    .ToElementIds();

                foreach (var id in elements)
                {
                    var overrides = view.GetElementOverrides(id);
                    if (overrides.HasOverrides())
                    {
                        count++;
                    }
                }
                return count;
            }
            catch
            {
                return 0;
            }
        }

        private void ExecuteClean_Click(object sender, RoutedEventArgs e)
        {
            var selectedViews = _allViews.Where(x => x.IsSelected).ToList();
            if (selectedViews.Count == 0)
            {
                MessageBox.Show("请至少选择一个视图进行清理。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            StatusText.Text = "正在清理...";
            int totalClearedViews = 0;
            int totalClearedElements = 0;

            using (TransactionGroup tg = new TransactionGroup(_doc, "清理视图覆盖"))
            {
                tg.Start();

                foreach (var item in selectedViews)
                {
                    var elements = new FilteredElementCollector(_doc, item.View.Id)
                        .WhereElementIsNotElementType()
                        .ToElementIds();

                    bool hasTransaction = false;

                    // Collect overrides first to know if we need a transaction
                    var overridesToClear = new System.Collections.Generic.List<ElementId>();
                    foreach (var id in elements)
                    {
                        var overrides = item.View.GetElementOverrides(id);
                        if (overrides.HasOverrides())
                        {
                            overridesToClear.Add(id);
                        }
                    }

                    if (overridesToClear.Count > 0)
                    {
                        using (Transaction t = new Transaction(_doc, $"清理 {item.ViewName}"))
                        {
                            t.Start();
                            foreach (var id in overridesToClear)
                            {
                                item.View.SetElementOverrides(id, new OverrideGraphicSettings());
                                totalClearedElements++;
                            }
                            t.Commit();
                        }
                        totalClearedViews++;
                        item.ActualCount = 0;
                        item.OverrideCount = "已清理";
                        item.IsCalculated = true;
                        item.IsSelected = false; // Deselect after successful clean
                    }
                    else
                    {
                        // Even if there were no overrides, mark as calculated 0
                        item.ActualCount = 0;
                        item.OverrideCount = "无";
                        item.IsCalculated = true;
                        item.IsSelected = false;
                    }
                }

                tg.Assimilate();
            }

            StatusText.Text = $"清理完成！共处理 {totalClearedViews} 个视图，清除了 {totalClearedElements} 个元素的覆盖。";
            MessageBox.Show($"清理成功！\n共清理了 {totalClearedViews} 个视图中的 {totalClearedElements} 个元素图形覆盖。", "清理完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    public class CleanerViewItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        public bool IsSelected 
        { 
            get => _isSelected; 
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); } 
        }

        public View View { get; }
        public string ViewName { get; }
        public string ViewTypeName { get; }

        private string _overrideCount;
        public string OverrideCount 
        { 
            get => _overrideCount; 
            set { _overrideCount = value; OnPropertyChanged(nameof(OverrideCount)); } 
        }

        private bool _isCalculated;
        public bool IsCalculated
        {
            get => _isCalculated;
            set { _isCalculated = value; OnPropertyChanged(nameof(IsCalculated)); }
        }
        
        public int ActualCount { get; set; }

        public CleanerViewItem(View view)
        {
            View = view;
            ViewName = view.Name;
            ViewTypeName = TranslateViewType(view.ViewType);
            OverrideCount = "点击计算或清理";
            IsCalculated = false;
            ActualCount = 0;
        }

        private string TranslateViewType(ViewType type)
        {
            switch(type)
            {
                case ViewType.FloorPlan: return "平面";
                case ViewType.Section: return "剖面";
                case ViewType.Elevation: return "立面";
                case ViewType.ThreeD: return "三维";
                case ViewType.CeilingPlan: return "天花板";
                case ViewType.Detail: return "详图";
                case ViewType.DraftingView: return "绘图";
                default: return type.ToString();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
