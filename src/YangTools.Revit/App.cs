using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using YangTools.Revit.Mcp;

namespace YangTools.Revit
{
    /// <summary>
    /// 插件生命周期入口点
    /// </summary>
    public class App : IExternalApplication
    {
        private McpHttpServer _mcpServer;
        public static McpExternalEventHandler GlobalMcpHandler { get; private set; }
        public static ExternalEvent GlobalMcpEvent { get; private set; }
        public static RevitReadEventHandler GlobalReadHandler { get; private set; }
        public static ExternalEvent GlobalReadEvent { get; private set; }

        /// <summary>
        /// Revit 启动时触发，在此处构建 UI 界面
        /// </summary>
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                // 调用动态 Ribbon 构建器 (AI-A 负责的插件功能)
                RibbonBuilder.Build(application);

                // 启动 MCP 监听器 (AI-B 负责的自然语言控制后台，完全隔离)
                GlobalMcpHandler = new McpExternalEventHandler();
                GlobalMcpEvent = ExternalEvent.Create(GlobalMcpHandler);
                _mcpServer = new McpHttpServer(GlobalMcpHandler, GlobalMcpEvent);
                _mcpServer.Start();

                // 注册只读委托调度器
                GlobalReadHandler = new RevitReadEventHandler();
                GlobalReadEvent = ExternalEvent.Create(GlobalReadHandler);

                // 启动全局快捷键监听
                NativeHookManager.StartHook();

                // 注册 AI 助手悬浮面板
                var copilotPanel = new UI.CopilotPanel();
                var dpid = new DockablePaneId(new System.Guid("d5e89a24-9c56-4c31-97b0-13f56d0285a7"));
                application.RegisterDockablePane(dpid, "YangTools Copilot", copilotPanel);

                return Result.Succeeded;
            }
            catch (System.Exception ex)
            {
                TaskDialog.Show("YangTools 启动失败", $"在加载个人插件时发生错误：\n{ex.Message}");
                return Result.Failed;
            }
        }

        /// <summary>
        /// Revit 退出时触发，可用于释放资源
        /// </summary>
        public Result OnShutdown(UIControlledApplication application)
        {
            _mcpServer?.Stop();
            NativeHookManager.StopHook();
            return Result.Succeeded;
        }
    }
}
