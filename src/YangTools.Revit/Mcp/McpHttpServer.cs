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

        private void Listen()
        {
            while (_listener.IsListening)
            {
                try
                {
                    var context = _listener.GetContext();
                    var request = context.Request;

                    if (request.HttpMethod == "POST")
                    {
                        using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                        {
                            string body = reader.ReadToEnd();
                            JObject payload = JObject.Parse(body);
                            
                            var tcs = new TaskCompletionSource<string>();
                            _handler.EnqueueCommand(payload, tcs);
                            _externalEvent.Raise();

                            string result = tcs.Task.Result; 

                            byte[] buffer = Encoding.UTF8.GetBytes(result);
                            context.Response.ContentLength64 = buffer.Length;
                            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                        }
                    }
                    else
                    {
                        byte[] buffer = Encoding.UTF8.GetBytes("Revit MCP Receiver is running.");
                        context.Response.ContentLength64 = buffer.Length;
                        context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                    }
                    context.Response.Close();
                }
                catch (Exception)
                {
                    // Ignore
                }
            }
        }
    }
}
