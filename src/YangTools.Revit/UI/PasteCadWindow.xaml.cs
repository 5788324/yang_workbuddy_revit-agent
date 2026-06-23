using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Autodesk.Revit.DB;
using MediaColor = System.Windows.Media.Color;

namespace YangTools.Revit.UI
{
    /// <summary>
    /// PasteCadWindow.xaml 的交互逻辑
    /// </summary>
    public partial class PasteCadWindow : Window
    {
        // 导出的所选参数，供外部 Command 调用
        public string? SelectedDwgPath { get; private set; }
        public ImportUnit SelectedUnit { get; private set; }
        public ImportColorMode SelectedColorMode { get; private set; }
        public ImportPlacement SelectedPlacement { get; private set; }
        public bool SelectedThisViewOnly { get; private set; }

        public PasteCadWindow()
        {
            InitializeComponent();
            DetectCadClipboard();
        }

        /// <summary>
        /// 检测 AutoCAD 剪贴板临时文件并渲染界面
        /// AutoCAD 在 Ctrl+C 时会在 %TEMP% 以及当前用户临时目录写入 A$C*.dwg 临时文件
        /// </summary>
        private void DetectCadClipboard()
        {
            try
            {
                // 多路径搜索：AutoCAD 可能在不同路径写入临时 DWG
                var searchDirs = new[]
                {
                    Path.GetTempPath(),
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"),
                    Environment.GetEnvironmentVariable("TEMP") ?? "",
                    Environment.GetEnvironmentVariable("TMP") ?? "",
                };

                var allValidFiles = searchDirs
                    .Where(d => !string.IsNullOrEmpty(d) && Directory.Exists(d))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .SelectMany(dir =>
                    {
                        try
                        {
                            return Directory.GetFiles(dir, "A$C*.dwg");
                        }
                        catch
                        {
                            return Array.Empty<string>();
                        }
                    })
                    .Select(f => new FileInfo(f))
                    .Where(fi =>
                    {
                        try { return fi.Exists && fi.Length > 0; }
                        catch { return false; }
                    })
                    .OrderByDescending(fi => fi.LastWriteTime)
                    .ToList();

                if (allValidFiles.Count == 0)
                {
                    // 未检测到 → 渲染红色警告
                    SetStatus(
                        isSuccess: false,
                        title: "未检测到 AutoCAD 复制数据",
                        desc: "请先在 AutoCAD 软件中选择您需要导入的图元，按下 [Ctrl + C] 复制，然后回到 Revit 点击本按钮。\n\n" +
                              "如刚完成复制仍无法检测，可点击「手动选择文件」按钮，在 Windows 临时目录（%TEMP%）中手动选取 A$C*.dwg 文件。",
                        icon: "⚠"
                    );
                    TxtSelectedFile.Text = "（未检测到 AutoCAD 临时剪贴板文件）";
                    TxtSelectedFile.Foreground = new SolidColorBrush(MediaColor.FromRgb(0xA0, 0x80, 0x68));
                    SelectedDwgPath = null;
                    BtnConfirm.IsEnabled = false;
                }
                else
                {
                    var latestFile = allValidFiles.First();
                    SetFileSelected(latestFile);
                }
            }
            catch (Exception ex)
            {
                SetStatus(
                    isSuccess: false,
                    title: "检测过程发生错误",
                    desc: ex.Message,
                    icon: "❌"
                );
                SelectedDwgPath = null;
                BtnConfirm.IsEnabled = false;
            }
        }

        /// <summary>
        /// 设置已选中文件并更新 UI 状态
        /// </summary>
        private void SetFileSelected(FileInfo fileInfo)
        {
            SelectedDwgPath = fileInfo.FullName;

            string timeStr = fileInfo.LastWriteTime.ToString("MM-dd HH:mm:ss");
            string sizeStr = fileInfo.Length >= 1024
                ? (fileInfo.Length / 1024.0).ToString("F1") + " KB"
                : fileInfo.Length + " B";

            SetStatus(
                isSuccess: true,
                title: "已检测到 AutoCAD 剪贴板文件！",
                desc: $"文件：{fileInfo.Name}\n修改时间：{timeStr}　大小：{sizeStr}",
                icon: "✔"
            );

            TxtSelectedFile.Text = fileInfo.FullName;
            TxtSelectedFile.Foreground = new SolidColorBrush(MediaColor.FromRgb(0x5A, 0x7A, 0x3A));
            BtnConfirm.IsEnabled = true;
        }

