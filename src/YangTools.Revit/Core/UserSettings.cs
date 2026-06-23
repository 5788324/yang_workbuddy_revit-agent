using System;
using System.IO;

namespace YangTools.Revit.Core
{
    public class UserSettings
    {
        public string EntityGeneratorTemplatePath { get; set; } = string.Empty;

        private static string SettingsFilePath
        {
            get
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string dir = Path.Combine(appData, "YangTools");
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                return Path.Combine(dir, "settings.txt");
            }
        }

        public static UserSettings Load()
        {
            var settings = new UserSettings();
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string[] lines = File.ReadAllLines(SettingsFilePath);
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("EntityGeneratorTemplatePath="))
                        {
                            settings.EntityGeneratorTemplatePath = line.Substring("EntityGeneratorTemplatePath=".Length);
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors and return default
            }
            return settings;
        }

        public void Save()
        {
            try
            {
                string content = $"EntityGeneratorTemplatePath={EntityGeneratorTemplatePath}";
                File.WriteAllText(SettingsFilePath, content);
            }
            catch
            {
                // Ignore errors
            }
        }
    }
}
