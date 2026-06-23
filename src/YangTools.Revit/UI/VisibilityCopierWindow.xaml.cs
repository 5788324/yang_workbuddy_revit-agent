using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.DB;
using YangTools.Revit.Core;

namespace YangTools.Revit.UI
{
    public class ViewItem : System.ComponentModel.INotifyPropertyChanged
    {
        public View View { get; set; }
        
        public string Name => $"[{GetViewTypeString(View.ViewType)}] {View.Name}";

        private string GetViewTypeString(ViewType vt)
        {
            switch(vt)
            {
                case ViewType.FloorPlan: return "楼层平面";
                case ViewType.CeilingPlan: return "天花板平面";
                case ViewType.Elevation: return "立面";
                case ViewType.ThreeD: return "三维";
                case ViewType.Section: return "剖面";
                case ViewType.Detail: return "详图";
                default: return vt.ToString();
            }
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    }

    public partial class VisibilityCopierWindow : Window
    {
        private Document _doc;
        public ObservableCollection<ViewItem> AllViews { get; set; }
        public ObservableCollection<ViewItem> TargetViews { get; set; }

        public VisibilityCopierWindow(Document doc)
        {
            InitializeComponent();
            _doc = doc;
            AllViews = new ObservableCollection<ViewItem>();
            TargetViews = new ObservableCollection<ViewItem>();
            
            LoadViews();
            
            CmbSourceView.ItemsSource = AllViews;
            LstTargetViews.ItemsSource = TargetViews;
            
            if (AllViews.Count > 0)
            {
                // Set default source to active view if it's in the list
                var activeViewId = doc.ActiveView.Id;
                var activeItem = AllViews.FirstOrDefault(v => v.View.Id == activeViewId);
                if (activeItem != null)
                {
                    CmbSourceView.SelectedItem = activeItem;
                }
                else
                {
                    CmbSourceView.SelectedIndex = 0;
                }
            }
        }

        private void LoadViews()
        {
            var views = new FilteredElementCollector(_doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate && v.CanUseTemporaryVisibilityModes() && !v.IsAssemblyView)
                .Where(v => v.ViewType == ViewType.FloorPlan || v.ViewType == ViewType.CeilingPlan || 
                            v.ViewType == ViewType.ThreeD || v.ViewType == ViewType.Elevation || 
                            v.ViewType == ViewType.Section || v.ViewType == ViewType.Detail)
                .OrderBy(v => GetViewTypeOrder(v.ViewType))
                .ThenBy(v => v.Name)
                .ToList();

            foreach (var view in views)
            {
                AllViews.Add(new ViewItem { View = view });
            }
        }
        
        private int GetViewTypeOrder(ViewType vt)
        {
            switch (vt)
            {
                case ViewType.FloorPlan: return 1;
                case ViewType.CeilingPlan: return 2;
                case ViewType.Elevation: return 3;
                case ViewType.Section: return 4;
                case ViewType.ThreeD: return 5;
                case ViewType.Detail: return 6;
                default: return 99;
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CmbSourceView_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var selectedItem = CmbSourceView.SelectedItem as ViewItem;
            TargetViews.Clear();
            foreach (var view in AllViews)
            {
                if (selectedItem == null || view.View.Id != selectedItem.View.Id)
                {
                    TargetViews.Add(new ViewItem { View = view.View, IsSelected = false });
                }
            }
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in TargetViews) item.IsSelected = true;
        }

