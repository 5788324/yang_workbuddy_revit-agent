# YangTools Revit 插件 v2.9

> 个人 Revit 效率增强插件，支持 Revit 2021-2027，57 个源文件，29 个功能按钮，4 套主题配色。

---

## 项目概览

| 项目 | 说明 |
|------|------|
| **名称** | YangTools Revit 个人插件 |
| **版本** | v2.9 |
| **框架** | .NET Framework 4.8 (2021-2024) / .NET 8.0 (2025+) |
| **平台** | x64 only |
| **语言** | C# + WPF (XAML) |
| **Revit API** | Nice3point.Revit.Api NuGet |
| **许可证** | 仅供学习交流使用 |

---

## 项目结构

```
YangTools.Revit/
├── src/YangTools.Revit/
│   ├── App.cs                          # 插件入口：Ribbon构建 + MCP启动 + 主题初始化
│   ├── Commands/                       # 29个 ExternalCommand（每个按钮一个）
│   ├── Core/
│   │   ├── RibbonBuilder.cs            # 自动扫描 [RibbonButton] 属性构建 Ribbon
│   │   ├── RibbonConfigManager.cs      # 面板可见性配置持久化
│   │   ├── ThemeManager.cs             # 4套主题切换（light/dark/prof-blue/apple）
│   │   ├── DeepSeekClient.cs           # AI 对话客户端（流式输出）
│   │   ├── RevitEventHandler.cs        # ExternalEvent 线程安全队列
│   │   ├── TransactionHelper.cs        # Transaction 包装工具
│   │   └── BatchTasks/
│   │       ├── BatchTaskEngine.cs       # NWC/IFC/DWG/PDF 批量导出 + CAD/Revit 链接
│   │       ├── BatchTaskViewModel.cs    # 多文档批处理数据模型
│   │       └── CadLinkService.cs        # CAD 链接服务
│   ├── Mcp/
│   │   └── McpHttpServer.cs            # MCP HTTP 服务器（AI↔Revit 通信）
│   ├── Models/                         # 数据模型
│   └── UI/
│       ├── Themes/                     # 4套主题资源字典
│       │   ├── LightTheme.xaml         # 暖棕风格
│       │   ├── DarkTheme.xaml          # 深色风格
│       │   ├── ProfBlueTheme.xaml      # 专业蓝风格
│       │   └── AppleTheme.xaml         # 苹果水滴风
│       ├── SharedStyles.xaml           # 全局统一按钮/输入框样式
│       ├── ThemeHelper.cs              # 窗口主题注入工具
│       ├── AssistantWindow.xaml/.cs    # 系统设置窗口（主题/面板/关于/Hello/MCP）
│       ├── CopilotPanel.xaml/.cs       # AI 助手侧边栏（流式对话 + 摸鱼模式）
│       ├── BatchTaskWindow.xaml/.cs    # 批处理与云链接窗口
│       └── ...                         # 其余 28 个功能窗口
├── McpServer/
│   └── server.py                       # Python MCP 服务端
├── deploy/                             # 部署输出目录
│   ├── 2022/ → 2025/                   # 各版本插件文件
│   └── deploy.zip                      # 安装器资源包
├── Deploy.cmd                          # 手动部署脚本
├── build_installer.cmd                 # 一键构建 EXE 安装器
└── YangTools.Revit.sln                 # Visual Studio 解决方案
```

---

## 功能面板总览（29 个按钮）

### 总控中心（1 个）
| 按钮 | 功能 |
|------|------|
| 批处理与云链接 | JSON 配置批量导出 NWC/IFC/DWG/PDF，批量链接 Revit/CAD 模型，支持多文档 |

### 模型修改区（5 个）
| 按钮 | 功能 |
|------|------|
| 线性布置 | 沿路径线性布置族实例 |
| 标高修改 | 修改图元标高并保持原位置 |
| 基于面转换 | 基于面族 → 非基于面常规模型转换 |
| 实体生成(Loft) | 基于轮廓生成放样/拉伸三维实体 |
| 布尔几何 | 两个族实例布尔运算（连接/剪切/融合） |

### 项目管理区（4 个）
| 按钮 | 功能 |
|------|------|
| 族文档管理 | 管理、重载、浏览族文档 |
| 族实例管理 | 管理当前文档中的族实例 |
| 项目资产管理器 | 视图/材质/工作集/过滤器/填充图案/线型/系统配置/链接/编组管理 |
| 图纸管理 | 图纸列表编辑、Excel 批量建图/更新、图框换型 |

### 文本工具区（4 个）
| 按钮 | 功能 |
|------|------|
| 文本修改 | 查找、替换、格式化文本（支持选择/视图/项目范围） |
| 文本合并 | 用分隔符合并多个文本 |
| 等距分布 | 按间距及排列方式分布文本 |
| 对齐到线 | 文本起点/终点对齐到虚拟线 |

### 视图修改区（3 个）
| 按钮 | 功能 |
|------|------|
| 可见性拷贝 | 视图覆盖设置复制到其他视图 |
| 覆盖清理 | 清除手动覆盖的图形设置 |
| 剖面(By Line) | 模型线/详图线生成剖面视图 |

