using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using MediaColor = System.Windows.Media.Color;

namespace YangTools.Revit.UI
{
    public partial class AssistantWindow : Window
    {
        public AssistantWindow()
        {
            InitializeComponent();
            ThemeHelper.ApplyToWindow(this);
            SetGreeting();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                IntPtr revitNativeWindowHandle = Process.GetCurrentProcess().MainWindowHandle;
                WindowInteropHelper windowInteropHelper = new WindowInteropHelper(this)
                {
                    Owner = revitNativeWindowHandle
                };
            }
            catch
            {
                // Ignored
            }
        }

        private void SetGreeting()
        {
            int hour = DateTime.Now.Hour;
            if (hour >= 6 && hour < 12)
            {
                TxtGreeting.Text = "早上好！新的一天充满活力。";
            }
            else if (hour >= 12 && hour < 18)
            {
                TxtGreeting.Text = "下午好！祝您工作顺利。";
            }
            else
            {
                TxtGreeting.Text = "晚上好！注意休息。";
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }
    }
}
