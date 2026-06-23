using System.Windows;
using System.Windows.Input;

namespace YangTools.Revit.UI
{
    public partial class AlignTextToTextWindow : Window
    {
        public bool AlignX { get; private set; }
        public bool AlignY { get; private set; }
        public bool AlignRotation { get; private set; }

        public AlignTextToTextWindow()
        {
            InitializeComponent();
            ThemeHelper.ApplyToWindow(this);
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            AlignX = chkAlignX.IsChecked ?? false;
            AlignY = chkAlignY.IsChecked ?? false;
            AlignRotation = chkAlignRot.IsChecked ?? false;

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
