using System.Windows;

namespace YangTools.Revit.UI
{
    public partial class McpStatusWindow : Window
    {
        public McpStatusWindow()
        {
            InitializeComponent();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
