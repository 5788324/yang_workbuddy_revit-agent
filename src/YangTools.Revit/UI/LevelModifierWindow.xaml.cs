using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Autodesk.Revit.DB;

namespace YangTools.Revit.UI
{
    public partial class LevelModifierWindow : Window
    {
        public Level SelectedLevel { get; private set; }
        public bool IsOk { get; private set; }

        public LevelModifierWindow(List<Level> levels)
        {
            InitializeComponent();
            ThemeHelper.ApplyToWindow(this);
            LevelComboBox.ItemsSource = levels;
            if (levels.Count > 0)
            {
                LevelComboBox.SelectedIndex = 0;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Center to Revit Main Window
            try
            {
                var process = System.Diagnostics.Process.GetCurrentProcess();
                var helper = new WindowInteropHelper(this);
                helper.Owner = process.MainWindowHandle;
            }
            catch
            {
                // Ignore
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            IsOk = false;
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            IsOk = false;
            this.Close();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (LevelComboBox.SelectedItem is Level level)
            {
                SelectedLevel = level;
                IsOk = true;
                this.Close();
            }
            else
            {
                MessageBox.Show("请先选择一个标高。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
