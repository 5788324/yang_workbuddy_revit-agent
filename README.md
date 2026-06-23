# YangTools Revit - 个人多功能二次开发插件框架

[![Revit 2024](https://img.shields.io/badge/Revit-2024%20(net48)-blue.svg)](https://www.autodesk.com/)
[![Revit 2025](https://img.shields.io/badge/Revit-2025%20(net8.0)-darkgreen.svg)](https://www.autodesk.com/)
[![License](https://img.shields.io/badge/License-MIT-purple.svg)](#)

这是一个面向 **Revit 2024 及更高版本** (如 Revit 2025+) 的模块化、轻量级插件基础开发框架。专为个人高频迭代开发设计，支持功能的**一秒增删**与**免管理员权限安装**。

---

## 🌟 核心设计优势

1. **一套代码，双重兼容 (Multi-Targeting)**：
   利用新版 SDK-Style 项目格式，编译时自动同时生成针对 `.NET Framework 4.8` (适配 Revit 2024) 和 `.NET 8.0-windows` (适配 Revit 2025+) 的运行库。
2. **极速功能增删 (Dynamic Ribbon Loader)**：
   利用自定义特性 `[RibbonButton]` 加反射机制，在 Revit 启动时自动构建 UI。**想增加或删除功能，只需直接增加或删除对应的 C# 代码文件即可，无需编写复杂的 UI 静态代码。**
3. **免密码极简部署 (Non-Admin)**：
   插件全部部署在当前用户的 `%APPDATA%` 目录下，不需要管理员特权即可顺利安装与卸载。
4. **编译自动化 (Auto-Deploy)**：
   项目配置了 MSBuild 后处理任务，您在 VS/Rider 中**点击编译，即代表安装完成**，重新打开 Revit 即可调试。
5. **Modern WPF UI**：
   集成了精致的深色风格 WPF 桥接窗口，使用句柄绑定技术防弹窗失踪。

---

## 📂 项目目录结构

```text
e:\Antigravity\YANG TOOLS_REVIT\
├── README.md                          # 本说明文档
├── YangTools.Revit.sln                # Visual Studio / Rider 解决方案入口
├── src/
│   └── YangTools.Revit/
│       ├── YangTools.Revit.csproj     # 双框架 SDK 格式主工程配置文件
│       ├── YangTools.Revit.addin      # Revit 描述清单文件
│       ├── App.cs                     # 插件生命周期入口 (IExternalApplication)
│       ├── Core/
│       │   ├── RibbonButtonAttribute.cs # 标记自动注册 UI 的自定义属性
│       │   └── RibbonBuilder.cs       # 反射发现并组装菜单的核心类
│       ├── Commands/                  # 功能命令集合（在此文件夹增删功能文件）
│       │   ├── HelloWorldCommand.cs   # 示例：基础弹窗测试
│       │   └── SampleWindowCommand.cs # 示例：激活现代化 WPF 窗口
│       └── UI/
│           ├── SampleWindow.xaml      # 现代暗黑色调 WPF 交互窗口
│           └── SampleWindow.xaml.cs   # WPF 窗口与 Revit API 交互后台
├── docs/
│   ├── Architecture.md                # 架构设计解析说明书
│   ├── Development.md                 # 开发者指南（快速添加功能与调试）
│   └── Installation.md                # 部署安装与卸载指引
└── scripts/
    ├── Install.ps1                    # 免管理员一键编译部署脚本
    └── Uninstall.ps1                  # 免管理员一键卸载清理脚本
```

---

## 🚀 1分钟快速上手

### 1. 克隆与载入项目
* 双击打开 [YangTools.Revit.sln](file:///e:/Antigravity/YANG_TOOLS_REVIT/YangTools.Revit.sln) 解决方案。
* 项目会自动通过 NuGet 还原 `Nice3point.Revit.Api`（无需本地 Program Files 路径下有固定的 RevitAPI 物理依赖）。

### 2. 本地开发与测试
1. 在 IDE 中直接执行 **生成解决方案 (Build)**。
2. 编译成功后，输出信息会提示自动部署完毕：
   ```text
   [Auto Deploy] 正在部署至 Revit 2024 Addins 目录...
   [Auto Deploy] 正在部署至 Revit 2025 Addins 目录...
   ```
3. 打开 Revit 2024 或 2025，在工具栏顶部您会看到崭新的 `YangTools` 标签页，并包含 **“你好，Revit”** 以及 **“个人助手”** 两个按钮，点击即可运行！

### 3. 一键部署与卸载 (Powershell)
* **安装/编译部署**：
  ```powershell
  .\scripts\Install.ps1
  ```
* **干净卸载**：
  ```powershell
  .\scripts\Uninstall.ps1
  ```

---

## 📝 开发规范与交接要求 (Project Requirements)

**强制要求**：为了保证项目的可维护性和交接的顺畅，**每天必须更新工作日志**（如项目根目录下的 `操作日志.md` 或 `工作日志.md`）。请将所有的功能修改、Bug 修复、逻辑变更和架构优化详细记录在案。**要牢记这一点，这是方便后续项目完美交接的关键步骤。**

---

## 📖 深度查阅文档
* 想探究跨 .NET 架构设计原理？请查阅 [架构设计说明书 (docs/Architecture.md)](file:///e:/Antigravity/YANG_TOOLS_REVIT/docs/Architecture.md)。
* 想了解如何新增、删除业务指令？请查阅 [开发者指南 (docs/Development.md)](file:///e:/Antigravity/YANG_TOOLS_REVIT/docs/Development.md)。
* 想了解如何在外部设备手动部署插件？请查阅 [安装与卸载指南 (docs/Installation.md)](file:///e:/Antigravity/YANG_TOOLS_REVIT/docs/Installation.md)。
