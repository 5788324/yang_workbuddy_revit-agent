using System;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace YangTools.Revit.UI
{
    public partial class NewProjectDialog : Window
    {
        public string ProjectName => ProjectNameBox.Text.Trim();
        public string FolderPath => FolderPathBox.Text.Trim();

        public NewProjectDialog()
        {
            InitializeComponent();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Title = "进入目标文件夹，点击“打开”即可选择路径";
            dialog.ValidateNames = false;
            dialog.CheckFileExists = false;
            dialog.CheckPathExists = true;
            dialog.FileName = "Folder Selection.";
            
            if (dialog.ShowDialog() == true)
            {
                FolderPathBox.Text = Path.GetDirectoryName(dialog.FileName);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ProjectName))
            {
                MessageBox.Show("项目名称不能为空。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(FolderPath))
            {
                MessageBox.Show("文件夹路径不能为空。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Ensure valid path
                var fullPath = Path.GetFullPath(FolderPath);
                
                if (!Directory.Exists(fullPath))
                {
                    Directory.CreateDirectory(fullPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无效的文件夹路径或无法创建目录：\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            DialogResult = true;
            Close();
        }
    }
}
