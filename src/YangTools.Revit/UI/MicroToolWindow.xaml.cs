using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using YangTools.Revit.Commands;

namespace YangTools.Revit.UI
{
    public partial class MicroToolWindow : Window
    {
        public string SelectedDllPath { get; private set; }

        public MicroToolWindow()
        {
            InitializeComponent();
            ThemeHelper.ApplyToWindow(this);
            LoadProjects();
        }

        private void LoadProjects()
        {
            var projectItems = MicroToolEngine.GetProjects();
            ProjectsListBox.ItemsSource = projectItems;
        }

        private void RefreshTools()
        {
            if (ProjectsListBox.SelectedItem is ProjectItem selectedProject)
            {
                ProjectPathText.Text = $"项目路径: {selectedProject.FullPath}";
                if (Directory.Exists(selectedProject.FullPath))
                {
                    var dllFiles = Directory.GetFiles(selectedProject.FullPath, "*.dll");
                    var toolItems = dllFiles.Select(f => new ToolItem { Name = Path.GetFileName(f), FullPath = f }).ToList();
                    ToolsListBox.ItemsSource = toolItems;
                }
                else
                {
                    ToolsListBox.ItemsSource = null;
                }
            }
            else
            {
                ProjectPathText.Text = "项目路径: ";
                ToolsListBox.ItemsSource = null;
            }
        }

        private void ProjectsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshTools();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void RunButton_Click(object sender, RoutedEventArgs e)
        {
            ExecuteSelectedTool();
        }

        private void ToolsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ExecuteSelectedTool();
        }

        private void ExecuteSelectedTool()
        {
            if (ToolsListBox.SelectedItem is ToolItem selectedTool)
            {
                SelectedDllPath = selectedTool.FullPath;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("请先选择一个微工具 DLL。 (Please select a Micro Tool DLL first.)", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void NewProjectButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new NewProjectDialog { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                MicroToolEngine.AddProjectConfig(dialog.ProjectName, dialog.FolderPath);
                LoadProjects();
            }
        }

        private void DeleteProjectButton_Click(object sender, RoutedEventArgs e)
        {
            if (ProjectsListBox.SelectedItem is ProjectItem selectedProject)
            {
                var result = MessageBox.Show($"确定要从列表中移除项目 '{selectedProject.Name}' 吗？\n\n这不会自动删除文件夹中的文件，但我将为您打开该文件夹以供确认删除。",
                                             "删除项目确认", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    MicroToolEngine.RemoveProjectConfig(selectedProject.Name);

                    // If it is in the default MicroProjects folder, we should inform user
                    if (Directory.Exists(selectedProject.FullPath))
                    {
                        System.Diagnostics.Process.Start("explorer.exe", selectedProject.FullPath);
                    }
                    
                    LoadProjects();
                }
            }
            else
            {
                MessageBox.Show("请先在左侧选择要删除的项目。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void DeletePluginButton_Click(object sender, RoutedEventArgs e)
        {
            if (ToolsListBox.SelectedItem is ToolItem selectedTool)
            {
                var result = MessageBox.Show($"确定要删除插件 '{selectedTool.Name}' 及其相关文件吗？\n删除后将无法恢复！", 
                                             "删除插件确认", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        string dir = Path.GetDirectoryName(selectedTool.FullPath);
                        string fileNameWithoutExt = Path.GetFileNameWithoutExtension(selectedTool.FullPath);
                        if (dir != null)
                        {
                            var filesToDelete = Directory.GetFiles(dir, fileNameWithoutExt + ".*");
                            foreach (var file in filesToDelete)
                            {
                                File.Delete(file);
                            }
                        }
                        RefreshTools();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"删除文件时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("请先在右侧选择要删除的微工具。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    public class ProjectItem
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
    }

    public class ToolItem
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
    }
}
