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
        /// <summary>
        /// 专门用于存放 MicroTool 临时 DLL 的目录。
        /// 启动时清理旧文件，避免 %TEMP% 下临时文件无限堆积。
        /// </summary>
        private static readonly string TempDir = Path.Combine(Path.GetTempPath(), "YangTools_Micro");

        static MicroToolEngine()
        {
            try
            {
                if (Directory.Exists(TempDir))
                {
                    // 清理上次会话遗留的临时 DLL；被占用无法删除的文件跳过。
                    foreach (var f in Directory.EnumerateFiles(TempDir, "*.dll"))
                    {
                        try { File.Delete(f); }
                        catch { /* 文件被占用或无权限，跳过 */ }
                    }
                }
                else
                {
                    Directory.CreateDirectory(TempDir);
                }
            }
            catch
            {
                // 启动清理失败不应阻塞引擎功能
            }
        }

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
            // 以下为自定义项目路径，可根据需要手动修改
            // list.Add(new YangTools.Revit.UI.ProjectItem { Name = "我的项目", FullPath = @"C:\MyProject" });
            
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
                if (!Directory.Exists(TempDir)) Directory.CreateDirectory(TempDir);
                string tempFile = Path.Combine(TempDir, Guid.NewGuid().ToString("N") + "_" + Path.GetFileName(dllPath));

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
