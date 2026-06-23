using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using YangTools.Revit.Commands;

namespace YangTools.Revit.UI
{
    public partial class AlignTextToLineWindow : Window
    {
        public AlignToLineOptions Options { get; private set; }

        public AlignTextToLineWindow()
        {
            InitializeComponent();
            ThemeHelper.ApplyToWindow(this);
            Options = new AlignToLineOptions();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            Options.KeepUpright = chkKeepUpright.IsChecked ?? true;
            
            var selectedItem = cmbAlignBase.SelectedItem as ComboBoxItem;
            if (selectedItem != null)
            {
                string tag = selectedItem.Tag.ToString();
                switch (tag)
                {
                    case "Retain":
                        Options.AlignBase = AlignBasePoint.Retain;
                        break;
                    case "Start":
                        Options.AlignBase = AlignBasePoint.Start;
                        break;
                    case "End":
                        Options.AlignBase = AlignBasePoint.End;
                        break;
                }
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
