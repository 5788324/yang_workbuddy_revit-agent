using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace YangTools.Revit.UI
{
    public class ApiMessage
    {
        public string role { get; set; }
        public string content { get; set; }
    }

    public class ChatMessageData
    {
        public string Message { get; set; }
        public bool IsUser { get; set; }
    }

    public class CopilotConversation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "新对话";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public List<ChatMessageData> Messages { get; set; } = new List<ChatMessageData>();
        public List<ApiMessage> ChatHistory { get; set; } = new List<ApiMessage>();
    }

    public class CopilotHistoryManager
    {
        private readonly string _filePath;
        public List<CopilotConversation> Conversations { get; set; } = new List<CopilotConversation>();

        public CopilotHistoryManager(bool isMoyu = false)
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YangTools");
            Directory.CreateDirectory(dir);
            string fileName = isMoyu ? "moyu_history.json" : "copilot_history.json";
            _filePath = Path.Combine(dir, fileName);
            Load();
        }

        public void Load()
        {
            if (File.Exists(_filePath))
            {
                try
                {
                    string json = File.ReadAllText(_filePath);
                    var data = JsonConvert.DeserializeObject<List<CopilotConversation>>(json);
                    if (data != null) Conversations = data;
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] CopilotHistoryManager.cs: {0}", ex.Message); }
            }
        }

        public void Save()
        {
            try
            {
                string json = JsonConvert.SerializeObject(Conversations, Formatting.Indented);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] CopilotHistoryManager.cs: {0}", ex.Message); }
        }
    }
}
