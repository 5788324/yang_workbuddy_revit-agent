using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace YangTools.Revit.UI
{
    public partial class ProjectInfoWindow : Window
    {
        private UIApplication _uiapp;
        private Document _doc;

        public ProjectInfoWindow(UIApplication uiapp)
        {
            InitializeComponent();
            _uiapp = uiapp;
            _doc = uiapp.ActiveUIDocument.Document;
            LoadInfo();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void LoadInfo()
        {
            string path = _doc.PathName;
            if (string.IsNullOrEmpty(path))
            {
                TxtDocPath.Text = "文件未保存";
                TxtFileSize.Text = "-";
                TxtLastModified.Text = "-";
                BtnCalculatePurge.IsEnabled = false;
            }
            else
            {
                TxtDocPath.Text = path;
                try
                {
                    TxtFileName.Text = Path.GetFileName(path);
                    FileInfo fi = new FileInfo(path);
                    if (fi.Exists)
                    {
                        double mb = fi.Length / 1024.0 / 1024.0;
                        TxtFileSize.Text = $"{mb:F2} MB";
                        TxtCurrentSizeCard2.Text = $"{mb:F2} MB";
                        TxtLastModified.Text = fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
                    }
                }
                catch
                {
                    TxtFileSize.Text = "读取失败";
                    TxtCurrentSizeCard2.Text = "读取失败";
                }
            }

            TxtRevitVersion.Text = _uiapp.Application.VersionName;

            if (_doc.IsWorkshared)
            {
                PanelCentralPath.Visibility = System.Windows.Visibility.Visible;
                try
                {
                    ModelPath centralModelPath = _doc.GetWorksharingCentralModelPath();
                    string centralPathStr = ModelPathUtils.ConvertModelPathToUserVisiblePath(centralModelPath);
                    TxtCentralPath.Text = string.IsNullOrEmpty(centralPathStr) ? "无" : centralPathStr;
                }
                catch
                {
                    TxtCentralPath.Text = "无法获取";
                }
            }

            try
            {
                var unusedIds = new List<ElementId>();
                var method = _doc.GetType().GetMethod("GetUnusedElements");
                if (method != null)
                {
                    var res = method.Invoke(_doc, new object[] { new HashSet<ElementId>() }) as ICollection<ElementId>;
                    if (res != null) { unusedIds.AddRange(res); }
                }
                else
                {
                    throw new NotSupportedException();
                }
                TxtUnusedCount.Text = $"{unusedIds.Count} 个";
            }
            catch
            {
                TxtUnusedCount.Text = "当前Revit版本不支持直接获取";
                BtnCalculatePurge.IsEnabled = false;
            }
        }

        private void BtnCalculatePurge_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_doc.PathName))
            {
                TaskDialog.Show("提示", "请先保存当前文档！");
                return;
            }

            MessageBoxResult result = MessageBox.Show(
                "计算清理后大小可能需要几秒钟到几十秒时间，期间Revit将无响应。\n是否继续？",
                "提示", MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (result != MessageBoxResult.Yes) return;

            string tempFile = "";
            Document tempDoc = null;

            try
            {
                TxtStatus.Text = "正在复制文件并后台打开...";
                ForceUIToUpdate();

                tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".rvt");
                File.Copy(_doc.PathName, tempFile, true);

                tempDoc = _uiapp.Application.OpenDocumentFile(tempFile);

                TxtStatus.Text = "正在清理未使用图元...";
                ForceUIToUpdate();

                var unusedIds = new List<ElementId>();
                var method = tempDoc.GetType().GetMethod("GetUnusedElements");
                if (method != null)
                {
                    var res = method.Invoke(tempDoc, new object[] { new HashSet<ElementId>() }) as ICollection<ElementId>;
                    if (res != null) { unusedIds.AddRange(res); }
                }
                if (unusedIds.Count > 0)
                {
                    using (Transaction t = new Transaction(tempDoc, "Purge"))
                    {
                        t.Start();
                        tempDoc.Delete(unusedIds);
                        t.Commit();
                    }
                }

                TxtStatus.Text = "正在保存并计算大小...";
                ForceUIToUpdate();

                tempDoc.Save();
                
                FileInfo fi = new FileInfo(tempFile);
                double mb = fi.Length / 1024.0 / 1024.0;
                TxtPurgedSize.Text = $"{mb:F2} MB";

                FileInfo currentFi = new FileInfo(_doc.PathName);
                if (currentFi.Exists)
                {
                    double currentMb = currentFi.Length / 1024.0 / 1024.0;
                    double saved = currentMb - mb;
                    TxtSavedSize.Text = $"{Math.Max(0, saved):F2} MB";
                }

                TxtStatus.Text = "计算完成";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"计算时发生错误:\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                TxtStatus.Text = "计算失败";
            }
            finally
            {
                if (tempDoc != null)
                {
                    tempDoc.Close(false);
                }
                if (!string.IsNullOrEmpty(tempFile) && File.Exists(tempFile))
                {
                    try
                    {
                        File.Delete(tempFile);
                    }
                    catch { }
                }
            }
        }

        private void ForceUIToUpdate()
        {
            System.Windows.Threading.DispatcherFrame frame = new System.Windows.Threading.DispatcherFrame();
            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background,
                new System.Windows.Threading.DispatcherOperationCallback(delegate (object f)
                {
                    ((System.Windows.Threading.DispatcherFrame)f).Continue = false;
                    return null;
                }), frame);
            System.Windows.Threading.Dispatcher.PushFrame(frame);
        }

        private void CopyPath_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(TxtDocPath.Text) && TxtDocPath.Text != "加载中..." && TxtDocPath.Text != "文件未保存")
            {
                System.Windows.Clipboard.SetText(TxtDocPath.Text);
            }
        }

        private void CopyCentralPath_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(TxtCentralPath.Text) && TxtCentralPath.Text != "加载中..." && TxtCentralPath.Text != "无" && TxtCentralPath.Text != "无法获取")
            {
                System.Windows.Clipboard.SetText(TxtCentralPath.Text);
            }
        }
    }
}