### 标注工具区（1 个）
| 按钮 | 功能 |
|------|------|
| 标注替换 | 拾取标注替换显示文本 |

### MEP 工具（2 个）
| 按钮 | 功能 |
|------|------|
| 管井标高修改 | 管井族标高偏移量修改 |
| 管道修改 TypeA | 文字内容修改管道注释/尺寸/高程 |

### 其他面板（7 个）
| 面板 | 按钮 | 功能 |
|------|------|------|
| 导入工具区 | 从CAD粘贴 | 提取 AutoCAD 剪贴板数据导入 |
| 检查工具区 | 中文检查 | 检查项目中是否包含中文字符 |
| 项目信息区 | 文件信息 | 项目基本信息与清理预测 |
| 项目工具区 | 项目微工具 | 管理并运行外部项目微工具脚本 |
| 系统管理区 | 系统设置 | 主题配色 + 面板可见性 + 关于 + MCP 状态 |
| 系统管理区 | AI 助手 | 唤醒 Copilot 侧边栏 |
| 系统管理区 | MCP 状态 | MCP 服务状态查看 |
| 系统管理区 | 你好，Revit | 验证测试 |

---

## 主题系统

4 套主题，通过 `DynamicResource` 全窗口实时切换：

| 主题 | ID | 色系 |
|------|-----|------|
| 暖棕 | `light_warm` | 咖啡/米色，舒适护眼 |
| 深色 | `dark_modern` | 暗色底 + 浅色字 |
| 专业蓝 | `prof_blue` | 蓝灰风格 |
| 苹果水滴 | `apple_frost` | iOS 风格浅灰 |

**切换方式**：系统设置 → 界面设置 → 点击主题色块（即时生效，全窗口跟随）

---

## AI 助手（Copilot）

- **流式输出**：DeepSeek API 对话，打字机效果实时显示
- **摸鱼模式**：角色扮演（猫娘/小秘书/哲学家），含敏感词过滤
- **MCP 集成**：AI 可通过 MCP 协议直接操作 Revit

---

## 技术架构

### 核心设计模式
- **属性驱动 Ribbon**：`[RibbonButton]` 注解自动扫描并构建 Ribbon UI，无需手动注册
- **ExternalEvent 线程安全**：`RevitEventHandler` 队列管理所有 Revit API 调用
- **DynamicResource 主题**：所有颜色通过 `DynamicResource` 绑定，4 套 ResourceDictionary 热切换
- **ThemeHelper 注入**：每个窗口 `ApplyToWindow()` 注入当前主题资源

### 依赖包
| 包 | 用途 |
|----|------|
| Nice3point.Revit.Api | Revit API 引用程序集 |
| Newtonsoft.Json | JSON 序列化 |
| MiniExcel | Excel 导入导出 |
| IronPython | Python 脚本引擎 |

---

## 编译

### 前置条件
- Visual Studio 2022+ 或 .NET SDK 8.0+
- .NET Framework 4.8 SDK（编译 2021-2024 版本）
- Revit API NuGet 包自动下载

### 编译命令

```bash
# Revit 2022 (net48, 需 Windows)
dotnet build src\YangTools.Revit\YangTools.Revit.csproj -c 2022

# Revit 2024 (net48, 需 Windows)
dotnet build src\YangTools.Revit\YangTools.Revit.csproj -c 2024

# Revit 2025 (net8.0, 任意平台)
dotnet build src\YangTools.Revit\YangTools.Revit.csproj -c 2025 -p:EnableWindowsTargeting=true
```

输出目录：`src/YangTools.Revit/bin/x64/{版本号}/`

---

## 安装

1. 下载 `YangTools_Installer_v2.9.zip`
2. 解压，运行 `Deploy.cmd`（或手动复制 `deploy/{版本号}/` 到 `C:\ProgramData\Autodesk\Revit\Addins\{版本号}\`）
3. 启动 Revit

---

## 版本历史

| 版本 | 关键更新 |
|------|---------|
| v2.9 | 面板可见性保存+重启提示、DLL 编译完成 |
| v2.8 | 图纸管理弹窗修复、项目资产管理器精简、系统设置 UI 改进 |
| v2.7 | SolidColorBrush 循环引用崩溃修复（3 文件） |
| v2.6 | 全量硬编码颜色清零（26 文件 × 数百处 → DynamicResource） |
| v2.5 | 标题栏渐变统一 + IFC 事务修复 + ExternalCommand 保护 + 图标统一 |
| v2.4 | 基础版：多主题、AI 助手、批处理、MCP 服务 |

---

## 链接

- GitHub: https://github.com/5788324/yang_workbuddy_revit-agent
- Revit 支持: 2021 / 2022 / 2023 / 2024 / 2025 / 2026 / 2027

---

*免责声明：本插件仅供学习与交流使用。开发者不对因使用本插件引起的任何直接或间接损失承担责任。*
