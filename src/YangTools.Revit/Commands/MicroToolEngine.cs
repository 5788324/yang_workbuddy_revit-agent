using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace YangTools.Revit.Commands
{
    public class MicroToolEngine
    {
        public static string GetMicroProjectsFolder()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YangTools", "MicroProjects");
        }

        private static string GetConfigFilePath()
        {
            return Path.Combine(GetMicroProjectsFolder(), "projects.txt");
        }

        public static System.Collections.Generic.List<YangTools.Revit.UI.ProjectItem> GetProjects()
        {
            var list = new System.Collections.Generic.List<YangTools.Revit.UI.ProjectItem>();
            list.Add(new YangTools.Revit.UI.ProjectItem { Name = "默认仓库", FullPath = GetMicroProjectsFolder() });
            list.Add(new YangTools.Revit.UI.ProjectItem { Name = "当前项目(92053)", FullPath = @"E:\Yang\92053\92053" });
            
            var configPath = GetConfigFilePath();
            if (File.Exists(configPath))
            {
                var lines = File.ReadAllLines(configPath);
                foreach (var line in lines)
                {
                    var parts = line.Split('|');
                    if (parts.Length == 2)
                    {
                        if (!list.Any(p => p.Name == parts[0]))
                        {
                            list.Add(new YangTools.Revit.UI.ProjectItem { Name = parts[0], FullPath = parts[1] });
                        }
                    }
                }
            }
            return list;
        }

        public static void AddProjectConfig(string name, string path)
        {
            var configPath = GetConfigFilePath();
            var dir = GetMicroProjectsFolder();
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.AppendAllText(configPath, $"{name}|{path}\n");
        }

        public static void RemoveProjectConfig(string name)
        {
            var configPath = GetConfigFilePath();
            if (File.Exists(configPath))
            {
                var lines = File.ReadAllLines(configPath).Where(l => !l.StartsWith(name + "|")).ToArray();
                File.WriteAllLines(configPath, lines);
            }
        }

        public static Result ExecuteMicroTool(string dllPath, ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // 修复 WPF 加载失败: 使用 Shadow Copy 取代单纯的 byte[] Load
                string tempDir = Path.Combine(Path.GetTempPath(), "YangTools_Micro");
                if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);
                string tempFile = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + "_" + Path.GetFileName(dllPath));
                
                byte[] bytes = File.ReadAllBytes(dllPath);
                File.WriteAllBytes(tempFile, bytes);
                Assembly assembly = Assembly.LoadFrom(tempFile);
                
                var commandType = assembly.GetTypes()
                    .FirstOrDefault(t => typeof(IExternalCommand).IsAssignableFrom(t) && !t.IsAbstract);

                if (commandType != null)
                {
                    IExternalCommand command = (IExternalCommand)Activator.CreateInstance(commandType);
                    return command.Execute(commandData, ref message, elements);
                }
                else
                {
                    TaskDialog.Show("错误", "No class implementing IExternalCommand found in the DLL.");
                    return Result.Failed;
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("错误", $"Micro Tool Execution Error:\n{ex.ToString()}");
                return Result.Failed;
            }
        }
    }
}
