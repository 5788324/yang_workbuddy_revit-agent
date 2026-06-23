using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace YangTools.Revit.Core
{
    /// <summary>
    /// 本地 RevitAPI.xml 检索工具
    /// 提供给 LLM Agent 进行主动的 API 签名和文档查询 (RAG Tool Calling)
    /// </summary>
    public static class RevitApiSearcher
    {
        private static List<ApiMember> _membersCache = null;
        private static readonly object _lock = new object();

        public class ApiMember
        {
            public string Name { get; set; }
            public string Summary { get; set; }
            public string Returns { get; set; }
        }

        private static void EnsureLoaded()
        {
            if (_membersCache != null) return;
            lock (_lock)
            {
                if (_membersCache != null) return;
                
                _membersCache = new List<ApiMember>();
                try
                {
                    // 获取当前 DLL 所在目录
                    string assemblyDir = Path.GetDirectoryName(typeof(RevitApiSearcher).Assembly.Location);
                    string xmlPath = Path.Combine(assemblyDir, "Docs", "RevitAPI.xml");
                    
                    if (!File.Exists(xmlPath))
                    {
                        System.Diagnostics.Debug.WriteLine($"API 文档不存在: {xmlPath}");
                        return;
                    }

                    // 加载 XML 并提取 member
                    XDocument doc = XDocument.Load(xmlPath);
                    var members = doc.Descendants("member");
                    foreach (var member in members)
                    {
                        string name = member.Attribute("name")?.Value;
                        if (string.IsNullOrWhiteSpace(name)) continue;

                        string summary = member.Element("summary")?.Value?.Trim();
                        string returns = member.Element("returns")?.Value?.Trim();

                        _membersCache.Add(new ApiMember
                        {
                            Name = name,
                            Summary = summary,
                            Returns = returns
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"解析 RevitAPI.xml 失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 搜索 API 文档。
        /// <param name="keyword">可以是类名或方法名，例如 "Extrusion.Create" 或 "LookupParameter"</param>
        /// </summary>
        public static string Search(string keyword)
        {
            EnsureLoaded();
            if (_membersCache == null || _membersCache.Count == 0)
            {
                return "[Error] 本地 API 知识库未就绪或文件缺失。";
            }

            if (string.IsNullOrWhiteSpace(keyword))
            {
                return "请输入要查询的 API 关键字。";
            }

            // 忽略大小写的模糊匹配，并进行智能排序
            var matches = _membersCache
                .Where(m => m.Name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderByDescending(m => 
                    // 优先匹配精确的类名或方法名结尾
                    m.Name.EndsWith("." + keyword, StringComparison.OrdinalIgnoreCase) || 
                    m.Name.IndexOf("." + keyword + "(", StringComparison.OrdinalIgnoreCase) >= 0)
                .ThenBy(m => 
                    // T: (类型) 优先级最高, M: (方法) 其次, P: (属性) 和 F: (字段) 放后面
                    m.Name.StartsWith("T:") ? 0 : 
                    (m.Name.StartsWith("M:") ? 1 : 2))
                .ThenBy(m => m.Name.Length) // 名字短的优先
                .Take(40) // 放宽到 40 条，DeepSeek 上下文足够大，防止 LLM 被大量垃圾重载/属性淹没导致反复重试
                .ToList();

            if (matches.Count == 0)
            {
                return $"未找到包含 '{keyword}' 的 API 定义。";
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"为您找到以下关于 '{keyword}' 的官方 API 定义：\n");
            
            foreach (var match in matches)
            {
                sb.AppendLine($"【API 签名】: {match.Name}");
                if (!string.IsNullOrEmpty(match.Summary))
                {
                    sb.AppendLine($"【功能说明】: {match.Summary}");
                }
                if (!string.IsNullOrEmpty(match.Returns))
                {
                    sb.AppendLine($"【返回值】: {match.Returns}");
                }
                sb.AppendLine("--------------------------------------------------");
            }

            return sb.ToString();
        }
    }
}
