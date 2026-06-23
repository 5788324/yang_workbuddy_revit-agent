"""生成 Fluent 风格图标替换 Material 风图标"""
from PIL import Image, ImageDraw
import os, math

OUT = "/workspace/YANG_TOOLS_REVIT/src/YangTools.Revit/Icons"
C = "#5C8DB5"  # Fluent Blue

def make(name, draw_fn):
    for sz in [16, 32]:
        img = Image.new("RGBA", (sz, sz), (0,0,0,0))
        d = ImageDraw.Draw(img)
        sc = sz / 16.0
        def s(v): return int(round(v * sc))
        draw_fn(d, s, sz)
        path = os.path.join(OUT, f"{name}_{sz}.png")
        img.save(path)
        print(f"  {name}_{sz}.png")

# ===== copilot (AI 助手) — 大脑/芯片 =====
def draw_copilot(d, s, sz):
    # 圆形头部 + 内部齿轮线条
    cx, cy = s(8), s(7)
    r = s(5.5)
    d.ellipse([cx-r, cy-r, cx+r, cy+r], outline=C, width=max(1,s(1.2)))
    # 内部电路
    d.line([cx-s(3),cy,cx+s(3),cy], fill=C, width=max(1,s(1)))
    d.line([cx,cy-s(3),cx,cy+s(3)], fill=C, width=max(1,s(1)))
    # 底部小圆
    d.ellipse([cx-s(1.5),s(12),cx+s(1.5),s(14)], outline=C, width=max(1,s(1)))

make("copilot", draw_copilot)

# ===== hello (你好Revit) — 对话气泡 =====
def draw_hello(d, s, sz):
    # 气泡
    pts = [(s(3),s(2)), (s(14),s(2)), (s(14),s(11)), (s(8),s(11)), (s(5),s(14)), (s(5),s(11)), (s(3),s(11))]
    d.polygon(pts, outline=C, fill=C+"18", width=max(1,s(1.2)))
    # 文字横线
    d.line([s(5),s(5),s(12),s(5)], fill=C, width=max(1,s(1)))
    d.line([s(5),s(8),s(10),s(8)], fill=C, width=max(1,s(0.8)))

make("hello", draw_hello)

# ===== mcp_server (MCP 状态) — 服务器节点 =====
def draw_mcp(d, s, sz):
    # 中央服务器方块
    d.rectangle([s(5),s(1),s(11),s(6)], outline=C, width=max(1,s(1.2)))
    d.line([s(6.5),s(2.5),s(9.5),s(2.5)], fill=C, width=max(1,s(0.8)))
    d.line([s(6.5),s(4),s(9.5),s(4)], fill=C, width=max(1,s(0.8)))
    # 左客户端
    d.rectangle([s(1),s(5),s(4),s(7)], outline=C, width=max(1,s(1)))
    d.line([s(4),s(6),s(5),s(6)], fill=C, width=max(1,s(0.8)))
    # 右客户端
    d.rectangle([s(12),s(5),s(15),s(7)], outline=C, width=max(1,s(1)))
    d.line([s(11),s(6),s(12),s(6)], fill=C, width=max(1,s(0.8)))
    # 底部连接线
    d.line([s(8),s(6),s(8),s(10)], fill=C, width=max(1,s(1)))
    d.ellipse([s(6.5),s(10),s(9.5),s(13)], outline=C, width=max(1,s(1)))
    # 数据库图标
    d.line([s(7),s(12),s(9),s(12)], fill=C, width=max(1,s(0.8)))

make("mcp_server", draw_mcp)

print("\nDone! 3 Fluent icons regenerated.")
