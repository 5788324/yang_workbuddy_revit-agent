using System.Windows;

namespace YangTools.Revit.UI
{
    public partial class SimpleInputDialog : Window
    {
        public string InputText => InputTextBox.Text;

        public SimpleInputDialog(string title, string message, string defaultText = "")
        {
            InitializeComponent();
            ThemeHelper.ApplyToWindow(this);
            this.Title = title;
            this.MessageTextBlock.Text = message;
            this.InputTextBox.Text = defaultText;
            this.InputTextBox.SelectAll();
            this.InputTextBox.Focus();
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
