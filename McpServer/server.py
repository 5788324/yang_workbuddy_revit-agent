import asyncio
import json
import urllib.request
import urllib.error
import os
from mcp.server import Server
from mcp.server.stdio import stdio_server
from mcp.types import Tool, TextContent

server = Server("revit-mcp")

# C# 插件监听的本地端口（可通过环境变量 REVIT_MCP_URL 覆盖）
REVIT_LOCAL_URL = os.environ.get("REVIT_MCP_URL", "http://localhost:8081/mcp/")

async def send_to_revit(command_name: str, args: dict, timeout: int = 30) -> str:
    """向本地 Revit C# 插件发送指令（异步 + 超时保护）"""
    data = json.dumps({
        "command": command_name,
        "args": args
    }).encode('utf-8')

    def _sync_call():
        req = urllib.request.Request(REVIT_LOCAL_URL, data=data,
                                     headers={'Content-Type': 'application/json'})
        try:
            with urllib.request.urlopen(req, timeout=timeout) as response:
                return response.read().decode('utf-8')
        except urllib.error.URLError as e:
            return f"Error: 无法连接到 Revit，Revit 可能未启动或插件未加载。详情: {e}"
        except TimeoutError:
            return "Error: 与 Revit 通信超时，请检查 Revit 是否响应。"
        except Exception as e:
            return f"Error: 与 Revit 通信失败。详情: {str(e)}"

    return await asyncio.to_thread(_sync_call)


@server.list_tools()
async def handle_list_tools() -> list[Tool]:
    return [
        Tool(
            name="run_revit_command",
            description="执行预设在 Revit C# 插件中的高效命令 (老员工技能)",
            inputSchema={
                "type": "object",
                "properties": {
                    "commandName": {"type": "string", "description": "需要执行的命令名称"},
                    "args": {"type": "object", "description": "该命令所需的参数字典"}
                },
                "required": ["commandName", "args"]
            }
        ),
        Tool(
            name="run_pyrevit_script",
            description="生成并执行动态的 Python/pyRevit 脚本 (实习生技能，无需编译)",
            inputSchema={
                "type": "object",
                "properties": {
                    "scriptContent": {"type": "string", "description": "完整的 Python (pyRevit) 脚本代码"}
                },
                "required": ["scriptContent"]
            }
        )
    ]


@server.call_tool()
async def handle_call_tool(name: str, arguments: dict) -> list[TextContent]:
    if name == "run_revit_command":
        res = await send_to_revit(arguments["commandName"], arguments["args"])
        return [TextContent(type="text", text=res)]
    elif name == "run_pyrevit_script":
        res = await send_to_revit("eval_python", {"script": arguments["scriptContent"]})
        return [TextContent(type="text", text=res)]
    else:
        raise ValueError(f"Unknown tool: {name}")


async def main():
    async with stdio_server() as (read_stream, write_stream):
        await server.run(read_stream, write_stream, server.create_initialization_options())


if __name__ == "__main__":
    asyncio.run(main())
