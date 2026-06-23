using System.Windows;
using System.Windows.Input;

namespace YangTools.Revit.UI
{
    /// <summary>
    /// DimTextOverrideWindow.xaml 的交互逻辑
    /// 标注文本替换对话框，允许用户输入自定义文本或清除覆盖
    /// </summary>
    public partial class DimTextOverrideWindow : Window
    {
        /// <summary>
        /// 用户输入的替换文本
        /// </summary>
        public string OverrideText { get; set; }

        /// <summary>
        /// 是否清除覆盖文本（恢复原始数值显示）
        /// </summary>
        public bool IsClear { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="selectedCount">已选中的标注数量</param>
        public DimTextOverrideWindow(int selectedCount)
        {
            InitializeComponent();
            ThemeHelper.ApplyToWindow(this);

            OverrideText = string.Empty;
            IsClear = false;

            // 显示已选标注数量
            TxtSelectedCount.Text = $"已选中 {selectedCount} 个标注";
        }

        /// <summary>
        /// 勾选"清除覆盖文本"时，禁用文本输入框
        /// </summary>
        private void ChkClear_Checked(object sender, RoutedEventArgs e)
        {
            TxtOverride.IsEnabled = false;
            TxtOverride.Text = string.Empty;
        }

        /// <summary>
        /// 取消勾选"清除覆盖文本"时，重新启用文本输入框
        /// </summary>
        private void ChkClear_Unchecked(object sender, RoutedEventArgs e)
        {
            TxtOverride.IsEnabled = true;
            TxtOverride.Focus();
        }

        /// <summary>
        /// 确认按钮点击事件
        /// </summary>
        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            OverrideText = TxtOverride.Text;
            IsClear = ChkClear.IsChecked == true;
            DialogResult = true;
        }

        /// <summary>
        /// 取消/关闭按钮点击事件
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        /// <summary>
        /// 允许无边框窗口通过鼠标左键按住拖动
        /// </summary>
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }
    }
}
