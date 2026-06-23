using System.Windows;
using System.Windows.Input;

namespace YangTools.Revit.UI
{
    public enum MergeSortMode
    {
        SelectionOrder,
        XDesc,
        XAsc,
        YDesc,
        YAsc
    }

    public partial class MergeTextWindow : Window
    {
        public MergeSortMode SortMode { get; private set; } = MergeSortMode.SelectionOrder;
        public bool UseNewlineSeparator { get; private set; } = true;

        public MergeTextWindow()
        {
            InitializeComponent();
            ThemeHelper.ApplyToWindow(this);
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ExecuteButton_Click(object sender, RoutedEventArgs e)
        {
            if (RbXDesc.IsChecked == true)
                SortMode = MergeSortMode.XDesc;
            else if (RbXAsc.IsChecked == true)
                SortMode = MergeSortMode.XAsc;
            else if (RbYDesc.IsChecked == true)
                SortMode = MergeSortMode.YDesc;
            else if (RbYAsc.IsChecked == true)
                SortMode = MergeSortMode.YAsc;
            else
                SortMode = MergeSortMode.SelectionOrder;

            UseNewlineSeparator = ChkUseNewline.IsChecked == true;

            DialogResult = true;
            Close();
        }
    }
}