        /// <summary>
        /// 渲染状态卡片颜色与文本
        /// </summary>
        private void SetStatus(bool isSuccess, string title, string desc, string icon)
        {
            TxtStatusIcon.Text = icon;
            TxtStatusTitle.Text = title;
            TxtStatusDesc.Text = desc;

            if (isSuccess)
            {
                // 长安色系 - 抹茶绿成功状态
                BdrStatus.Background = new SolidColorBrush(MediaColor.FromRgb(0xE8, 0xF0, 0xD8));
                BdrStatus.BorderBrush = new SolidColorBrush(MediaColor.FromRgb(0xA0, 0xC0, 0x70));
                TxtStatusIcon.Foreground = new SolidColorBrush(MediaColor.FromRgb(0x5A, 0x7A, 0x3A));
                TxtStatusTitle.Foreground = new SolidColorBrush(MediaColor.FromRgb(0x5A, 0x7A, 0x3A));
            }
            else
            {
                // 长安色系 - 朱砂红警告状态
                BdrStatus.Background = new SolidColorBrush(MediaColor.FromRgb(0xF8, 0xE8, 0xE4));
                BdrStatus.BorderBrush = new SolidColorBrush(MediaColor.FromRgb(0xD0, 0x90, 0x80));
                TxtStatusIcon.Foreground = new SolidColorBrush(MediaColor.FromRgb(0xA0, 0x30, 0x20));
                TxtStatusTitle.Foreground = new SolidColorBrush(MediaColor.FromRgb(0xA0, 0x30, 0x20));
            }
        }

        /// <summary>
        /// 手动浏览 DWG 文件
        /// </summary>
        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择 AutoCAD 剪贴板临时 DWG 文件",
                Filter = "DWG 文件 (*.dwg)|*.dwg|所有文件 (*.*)|*.*",
                InitialDirectory = Path.GetTempPath(),
                FileName = ""
            };

            if (dialog.ShowDialog() == true)
            {
                var fi = new FileInfo(dialog.FileName);
                if (!fi.Exists || fi.Length == 0)
                {
                    MessageBox.Show("所选文件无效或大小为零，请重新选择。", "YangTools 提示",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                SetFileSelected(fi);
            }
        }

        /// <summary>
        /// 点击确定粘贴
        /// </summary>
        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(SelectedDwgPath) || !File.Exists(SelectedDwgPath))
            {
                MessageBox.Show("剪贴板临时文件已被清除或不可达，请重新在 AutoCAD 中 Ctrl+C 复制，或手动选择文件。",
                    "YangTools 提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 解析导入单位
            SelectedUnit = CboUnits.SelectedIndex switch
            {
                0 => ImportUnit.Default,
                1 => ImportUnit.Millimeter,
                2 => ImportUnit.Centimeter,
                3 => ImportUnit.Decimeter,
                4 => ImportUnit.Meter,
                5 => ImportUnit.Inch,
                6 => ImportUnit.Foot,
                _ => ImportUnit.Millimeter
            };

            // 解析颜色模式
            SelectedColorMode = CboColors.SelectedIndex switch
            {
                0 => ImportColorMode.Preserved,
                1 => ImportColorMode.BlackAndWhite,
                2 => ImportColorMode.Inverted,
                _ => ImportColorMode.Preserved
            };

            // 解析定位方式
            SelectedPlacement = CboPlacement.SelectedIndex switch
            {
                0 => ImportPlacement.Origin,
                1 => ImportPlacement.Centered,
                _ => ImportPlacement.Origin
            };

            // 解析是否仅当前视口复制
            SelectedThisViewOnly = ChkThisViewOnly.IsChecked == true;

            DialogResult = true;
            Close();
        }

        /// <summary>
        /// 点击取消
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// 重新检测按钮
        /// </summary>
        private void BtnRedetect_Click(object sender, RoutedEventArgs e)
        {
            DetectCadClipboard();
        }

        /// <summary>
        /// 窗口拖拽
        /// </summary>
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
    }
}
