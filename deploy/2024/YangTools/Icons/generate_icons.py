"""
YangTools 图标生成器
生成 Fluent UI 风格的缺失图标（蓝色线条风，与其他图标一致）
"""
from PIL import Image, ImageDraw
import os

OUT_DIR = os.path.dirname(__file__)
BLUE = "#5C8DB5"
DARK_BLUE = "#3A6A94"

def make_icon(name, draw_func, color=BLUE):
    """生成 16x16 和 32x32 图标"""
    for size in [16, 32]:
        img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
        d = ImageDraw.Draw(img)
        scale = size / 16.0
        draw_func(d, scale, color, size)
        img.save(os.path.join(OUT_DIR, f"{name}_{size}.png"))

def s(v, scale):
    """缩放坐标"""
    return int(round(v * scale))

# ===== 布尔几何 (boolean_geometry) =====
def draw_boolean(d, sc, c, sz):
    """圆形叠加效果 → 布尔并集"""
    r = s(3, sc)
    # 左圆
    d.ellipse([s(1, sc), s(2, sc), s(9, sc), s(10, sc)], outline=c, width=max(1, s(1.3, sc)))
    # 填充淡色
    d.ellipse([s(2, sc), s(3, sc), s(8, sc), s(9, sc)], fill=c + "30", outline=None)
    # 右圆
    d.ellipse([s(6, sc), s(2, sc), s(14, sc), s(10, sc)], outline=c, width=max(1, s(1.3, sc)))
    d.ellipse([s(7, sc), s(3, sc), s(13, sc), s(9, sc)], fill=c + "30", outline=None)
    # 加号
    mid = s(8, sc)
    d.line([(mid - s(1.5, sc), s(3, sc)), (mid + s(1.5, sc), s(3, sc))], fill=c, width=max(1, s(1.2, sc)))

make_icon("boolean_geometry", draw_boolean)
print("✓ boolean_geometry")

# ===== 族实例管理 (family_instance) =====
def draw_family_instance(d, sc, c, sz):
    """多个方块 → 族实例"""
    for col in range(3):
        x = s(2, sc) + col * s(4.5, sc)
        y_base = s(8, sc) if col != 2 else s(6, sc)
        d.rectangle([x, y_base, x + s(3.5, sc), s(13, sc)], outline=c, width=max(1, s(1.2, sc)))

make_icon("family_instance", draw_family_instance)
print("✓ family_instance")

# ===== 线性布置 (linear_placement) =====
def draw_linear(d, sc, c, sz):
    """直线 + 等距标记点 → 线性布置"""
    d.line([s(2, sc), s(8, sc), s(14, sc), s(8, sc)], fill=c, width=max(1, s(1.5, sc)))
    for i in range(4):
        x = s(3, sc) + i * s(3, sc)
        r = s(1.3, sc)
        d.ellipse([x - r, s(8, sc) - r, x + r, s(8, sc) + r], fill=c)

make_icon("linear_placement", draw_linear)
print("✓ linear_placement")

# ===== 管井标高 (manhole_elevation) =====
def draw_manhole(d, sc, c, sz):
    """圆 + 水平线 + 上下箭头 → 管井标高"""
    r = s(3.5, sc)
    d.ellipse([s(5, sc), s(3, sc), s(12, sc), s(10, sc)], outline=c, width=max(1, s(1.2, sc)))
    d.line([s(5, sc), s(6.5, sc), s(12, sc), s(6.5, sc)], fill=c, width=max(1, s(1.2, sc)))
    # 上下箭头
    d.line([s(8.5, sc), s(12, sc), s(8.5, sc), s(10, sc)], fill=c, width=max(1, s(1, sc)))
    d.line([s(8.5, sc), s(3, sc), s(8.5, sc), s(1, sc)], fill=c, width=max(1, s(1, sc)))

make_icon("manhole_elevation", draw_manhole)
print("✓ manhole_elevation")

# ===== 管道修改 (pipe_modify) =====
def draw_pipe(d, sc, c, sz):
    """管道折线 → 管道修改"""
    d.line([s(2, sc), s(13, sc), s(8, sc), s(5, sc), s(14, sc), s(5, sc)], fill=c, width=max(1, s(1.5, sc)))
    # 端点圆
    d.ellipse([s(1, sc), s(12, sc), s(3, sc), s(14, sc)], outline=c, width=max(1, s(1, sc)))

