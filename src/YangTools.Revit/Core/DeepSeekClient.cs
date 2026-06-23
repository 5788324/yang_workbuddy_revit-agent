using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace YangTools.Revit.Core
{
    public class DeepSeekClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _baseUrl;
        private readonly string _modelName;

        public DeepSeekClient(string apiKey, string baseUrl, string modelName)
        {
            _apiKey = apiKey;
            _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "https://api.deepseek.com/chat/completions" : baseUrl;
            _modelName = string.IsNullOrWhiteSpace(modelName) ? "deepseek-chat" : modelName;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
            _httpClient.Timeout = TimeSpan.FromSeconds(120);
        }

        public async Task<string> SendMessageAsync(List<object> messages)
        {
            var tools = new[]
            {
                new {
                    type = "function",
                    function = new {
                        name = "search_revit_api",
                        description = "搜索 Revit 官方 API 字典，获取 C# 函数签名和用法，绝大部分对象在 DB 命名空间。当你记不清如何操作 Revit 参数、几何或事务时，必须先调用此工具以防止你写错语法。注意：【1】只允许使用纯英文的类名或方法名进行搜索（如 'ViewSheet'，'Wall.Create'），绝对禁止使用中文关键词进行搜索！【2】一旦你查到了需要的方法，必须立刻停止调用工具，不要重复查询相同或相似的词，直接输出最终的 IronPython 代码！",
                        parameters = new {
                            type = "object",
                            properties = new {
                                keyword = new { type = "string", description = "API 的纯英文关键词 (例如 'Extrusion' 或 'LookupParameter')，严禁使用中文！" }
                            },
                            required = new[] { "keyword" }
                        }
                    }
                }
            };

            int maxToolCalls = 20;
            int callCount = 0;

            while (callCount < maxToolCalls)
            {
                callCount++;
                var payload = new
                {
                    model = _modelName,
                    messages = messages,
                    temperature = 0.1,
                    tools = tools
                };

                var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

                try
                {
                    var response = await _httpClient.PostAsync(_baseUrl, content);
                    var responseString = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        return $"[API Error] {response.StatusCode}: {responseString}";
                    }

                    var json = JObject.Parse(responseString);
                    var responseMessage = json["choices"]?[0]?["message"];
                    
                    if (responseMessage == null) return "无法解析 API 响应";

                    var toolCalls = responseMessage["tool_calls"] as JArray;
                    if (toolCalls != null && toolCalls.Count > 0)
                    {
                        // 包含 tool_calls 的原样压入历史
                        messages.Add(responseMessage.ToObject<object>());

                        foreach (var toolCall in toolCalls)
                        {
                            string id = toolCall["id"]?.ToString();
                            string funcName = toolCall["function"]?["name"]?.ToString();
                            string funcArgsStr = toolCall["function"]?["arguments"]?.ToString();
                            
                            string toolResult = "Tool not found.";
                            if (funcName == "search_revit_api")
                            {
                                try
                                {
                                    var args = JObject.Parse(funcArgsStr);
                                    string keyword = args["keyword"]?.ToString() ?? "";
                                    toolResult = RevitApiSearcher.Search(keyword);
                                    
                                    // [Debug] 将搜索日志写入到临时文件夹，用于监控 LLM 的循环行为
                                    try
                                    {
                                        string debugLogPath = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "YangTools", "tool_debug.log");
                                        System.IO.File.AppendAllText(debugLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ToolCall: {keyword}\nResultLength: {toolResult.Length}\n\n");
                                    }
                                    catch { }
                                }
                                catch (Exception ex)
                                {
                                    toolResult = $"Tool execution error: {ex.Message}";
                                }
                            }

                            // 将结果以 tool 角色推入历史
                            messages.Add(new { role = "tool", tool_call_id = id, content = toolResult });
                        }
                        // 循环继续，带上工具的返回结果重新请求模型
                        continue;
                    }

                    // 没有工具调用，直接返回内容
                    return responseMessage["content"]?.ToString() ?? "模型返回空内容";
                }
                catch (Exception ex)
                {
                    return $"[Exception] 请求失败: {ex.Message}";
                }
            }

            return "[Warning] 大模型进入死循环工具调用，已强制中断。";
        }
    }
}
