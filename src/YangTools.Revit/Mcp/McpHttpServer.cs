using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Autodesk.Revit.UI;

namespace YangTools.Revit.Mcp
{
    public class McpHttpServer
    {
        private HttpListener _listener;
        private McpExternalEventHandler _handler;
        private ExternalEvent _externalEvent;

        public McpHttpServer(McpExternalEventHandler handler, ExternalEvent externalEvent)
        {
            _handler = handler;
            _externalEvent = externalEvent;
        }

        public void Start()
        {
            int port = 8081;
            bool started = false;
            while (port <= 8090 && !started)
            {
                _listener = new HttpListener();
                try
                {
                    _listener.Prefixes.Add($"http://localhost:{port}/mcp/");
                    _listener.Start();
                    started = true;
                }
                catch (System.Net.HttpListenerException)
                {
                    _listener.Close();
                    port++;
                }
            }

            if (started)
            {
                Task.Run(() => Listen());
            }
            else
            {
                // 端口全部被占用，静默失败（不会影响 Revit 内部面板的使用）
                _listener.Close();
            }
        }

        public void Stop()
        {
            if (_listener != null && _listener.IsListening)
            {
                _listener.Stop();
                _listener.Close();
            }
        }

        private async Task ListenAsync()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext? context = null;
                try
                {
                    context = await _listener.GetContextAsync().ConfigureAwait(false);
                    var request = context.Request;

                    if (request.HttpMethod == "POST")
                    {
                        string body;
                        using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                        {
                            body = await reader.ReadToEndAsync().ConfigureAwait(false);
                        }

                        JObject payload = JObject.Parse(body);

                        var tcs = new TaskCompletionSource<string>();
                        _handler.EnqueueCommand(payload, tcs);
                        _externalEvent.Raise();

                        // 异步等待 ExternalEvent 完成，避免同步阻塞导致死锁
                        string result = await tcs.Task.ConfigureAwait(false);

                        byte[] buffer = Encoding.UTF8.GetBytes(result);
                        context.Response.ContentLength64 = buffer.Length;
                        await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                    }
                    else
                    {
                        byte[] buffer = Encoding.UTF8.GetBytes("Revit MCP Receiver is running.");
                        context.Response.ContentLength64 = buffer.Length;
                        await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                    }
                }
                catch (HttpListenerException)
                {
                    // 监听器被关闭，正常退出
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[YangTools MCP] 请求处理异常: {ex.Message}");
                }
                finally
                {
                    try { context?.Response.Close(); } catch { }
                }
            }
        }

        private void Listen()
        {
            // 包装异步方法，保持公开 API 向后兼容
            ListenAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }
    }
}
