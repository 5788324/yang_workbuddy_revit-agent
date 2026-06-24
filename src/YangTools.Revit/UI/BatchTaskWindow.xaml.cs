using System;
using System.Collections.Generic;
using System.Linq;
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
        private UIApplication _uiApp;
        private BatchTaskViewModel _viewModel;

        public BatchTaskWindow(UIApplication uiApp)
        {
            InitializeComponent();
            ThemeHelper.ApplyToWindow(this);
            _uiApp = uiApp;
            _viewModel = new BatchTaskViewModel(uiApp);
            this.DataContext = _viewModel;
        }

        private void BtnAddDocument_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Revit Models (*.rvt)|*.rvt|All Files (*.*)|*.*",
                Multiselect = true,
                Title = "添加要处理的 Revit 模型（支持 Desktop Connector 云路径）"
            };
            if (dlg.ShowDialog() == true)
                foreach (var file in dlg.FileNames)
                    _viewModel.AddDocument(file);
        }

        private void BtnAddFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Revit & CAD (*.rvt;*.dwg;*.dxf)|*.rvt;*.dwg;*.dxf",
                Multiselect = true,
                Title = "添加要链接的文件"
            };
            if (dlg.ShowDialog() == true)
                foreach (var file in dlg.FileNames)
                    _viewModel.Links.Add(new LinkItem { FilePath = file });
        }

        private void BtnRemoveLink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is LinkItem item)
                _viewModel.Links.Remove(item);
        }

        private void BtnBrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                ValidateNames = false, CheckFileExists = false, CheckPathExists = true,
                FileName = "选择此文件夹"
            };
            if (!string.IsNullOrWhiteSpace(_viewModel.OutputFolder) && System.IO.Directory.Exists(_viewModel.OutputFolder))
                dialog.InitialDirectory = _viewModel.OutputFolder;
            if (dialog.ShowDialog() == true)
            {
                string folder = System.IO.Path.GetDirectoryName(dialog.FileName);
                if (!string.IsNullOrEmpty(folder))
                    _viewModel.OutputFolder = folder;
            }
        }

        private void BtnExecute_Click(object sender, RoutedEventArgs e)
        {
            if (!_viewModel.EnableNwc && !_viewModel.EnableIfc && !_viewModel.EnableDwg && !_viewModel.EnablePdf && _viewModel.Links.Count == 0)
            {
                MessageBox.Show("请至少添加一个链接文件或开启一项导出任务！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TxtProgress.Text = "正在执行，请稍候...";
            FlushUI();

            try
            {
                var allErrors = new List<string>();
                var docs = _viewModel.Documents.Where(d => d.Enabled).ToList();

                foreach (var docInfo in docs)
                {
                    Document targetDoc;
                    if (docInfo.IsCurrentDocument)
                    {
                        targetDoc = _uiApp.ActiveUIDocument.Document;
                    }
                    else
                    {
                        // 打开外部模型
                        var modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(docInfo.FilePath);
                        var openOptions = new OpenOptions { DetachFromCentralOption = DetachFromCentralOption.DetachAndPreserveWorksets };
                        targetDoc = _uiApp.Application.OpenDocumentFile(modelPath, openOptions);
                    }

                    try
                    {
                        string docLabel = docInfo.IsCurrentDocument ? "当前文档" : System.IO.Path.GetFileNameWithoutExtension(docInfo.FilePath);
                        TxtProgress.Text = $"处理: {docLabel}...";

                        var errors = BatchTaskEngine.RunBatch(targetDoc, _viewModel, (msg) =>
                        {
                            TxtProgress.Text = $"[{docLabel}] {msg}";
                            FlushUI();
                        });

                        allErrors.AddRange(errors.Select(e => $"[{docLabel}] {e}"));
                    }
                    finally
                    {
                        // 关闭外部打开的文档（不保存）
                        if (!docInfo.IsCurrentDocument && !targetDoc.IsModifiable)
                        {
                            try { targetDoc.Close(false); } catch { }
                        }
                    }
                }

                if (allErrors.Count == 0)
                {
                    TxtProgress.Text = "批处理全部完成！";
                    MessageBox.Show("所有任务已执行完毕！", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    TxtProgress.Text = $"完成，{allErrors.Count} 个错误";
                    MessageBox.Show($"部分任务失败:\n\n{string.Join("\n", allErrors.Take(10))}" +
                                    (allErrors.Count > 10 ? $"\n\n...及其他 {allErrors.Count - 10} 个错误" : ""),
                                    "完成（有错误）", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
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
