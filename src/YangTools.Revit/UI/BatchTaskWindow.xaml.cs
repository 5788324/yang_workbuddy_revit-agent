using System;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core.BatchTasks;
using YangTools.Revit.Models.BatchTask;
using MessageBox = System.Windows.MessageBox;
using Button = System.Windows.Controls.Button;

namespace YangTools.Revit.UI
{
    public partial class BatchTaskWindow : Window
    {
        private Document _doc;
        private BatchTaskViewModel _viewModel;

        public BatchTaskWindow(Document doc)
        {
            InitializeComponent();
            _doc = doc;
            
            _viewModel = new BatchTaskViewModel(doc);
            this.DataContext = _viewModel;
        }

        private void BtnAddFile_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "Revit & CAD Files (*.rvt;*.dwg;*.dxf)|*.rvt;*.dwg;*.dxf|Revit Models (*.rvt)|*.rvt|CAD Files (*.dwg;*.dxf)|*.dwg;*.dxf|All Files (*.*)|*.*";
            dlg.Multiselect = true;
            
            if (dlg.ShowDialog() == true)
            {
                foreach (var file in dlg.FileNames)
                {
                    _viewModel.Links.Add(new LinkItem { FilePath = file });
                }
            }
        }

        private void BtnRemoveLink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is LinkItem item)
            {
                _viewModel.Links.Remove(item);
            }
        }

        private void BtnBrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                ValidateNames = false,
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "选择此文件夹"
            };

            if (!string.IsNullOrWhiteSpace(_viewModel.OutputFolder) && System.IO.Directory.Exists(_viewModel.OutputFolder))
            {
                dialog.InitialDirectory = _viewModel.OutputFolder;
            }
            
            if (dialog.ShowDialog() == true)
            {
                string folder = System.IO.Path.GetDirectoryName(dialog.FileName);
                if (!string.IsNullOrEmpty(folder))
                {
                    _viewModel.OutputFolder = folder;
                }
            }
        }

        private void BtnExecute_Click(object sender, RoutedEventArgs e)
        {
            if (!_viewModel.EnableNwc && !_viewModel.EnableIfc && !_viewModel.EnableDwg && !_viewModel.EnablePdf && _viewModel.Links.Count == 0)
            {
                MessageBox.Show("请至少添加一个链接文件或开启一项导出任务！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TxtProgress.Text = "正在后台执行任务，请稍候...";
            FlushUI();

            try
            {
                BatchTaskEngine.RunBatchGui(_doc, _viewModel, (msg) =>
                {
                    TxtProgress.Text = msg;
                    FlushUI();
                });

                TxtProgress.Text = "批处理全部完成！";
                MessageBox.Show("所有任务已执行完毕！", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"执行出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                TxtProgress.Text = "执行中断";
            }
        }

        private void FlushUI()
        {
            Dispatcher.Invoke(new Action(() => { }), System.Windows.Threading.DispatcherPriority.Background);
        }
    }
}
