using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using YangTools.Revit.Core;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace YangTools.Revit.UI
{
    public abstract class ChatSegment { }
    public class TextSegment : ChatSegment { public string Content { get; set; } }
    public class CodeSegment : ChatSegment { public string Code { get; set; } }

    public class ChatMessage
    {
        public string Message { get; set; }
        public ObservableCollection<ChatSegment> Segments { get; set; } = new ObservableCollection<ChatSegment>();
        public bool IsUser { get; set; }
        public Style BubbleStyle { get; set; }
        public Visibility CopyButtonVisibility => IsUser ? Visibility.Collapsed : Visibility.Visible;
    }

    public class SystemPromptItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "新预设";
        public string Content { get; set; } = "";
    }

    public class CopilotSettings
    {
        public string McpServerUrl { get; set; } = "http://localhost:8081/message";
        public string ApiKey { get; set; } = "";
        public string BaseUrl { get; set; } = "https://api.deepseek.com/chat/completions";
        public string ModelName { get; set; } = "deepseek-chat";
        
        public List<SystemPromptItem> SystemPrompts { get; set; } = new List<SystemPromptItem>
        {
            new SystemPromptItem { Name = "通用助手", Content = "你是一个精通 Revit API 和 IronPython 的可爱AI桌面宠物兼顶级工程师。如果用户要求建模或修改模型，你只输出包裹在 ```python ... ``` 块内的 Python 代码，并且代码中不要包含任何中文字符（包含注释）以防止乱码。运行环境已经为你准备好了 doc, uidoc, app, uiapp 全局变量，你可以直接使用。除了代码外，你可以用一两句软萌的话跟用户互动。" }
        };
        public string CurrentPromptId { get; set; } = "";

        public static bool IsMoyuProfile = false;

        public static CopilotSettings Load()
        {
            string fileName = IsMoyuProfile ? "moyu_settings.json" : "copilot_settings.json";
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YangTools", fileName);
            if (File.Exists(path))
            {
                try { return Newtonsoft.Json.JsonConvert.DeserializeObject<CopilotSettings>(File.ReadAllText(path)); }
                catch { }
            }
            return new CopilotSettings();
        }

        public void Save()
        {
            string fileName = IsMoyuProfile ? "moyu_settings.json" : "copilot_settings.json";
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YangTools");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, fileName);
            File.WriteAllText(path, Newtonsoft.Json.JsonConvert.SerializeObject(this));
        }
    }

    public partial class CopilotPanel : Page, IDockablePaneProvider
    {
        private ObservableCollection<ChatMessage> _messages = new ObservableCollection<ChatMessage>();
        private CopilotSettings _settings;
        private CopilotHistoryManager _historyManager;
        private CopilotConversation _currentConversation;

        public CopilotPanel()
        {
            InitializeComponent();
            ChatHistoryList.ItemsSource = _messages;
            
            _settings = CopilotSettings.Load();
            _historyManager = new CopilotHistoryManager();
            HistoryListBox.ItemsSource = _historyManager.Conversations;

            // 监听全局快捷键
            NativeHookManager.MoyuHotkeyTriggered += NativeHookManager_MoyuHotkeyTriggered;
            this.Loaded += CopilotPanel_Loaded;
            this.Unloaded += CopilotPanel_Unloaded;

            // 初始化对话
            if (_historyManager.Conversations.Count > 0)
            {
                SwitchToConversation(_historyManager.Conversations.Last());
            }
            else
            {
                CreateNewConversation();
            }
        }

        private void CopilotPanel_Loaded(object sender, RoutedEventArgs e)
        {
            NativeHookManager.StartHook();
        }

        private void CopilotPanel_Unloaded(object sender, RoutedEventArgs e)
        {
            NativeHookManager.StopHook();
        }

        private bool _isMoyuMode = false;
        private void NativeHookManager_MoyuHotkeyTriggered(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                // Physical Key check (License)
                var pluginDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                var moyuDllPath = Path.Combine(pluginDir ?? "", "YangTools.Moyu.dll");
                
                if (!File.Exists(moyuDllPath) && !_isMoyuMode)
                {
                    // No key, don't enter Moyu mode.
                    return;
                }

                _isMoyuMode = !_isMoyuMode;
                
                // Toggle Profile Configuration
                CopilotSettings.IsMoyuProfile = _isMoyuMode;
                _settings = CopilotSettings.Load();
                _historyManager = new CopilotHistoryManager(_isMoyuMode);
                HistoryListBox.ItemsSource = _historyManager.Conversations;
                
                if (_historyManager.Conversations.Count > 0)
                {
                    SwitchToConversation(_historyManager.Conversations.Last());
                }
                else
                {
                    CreateNewConversation();
                }

                if (_isMoyuMode)
                {
                    ChatUIContainer.Visibility = Visibility.Collapsed;
                    MoyuUIContainer.Visibility = Visibility.Visible;
                }
                else
                {
                    ChatUIContainer.Visibility = Visibility.Visible;
                    MoyuUIContainer.Visibility = Visibility.Collapsed;
                }
            });
        }


        public void SetupDockablePane(DockablePaneProviderData data)
        {
            data.FrameworkElement = this;
            data.InitialState = new DockablePaneState
            {
                DockPosition = DockPosition.Right
            };
        }

        // ---------- 历史对话管理逻辑 ----------

        private void HistoryBtn_Click(object sender, RoutedEventArgs e)
        {
            HistoryListBox.Items.Refresh();
            SettingsOverlay.Visibility = Visibility.Collapsed;
            HistoryOverlay.Visibility = Visibility.Visible;
        }

        private void CancelHistory_Click(object sender, RoutedEventArgs e)
        {
            HistoryOverlay.Visibility = Visibility.Collapsed;
        }

        private void NewConversation_Click(object sender, RoutedEventArgs e)
        {
            CreateNewConversation();
            HistoryOverlay.Visibility = Visibility.Collapsed;
        }

        // ... 省略 ... (但需要保持中间代码不变, 因为我用的是 replace_file_content)

        private void CreateNewConversation()
        {
            var convo = new CopilotConversation { Name = "新建对话 " + DateTime.Now.ToString("MM-dd HH:mm") };
            
            var currentPrompt = _settings.SystemPrompts?.FirstOrDefault(p => p.Id == _settings.CurrentPromptId)?.Content ?? "";
            convo.ChatHistory.Add(new ApiMessage { role = "system", content = currentPrompt });
            
            _historyManager.Conversations.Add(convo);
            _historyManager.Save();
            
            SwitchToConversation(convo);
        }

        private void SwitchToConversation(CopilotConversation convo)
        {
            _currentConversation = convo;
            _messages.Clear();

            // 重新渲染当前对话的所有 UI 气泡
            if (convo.Messages.Count == 0)
            {
                AddMessage("主人您好！我是您的智能桌宠向导！\n初次使用请点击右上角的“⚙设置”，配置您的 API Key 噢！", false, false);
            }
            else
            {
                foreach (var msg in convo.Messages)
                {
                    // 复用 AddMessage 逻辑进行渲染但不保存到磁盘
                    AddMessage(msg.Message, msg.IsUser, false);
                }
                ChatScrollViewer.ScrollToBottom();
            }
            HistoryListBox.Items.Refresh();
        }

        private void SelectConversation_Click(object sender, MouseButtonEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is CopilotConversation convo)
            {
                SwitchToConversation(convo);
                HistoryOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void RenameConversation_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is CopilotConversation convo)
            {
                var dialog = new Window
                {
                    Title = "重命名对话",
                    Width = 300,
                    Height = 150,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    ResizeMode = ResizeMode.NoResize
                };
                var stack = new StackPanel { Margin = new Thickness(10) };
                stack.Children.Add(new TextBlock { Text = "请输入新的对话名称：", Margin = new Thickness(0, 0, 0, 10) });
                var txt = new System.Windows.Controls.TextBox { Text = convo.Name, Padding = new Thickness(5) };
                stack.Children.Add(txt);
                var btn = new Button { Content = "确定", Width = 80, Margin = new Thickness(0, 15, 0, 0), Padding = new Thickness(5) };
                btn.Click += (s, ev) => { dialog.DialogResult = true; };
                stack.Children.Add(btn);
                dialog.Content = stack;
                
                if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(txt.Text))
                {
                    convo.Name = txt.Text.Trim();
                    _historyManager.Save();
                    HistoryListBox.Items.Refresh();
                }
            }
        }

        private void DeleteConversation_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is CopilotConversation convo)
            {
                if (MessageBox.Show($"确定要删除对话 '{convo.Name}' 吗？", "删除确认", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    _historyManager.Conversations.Remove(convo);
                    _historyManager.Save();
                    HistoryListBox.Items.Refresh();

                    if (_currentConversation == convo)
                    {
                        if (_historyManager.Conversations.Count > 0)
                            SwitchToConversation(_historyManager.Conversations.Last());
                        else
                            CreateNewConversation();
                    }
                }
            }
        }

        // ---------- 设置逻辑 ----------

        private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            McpUrlBox.Text = _settings.McpServerUrl;
            BaseUrlBox.Text = _settings.BaseUrl;
            ApiKeyBox.Password = _settings.ApiKey;
            ModelNameBox.Text = _settings.ModelName;
            
            RefreshSystemPromptsUI();

            HistoryOverlay.Visibility = Visibility.Collapsed;
            SettingsOverlay.Visibility = Visibility.Visible;
        }

        private void RefreshSystemPromptsUI()
        {
            if (_settings.SystemPrompts == null || _settings.SystemPrompts.Count == 0)
            {
                _settings.SystemPrompts = new List<SystemPromptItem>
                {
                    new SystemPromptItem { Name = "通用助手", Content = "你是一个精通 Revit API 和 IronPython 的可爱AI桌面宠物兼顶级工程师。如果用户要求建模或修改模型，你只输出包裹在 ```python ... ``` 块内的 Python 代码，并且代码中不要包含任何中文字符（包含注释）以防止乱码。运行环境已经为你准备好了 doc, uidoc, app, uiapp 全局变量，你可以直接使用。除了代码外，你可以用一两句软萌的话跟用户互动。" }
                };
            }
            
            SystemPromptComboBox.ItemsSource = null;
            SystemPromptComboBox.ItemsSource = _settings.SystemPrompts;
            
            var selected = _settings.SystemPrompts.FirstOrDefault(p => p.Id == _settings.CurrentPromptId) ?? _settings.SystemPrompts.First();
            SystemPromptComboBox.SelectedItem = selected;
        }

        private bool _isUpdatingPromptUI = false;

        private void SystemPromptComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingPromptUI) return;
            if (SystemPromptComboBox.SelectedItem is SystemPromptItem item)
            {
                _isUpdatingPromptUI = true;
                _settings.CurrentPromptId = item.Id;
                PromptNameBox.Text = item.Name;
                PromptBox.Text = item.Content;
                _isUpdatingPromptUI = false;
            }
        }

        private void PromptNameBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingPromptUI) return;
            if (SystemPromptComboBox.SelectedItem is SystemPromptItem item)
            {
                item.Name = PromptNameBox.Text;
                // Force UI refresh of ComboBox display
                SystemPromptComboBox.Items.Refresh();
            }
        }

        private void PromptBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingPromptUI) return;
            if (SystemPromptComboBox.SelectedItem is SystemPromptItem item)
            {
                item.Content = PromptBox.Text;
            }
        }

        private void AddPrompt_Click(object sender, RoutedEventArgs e)
        {
            var newItem = new SystemPromptItem { Name = "新预设 " + (_settings.SystemPrompts.Count + 1), Content = "请在此输入人设内容..." };
            _settings.SystemPrompts.Add(newItem);
            _settings.CurrentPromptId = newItem.Id;
            RefreshSystemPromptsUI();
        }

        private void DeletePrompt_Click(object sender, RoutedEventArgs e)
        {
            if (SystemPromptComboBox.SelectedItem is SystemPromptItem item)
            {
                if (_settings.SystemPrompts.Count <= 1)
                {
                    MessageBox.Show("必须保留至少一个预设！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                _settings.SystemPrompts.Remove(item);
                _settings.CurrentPromptId = _settings.SystemPrompts.First().Id;
                RefreshSystemPromptsUI();
            }
        }

        private void CancelSettings_Click(object sender, RoutedEventArgs e)
        {
            SettingsOverlay.Visibility = Visibility.Collapsed;
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            _settings.McpServerUrl = McpUrlBox.Text.Trim();
            _settings.BaseUrl = BaseUrlBox.Text.Trim();
            _settings.ApiKey = ApiKeyBox.Password.Trim();
            _settings.ModelName = ModelNameBox.Text?.Trim() ?? "";
            _settings.Save();
            
            var currentPrompt = _settings.SystemPrompts.FirstOrDefault(p => p.Id == _settings.CurrentPromptId)?.Content ?? "";
            
            // 更新当前对话的 System Prompt (如果有)
            if (_currentConversation != null && _currentConversation.ChatHistory.Count > 0 && _currentConversation.ChatHistory[0].role == "system")
            {
                _currentConversation.ChatHistory[0].content = currentPrompt;
                _historyManager.Save();
            }

            SettingsOverlay.Visibility = Visibility.Collapsed;
            AddMessage("✅ 设置已保存！", false, false);
        }

        private async void GetModels_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn != null) btn.IsEnabled = false;

            try
            {
                string baseUrl = BaseUrlBox.Text.Trim();
                string apiKey = ApiKeyBox.Password.Trim();
                
                if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(apiKey))
                {
                    MessageBox.Show("请先填写 API Base URL 和 API Key！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Append /models if not already ending in something
                string url = baseUrl.EndsWith("/chat/completions") 
                    ? baseUrl.Replace("/chat/completions", "/models") 
                    : baseUrl.TrimEnd('/') + "/models";

                using var httpClient = new System.Net.Http.HttpClient();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                
                var response = await httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var jsonStr = await response.Content.ReadAsStringAsync();
                    var jObj = JObject.Parse(jsonStr);
                    var data = jObj["data"] as JArray;
                    if (data != null)
                    {
                        var models = data.Select(x => x["id"]?.ToString()).Where(x => !string.IsNullOrEmpty(x)).ToList();
                        ModelNameBox.ItemsSource = models;
                        if (models.Count > 0) ModelNameBox.SelectedIndex = 0;
                        MessageBox.Show($"成功获取到 {models.Count} 个模型！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    MessageBox.Show($"获取模型失败: {response.StatusCode}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"网络错误：{ex.Message}", "获取模型失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (btn != null) btn.IsEnabled = true;
            }
        }

        private async void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            string apiKey = ApiKeyBox.Password.Trim();
            string baseUrl = BaseUrlBox.Text.Trim();
            string modelName = ModelNameBox.Text.Trim();

            if (string.IsNullOrEmpty(apiKey))
            {
                MessageBox.Show("API Key 不能为空！", "测试失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var button = sender as Button;
            if (button != null) button.Content = "测试中...";

            try
            {
                var client = new DeepSeekClient(apiKey, baseUrl, modelName);
                var messages = new List<object>
                {
                    new { role = "user", content = "Ping. Reply 'Pong' if you receive this." }
                };

                string reply = await client.SendMessageAsync(messages);

                if (reply.StartsWith("[API Error]") || reply.StartsWith("[Exception]"))
                {
                    MessageBox.Show($"连接失败！\n\n详情：{reply}", "测试失败", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show("连接成功！API 正常响应。", "测试成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"网络错误：{ex.Message}", "测试失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (button != null) button.Content = "测试连接";
            }
        }

        // ---------- 聊天核心逻辑 ----------

        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentConversation == null || _currentConversation.ChatHistory.Count <= 1)
            {
                MessageBox.Show("当前没有可以撤销的对话。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // The list could end with assistant or user message. Usually it's pairs.
            // Pop the last assistant message and the last user message.
            if (_currentConversation.ChatHistory.Count > 1)
            {
                _currentConversation.ChatHistory.RemoveAt(_currentConversation.ChatHistory.Count - 1); // remove assistant
            }
            if (_currentConversation.ChatHistory.Count > 1 && _currentConversation.ChatHistory.Last().role == "user")
            {
                _currentConversation.ChatHistory.RemoveAt(_currentConversation.ChatHistory.Count - 1); // remove user
            }

            // Remove from Messages as well
            if (_currentConversation.Messages.Count > 0)
            {
                _currentConversation.Messages.RemoveAt(_currentConversation.Messages.Count - 1);
            }
            if (_currentConversation.Messages.Count > 0 && _currentConversation.Messages.Last().IsUser)
            {
                _currentConversation.Messages.RemoveAt(_currentConversation.Messages.Count - 1);
            }

            _historyManager.Save();
            
            // UI refresh
            _messages.Clear();
            if (_currentConversation.Messages.Count == 0)
            {
                AddMessage("主人您好！我是您的智能桌宠向导！\n初次使用请点击右上角的“⚙设置”，配置您的 API Key 噢！", false, false);
            }
            else
            {
                foreach (var m in _currentConversation.Messages)
                {
                    AddMessage(m.Message, m.IsUser, false);
                }
            }
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            SendMessageAsync(InputTextBox.Text, false);
        }

        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
            {
                e.Handled = true;
                SendMessageAsync(InputTextBox.Text, false);
            }
        }

        private void MoyuSendButton_Click(object sender, RoutedEventArgs e)
        {
            SendMessageAsync(MoyuInputTextBox.Text, true);
        }

        private void MoyuInputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
            {
                e.Handled = true;
                SendMessageAsync(MoyuInputTextBox.Text, true);
            }
        }

        private async void SendMessageAsync(string text, bool isMoyu)
        {
            text = text?.Trim();
            if (string.IsNullOrEmpty(text)) return;
            
            if (string.IsNullOrEmpty(_settings.McpServerUrl))
            {
                MessageBox.Show("请先配置 MCP Server URL！", "未配置 MCP", MessageBoxButton.OK, MessageBoxImage.Warning);
                SettingsBtn_Click(null, null);
                return;
            }

            if (isMoyu)
            {
                MoyuInputTextBox.Text = string.Empty;
                MoyuOutputTextBox.Text = "思考中...";
            }
            else
            {
                InputTextBox.Text = string.Empty;
                SendButton.IsEnabled = false;
                AddMessage(text, true, true);
            }

            _currentConversation.ChatHistory.Add(new ApiMessage { role = "user", content = text });
            _historyManager.Save();

            string context = await GetSelectionContextAsync();
            string finalPrompt = string.IsNullOrWhiteSpace(context) ? text : $"[当前选中图元上下文: {context}]\n\n{text}";

            string reply = "";
            try
            {
                using var httpClient = new System.Net.Http.HttpClient();
                var currentPrompt = _settings.SystemPrompts?.FirstOrDefault(p => p.Id == _settings.CurrentPromptId)?.Content ?? "";
                var payload = new 
                { 
                    prompt = finalPrompt, 
                    history = _currentConversation.ChatHistory.Take(_currentConversation.ChatHistory.Count - 1),
                    systemPrompt = currentPrompt
                };
                
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
                var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");

                using var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, _settings.McpServerUrl);
                request.Content = content;

                using var response = await httpClient.SendAsync(request, System.Net.Http.HttpCompletionOption.ResponseHeadersRead);
                using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new System.IO.StreamReader(stream);

                var sb = new System.Text.StringBuilder();

                // 简单的 SSE 模拟接收 (如果服务端支持 SSE，则逐步读取)
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (!string.IsNullOrEmpty(line) && line.StartsWith("data: "))
                    {
                        var data = line.Substring(6);
                        // 处理转义换行符等 (这里为了简化，直接 append)
                        // 实际开发中服务端可能传输 JSON {"content": "..."}
                        if (data == "[DONE]") break;
                        
                        try
                        {
                            var tokenObj = JObject.Parse(data);
                            if (tokenObj["content"] != null)
                            {
                                sb.Append(tokenObj["content"].ToString());
                            }
                        }
                        catch
                        {
                            // fallback 如果不是 JSON
                            sb.Append(data);
                        }

                        if (isMoyu)
                        {
                            Dispatcher.Invoke(() => MoyuOutputTextBox.Text = sb.ToString());
                        }
                    }
                    else if (!string.IsNullOrEmpty(line) && !line.StartsWith("event:") && !line.StartsWith("id:") && !line.StartsWith(":"))
                    {
                        // 兼容非 SSE 的纯文本或 JSON 响应
                        sb.AppendLine(line);
                    }
                }
                
                reply = sb.ToString();
                if (string.IsNullOrWhiteSpace(reply))
                {
                    reply = "未能从 MCP Server 获取到有效回复。";
                }
            }
            catch (Exception ex)
            {
                reply = $"[Exception] MCP 连接失败: {ex.Message}";
            }
            
            _currentConversation.ChatHistory.Add(new ApiMessage { role = "assistant", content = reply });
            _historyManager.Save();

            if (isMoyu)
            {
                Dispatcher.Invoke(() => MoyuOutputTextBox.Text = reply);
            }
            else
            {
                SendButton.IsEnabled = true;
                AddMessage(reply, false, true);
            }

            ExtractAndExecutePython(reply);
        }

        private Task<string> GetSelectionContextAsync()
        {
            var tcs = new TaskCompletionSource<string>();
            App.GlobalReadHandler.SetAction(app =>
            {
                try
                {
                    var uidoc = app.ActiveUIDocument;
                    if (uidoc == null) { tcs.SetResult(""); return; }

                    var ids = uidoc.Selection.GetElementIds();
                    if (ids.Count == 0) { tcs.SetResult(""); return; }

                    var doc = uidoc.Document;
                    var items = new List<object>();
                    foreach(var id in ids)
                    {
                        var elem = doc.GetElement(id);
                        if (elem != null)
                        {
                            items.Add(new { 
                                Id = elem.Id.GetIdValue(), 
                                Category = elem.Category?.Name ?? "Unknown", 
                                Name = elem.Name 
                            });
                        }
                    }
                    string json = Newtonsoft.Json.JsonConvert.SerializeObject(items);
                    tcs.SetResult(json);
                }
                catch
                {
                    tcs.SetResult("");
                }
            });
            App.GlobalReadEvent.Raise();
            return tcs.Task;
        }

        private void ExtractAndExecutePython(string reply)
        {
            var match = Regex.Match(reply, @"```python(.*?)```", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string code = match.Groups[1].Value.Trim();
                var payload = new JObject();
                payload["command"] = "eval_python";
                payload["args"] = new JObject();
                payload["args"]["script"] = code;

                var tcs = new System.Threading.Tasks.TaskCompletionSource<string>();
                App.GlobalMcpHandler.EnqueueCommand(payload, tcs);
                App.GlobalMcpEvent.Raise();
            }
        }

        private void AddMessage(string text, bool isUser, bool saveToDisk)
        {
            var style = isUser ? (Style)FindResource("UserBubbleStyle") : (Style)FindResource("AiBubbleStyle");
            var msgObj = new ChatMessage { Message = text, IsUser = isUser, BubbleStyle = style };
            
            // 解析代码块和报错折叠
            if (!isUser && (text.StartsWith("[API Error]") || text.StartsWith("[Exception]")))
            {
                msgObj.Segments.Add(new CodeSegment { Code = text });
            }
            else if (!isUser && text.Contains("```"))
            {
                var regex = new Regex(@"```[a-zA-Z]*\n?(.*?)```", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                var matches = regex.Matches(text);
                int lastIndex = 0;
                
                foreach (Match m in matches)
                {
                    if (m.Index > lastIndex)
                    {
                        string preText = text.Substring(lastIndex, m.Index - lastIndex).Trim();
                        if (!string.IsNullOrEmpty(preText))
                            msgObj.Segments.Add(new TextSegment { Content = preText });
                    }
                    msgObj.Segments.Add(new CodeSegment { Code = m.Groups[1].Value.Trim() });
                    lastIndex = m.Index + m.Length;
                }
                if (lastIndex < text.Length)
                {
                    string postText = text.Substring(lastIndex).Trim();
                    if (!string.IsNullOrEmpty(postText))
                        msgObj.Segments.Add(new TextSegment { Content = postText });
                }
            }
            else
            {
                msgObj.Segments.Add(new TextSegment { Content = text });
            }

            _messages.Add(msgObj);
            
            if (saveToDisk && _currentConversation != null)
            {
                _currentConversation.Messages.Add(new ChatMessageData { Message = text, IsUser = isUser });
                _historyManager.Save();
            }
            
            ChatScrollViewer.ScrollToBottom();
        }

        private void CopyMessage_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is ChatMessage msg)
            {
                try
                {
                    Clipboard.SetText(msg.Message);
                    var btn = sender as Button;
                    if (btn != null)
                    {
                        btn.Content = "✅ 已复制";
                        Task.Delay(1500).ContinueWith(_ => Dispatcher.Invoke(() => btn.Content = "📋 复制"));
                    }
                }
                catch { }
            }
        }
    }
}