make_icon("pipe_modify", draw_pipe)
print("✓ pipe_modify")

# ===== 面板设置 (ribbon_settings) =====
def draw_ribbon_setting(d, sc, c, sz):
    """滑块 + 齿轮 → 设置"""
    # 齿轮
    r_big = s(3.2, sc)
    cx, cy = s(8, sc), s(8, sc)
    d.ellipse([cx - r_big, cy - r_big, cx + r_big, cy + r_big], outline=c, width=max(1, s(1.2, sc)))
    # 中间小圆
    r_sm = s(1.5, sc)
    d.ellipse([cx - r_sm, cy - r_sm, cx + r_sm, cy + r_sm], fill=c)

make_icon("ribbon_settings", draw_ribbon_setting)
print("✓ ribbon_settings")

# ===== 剖面 (section_line) =====
def draw_section(d, sc, c, sz):
    """折线 + 箭头 → 剖面"""
    d.line([s(2, sc), s(13, sc), s(8, sc), s(8, sc), s(14, sc), s(3, sc)], fill=c, width=max(1, s(1.5, sc)))
    # 箭头
    d.line([s(14, sc), s(3, sc), s(12, sc), s(4.5, sc)], fill=c, width=max(1, s(1.2, sc)))
    d.line([s(14, sc), s(3, sc), s(12.5, sc), s(1.5, sc)], fill=c, width=max(1, s(1.2, sc)))

make_icon("section_line", draw_section)
print("✓ section_line")

# ===== 基于面转换 (face_based) =====
def draw_face_based(d, sc, c, sz):
    """面 + 箭头 → 基于面转换"""
    # 底部面
    d.rectangle([s(2, sc), s(10, sc), s(14, sc), s(12, sc)], outline=c, width=max(1, s(1.2, sc)))
    # 顶部面
    d.rectangle([s(4, sc), s(6, sc), s(12, sc), s(8, sc)], outline=c + "CC", width=max(1, s(1.2, sc)))
    # 箭头
    d.line([s(8, sc), s(8, sc), s(8, sc), s(10, sc)], fill=c, width=max(1, s(1.5, sc)))
    d.line([s(6, sc), s(9, sc), s(8, sc), s(10, sc)], fill=c, width=max(1, s(1.2, sc)))
    d.line([s(10, sc), s(9, sc), s(8, sc), s(10, sc)], fill=c, width=max(1, s(1.2, sc)))

make_icon("face_based", draw_face_based)
print("✓ face_based")

# ===== 批处理 (batch_task) =====
def draw_batch(d, sc, c, sz):
    """多个文件叠放 → 批处理"""
    for i in range(3):
        offset = i * s(1.5, sc)
        y_off = i * s(1.5, sc)
        d.rectangle([s(2, sc) + offset, s(4, sc) - y_off, s(12, sc) + offset, s(12, sc) - y_off],
                   outline=c, width=max(1, s(1.2, sc)))

make_icon("batch_task", draw_batch)
print("✓ batch_task")

# ===== 项目微工具 (micro_tool) =====
def draw_microtool(d, sc, c, sz):
    """小扳手 → 微工具"""
    # 手柄
    d.rounded_rectangle([s(8, sc), s(7, sc), s(14, sc), s(10, sc)], radius=s(1.5, sc),
                       outline=c, width=max(1, s(1.2, sc)))
    # 头部
    d.arc([s(1, sc), s(3, sc), s(8, sc), s(10, sc)], start=180, end=300, fill=c, width=max(1, s(1.2, sc)))

make_icon("micro_tool", draw_microtool)
print("✓ micro_tool")

# ===== 窗口测试 (sample_window) =====
def draw_sample(d, sc, c, sz):
    """小窗口 + 放大镜 → 预览/测试"""
    d.rectangle([s(2, sc), s(4, sc), s(10, sc), s(11, sc)], outline=c, width=max(1, s(1.2, sc)))
    # 放大镜
    r_mag = s(2.5, sc)
    cx, cy = s(11, sc), s(10, sc)
    d.ellipse([cx - r_mag, cy - r_mag, cx + r_mag, cy + r_mag], outline=c, width=max(1, s(1, sc)))

make_icon("sample_window", draw_sample)
print("✓ sample_window")

print("\n🎉 所有图标生成完毕！")
