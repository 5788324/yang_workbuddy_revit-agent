using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace YangTools.Revit.UI
{
    public enum SortMode
    {
        SelectionOrder,
        XAsc,
        XDesc,
        YAsc,
        YDesc
    }

    public enum DistributeDirection
    {
        None,
        X,
        Y
    }

    public enum AlignMode
    {
        None,
        AlignX,
        AlignY
    }

    public partial class DistributeTextWindow : Window
    {
        public SortMode SortMode { get; private set; }
        public DistributeDirection DistributeDir { get; private set; }
        public AlignMode AlignMode { get; private set; }
        public double SpacingMm { get; private set; }

        public DistributeTextWindow()
        {
            InitializeComponent();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (cmbSortBy.SelectedItem is ComboBoxItem sortItem)
            {
                switch (sortItem.Tag.ToString())
                {
                    case "SelectionOrder": SortMode = SortMode.SelectionOrder; break;
                    case "XAsc": SortMode = SortMode.XAsc; break;
                    case "XDesc": SortMode = SortMode.XDesc; break;
                    case "YAsc": SortMode = SortMode.YAsc; break;
                    case "YDesc": SortMode = SortMode.YDesc; break;
                }
            }

            if (cmbDistributeDir.SelectedItem is ComboBoxItem dirItem)
            {
                switch (dirItem.Tag.ToString())
                {
                    case "None": DistributeDir = DistributeDirection.None; break;
                    case "X": DistributeDir = DistributeDirection.X; break;
                    case "Y": DistributeDir = DistributeDirection.Y; break;
                }
            }

            if (cmbAlignMode.SelectedItem is ComboBoxItem alignItem)
            {
                switch (alignItem.Tag.ToString())
                {
                    case "None": AlignMode = AlignMode.None; break;
                    case "AlignX": AlignMode = AlignMode.AlignX; break;
                    case "AlignY": AlignMode = AlignMode.AlignY; break;
                }
            }

            if (double.TryParse(txtSpacing.Text, out double val))
            {
                SpacingMm = val;
            }
            else
            {
                SpacingMm = 500;
            }

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
