using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using YangTools.Revit.Core;
using Microsoft.Scripting.Hosting;
using IronPython.Hosting;

namespace YangTools.Revit.Mcp
{
    public class McpExternalEventHandler : IExternalEventHandler
    {
        private ConcurrentQueue<Tuple<JObject, TaskCompletionSource<string>>> _queue = new ConcurrentQueue<Tuple<JObject, TaskCompletionSource<string>>>();
        private ScriptEngine _pythonEngine;

        public McpExternalEventHandler()
        {
            try
            {
                // 初始化内嵌的 Python 运行引擎
                _pythonEngine = Python.CreateEngine();
            }
            catch (Exception ex)
            {
                // 忽略初始化错误，在具体执行时再拦截并向外抛出
            }
        }

        public void EnqueueCommand(JObject payload, TaskCompletionSource<string> tcs)
        {
            _queue.Enqueue(new Tuple<JObject, TaskCompletionSource<string>>(payload, tcs));
        }

        public void Execute(UIApplication uiapp)
        {
            while (_queue.TryDequeue(out var item))
            {
                var payload = item.Item1;
                var tcs = item.Item2;

                string command = payload["command"]?.ToString();
                JObject args = payload["args"] as JObject;

                Document doc = uiapp.ActiveUIDocument?.Document;
                if (doc == null)
                {
                    tcs.SetResult("Error: No active document.");
                    continue;
                }

                // 完美对接 Plugin AI 重构的全局 TransactionGroup 范式
                using (TransactionGroup tg = new TransactionGroup(doc, $"MCP AI 操作: {command}"))
                {
                    tg.Start();
                    string resultMsg = "";
                    bool actionSuccess = false;
                    
                    try
                    {
                        if (command == "eval_python")
                        {
                            string script = args?["script"]?.ToString();
                            if (string.IsNullOrEmpty(script))
                            {
                                throw new Exception("收到的脚本内容为空。");
                            }

                            if (_pythonEngine == null)
                            {
                                throw new Exception("IronPython 引擎未成功初始化，请检查是否在 csproj 中引入了正确依赖。");
                            }

                            // 1. 创建隔离的作用域
                            ScriptScope scope = _pythonEngine.CreateScope();
                            
                            // 预加载 Revit 核心程序集，防止 import 报错
                            _pythonEngine.Runtime.LoadAssembly(typeof(Document).Assembly);
                            _pythonEngine.Runtime.LoadAssembly(typeof(UIApplication).Assembly);
                            
                            // 2. 将 Revit 环境和核心 API 对象注入到 Python 的运行时中
                            scope.SetVariable("uiapp", uiapp);
                            scope.SetVariable("app", uiapp.Application);
                            scope.SetVariable("uidoc", uiapp.ActiveUIDocument);
                            scope.SetVariable("doc", doc);
                            
                            // 兼容性保留
                            scope.SetVariable("__revit__", uiapp);
                            scope.SetVariable("__uidoc__", uiapp.ActiveUIDocument);
                            scope.SetVariable("__doc__", doc);
                            
                            // 3. 执行动态文本代码
                            var ms = new System.IO.MemoryStream();
                            var writer = new System.IO.StreamWriter(ms);
                            _pythonEngine.Runtime.IO.SetOutput(ms, writer);
                            
                            _pythonEngine.Execute(script, scope);
                            
                            writer.Flush();
                            ms.Position = 0;
                            var reader = new System.IO.StreamReader(ms);
                            string stdout = reader.ReadToEnd();
                            
                            // 4. 获取返回结果（AI 可以在代码中把返回值赋值给 __result__ 变量）
                            if (scope.TryGetVariable("__result__", out object pyResult))
                            {
                                resultMsg = pyResult?.ToString() ?? "脚本执行成功 (无返回值)";
                            }
                            else
                            {
                                resultMsg = string.IsNullOrWhiteSpace(stdout) ? "执行成功 (无返回值)" : "执行成功";
                            }
                            
                            if (!string.IsNullOrWhiteSpace(stdout))
                            {
                                resultMsg = $"【终端输出(Print)】:\n{stdout}\n【返回值】:\n{resultMsg}";
                            }
                            actionSuccess = true;
                        }
                        else
                        {
                            resultMsg = $"预设命令 '{command}' 尚未被 Plugin AI 实现。";
                            actionSuccess = false;
                        }
                        
                        if (actionSuccess)
                        {
                            // 使用统一的安全审查弹窗（MCP 调用不弹窗，结果通过返回值回传）
                            TransactionHelper.ShowSuccessAndCommit(tg, "AI 审查确认 (MCP Review)",
                                $"执行结果:\n{resultMsg}", uiapp.ActiveUIDocument, showDialog: false);
                                
                            tcs.SetResult($"Execution completed. Result: {resultMsg}. Group Status: {tg.GetStatus()}");
                        }
                        else
                        {
                            tg.RollBack();
                            tcs.SetResult($"Failed: {resultMsg}");
                        }
                    }
                    catch (Exception ex)
                    {
                        if (tg.GetStatus() == TransactionStatus.Started) 
                        {
                            tg.RollBack();
                        }
                        TaskDialog.Show("MCP AI 错误", $"动态 Python 执行中发生错误，已安全撤销。\n错误详情: {ex.Message}");
                        tcs.SetResult($"Error: {ex.Message}");
                    }
                }
            }
        }

        public string GetName() => "McpExternalEventHandler";
    }
}
