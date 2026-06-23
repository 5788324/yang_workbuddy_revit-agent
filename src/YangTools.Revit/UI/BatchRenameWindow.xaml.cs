using System.Windows;

namespace YangTools.Revit.UI
{
    public partial class BatchRenameWindow : Window
    {
        public string PrefixText => PrefixTextBox.Text;
        public string SuffixText => SuffixTextBox.Text;
        public string FindText => FindTextBox.Text;
        public string ReplaceText => ReplaceTextBox.Text;
        public bool ApplyToFamily => ApplyToFamilyCheckBox.IsChecked ?? false;

        public BatchRenameWindow()
        {
            InitializeComponent();
            ThemeHelper.ApplyToWindow(this);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