        private void SelectNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in TargetViews) item.IsSelected = false;
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            var sourceItem = CmbSourceView.SelectedItem as ViewItem;
            if (sourceItem == null)
            {
                MessageBox.Show("请选择源视图。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var targets = TargetViews.Where(x => x.IsSelected).Select(x => x.View).ToList();
            if (targets.Count == 0)
            {
                MessageBox.Show("请至少选择一个目标视图。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (Transaction t = new Transaction(_doc, "可见性拷贝"))
                {
                    t.Start();
                    
                    var sourceView = sourceItem.View;
                    
                    bool copyModel = ChkModelCategories.IsChecked == true;
                    bool copyAnno = ChkAnnotationCategories.IsChecked == true;
                    bool copyAnalytic = ChkAnalyticalCategories.IsChecked == true;
                    bool copyImport = ChkImportCategories.IsChecked == true;
                    bool copyFilters = ChkFilters.IsChecked == true;
                    bool copyWorksets = ChkWorksets.IsChecked == true;
                    bool copyLinks = ChkRvtLinks.IsChecked == true;

                    foreach (var target in targets)
                    {
                        CopyVisibility(sourceView, target, copyModel, copyAnno, copyAnalytic, copyImport, copyFilters, copyWorksets, copyLinks);
                    }
                    
                    t.Commit();
                }
                
                MessageBox.Show("可见性拷贝完成。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"拷贝失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CopyVisibility(View source, View target, bool copyModel, bool copyAnno, bool copyAnalytic, bool copyImport, bool copyFilters, bool copyWorksets, bool copyLinks)
        {
            var doc = source.Document;

            // 1. Categories
            var categories = doc.Settings.Categories;
            Category importCat = null;
            try { importCat = categories.get_Item(BuiltInCategory.OST_ImportObjectStyles); } catch {}

            foreach (Category cat in categories)
            {
                if (cat == null) continue;

                bool isImport = false;
                if (importCat != null && (cat.Id == importCat.Id || (cat.Parent != null && cat.Parent.Id == importCat.Id)))
                {
                    isImport = true;
                }

                bool shouldCopy = false;
                if (isImport)
                {
                    shouldCopy = copyImport;
                }
                else if (cat.Id.GetIdValue() == (int)BuiltInCategory.OST_RvtLinks || (cat.Parent != null && cat.Parent.Id.GetIdValue() == (int)BuiltInCategory.OST_RvtLinks))
                {
                    shouldCopy = copyLinks;
                }
                else
                {
                    switch (cat.CategoryType)
                    {
                        case CategoryType.Model:
                            shouldCopy = copyModel;
                            break;
                        case CategoryType.Annotation:
                            shouldCopy = copyAnno;
                            break;
                        case CategoryType.AnalyticalModel:
                            shouldCopy = copyAnalytic;
                            break;
                    }
                }

                if (shouldCopy)
                {
                    try
                    {
                        if (cat.get_AllowsVisibilityControl(source) && cat.get_AllowsVisibilityControl(target))
                        {
                            bool isHidden = source.GetCategoryHidden(cat.Id);
                            target.SetCategoryHidden(cat.Id, isHidden);
                        }

                        if (!source.IsCategoryOverridable(cat.Id) || !target.IsCategoryOverridable(cat.Id)) continue;
                        
                        var overrides = source.GetCategoryOverrides(cat.Id);
                        target.SetCategoryOverrides(cat.Id, overrides);
                    }
                    catch { } // Ignore errors for specific categories that might not support overrides
                }
            }

            // 2. Filters
            if (copyFilters)
            {
                try
                {
                    var sourceFilters = source.GetFilters();
                    var targetFilters = target.GetFilters();
                    
                    foreach (var filterId in sourceFilters)
                    {
                        if (!targetFilters.Contains(filterId))
                        {
                            target.AddFilter(filterId);
                        }
                        target.SetFilterVisibility(filterId, source.GetFilterVisibility(filterId));
                        target.SetFilterOverrides(filterId, source.GetFilterOverrides(filterId));
                    }

                    // Optional: remove filters not in source? We skip that to avoid destructive behavior unless strictly required
                }
                catch { }
            }

            // 3. Worksets
            if (copyWorksets && doc.IsWorkshared)
            {
                try
                {
                    var worksets = new FilteredWorksetCollector(doc).OfKind(WorksetKind.UserWorkset).ToList();
                    foreach (var ws in worksets)
                    {
                        var visibility = source.GetWorksetVisibility(ws.Id);
                        target.SetWorksetVisibility(ws.Id, visibility);
                    }
                }
                catch { }
            }
        }
    }
}
