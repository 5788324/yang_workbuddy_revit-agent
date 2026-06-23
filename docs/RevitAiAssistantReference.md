# 建筑软件 AI 助手（兼双面板“摸鱼”系统）开发参考文档

本文档总结了此前在 AutoCAD 环境下开发的 AI 助手（带极致视觉伪装与底层热键系统）的全部特性与核心实现逻辑。
**该文档可作为 Prompt 直接提供给其他 AI，用于在 Autodesk Revit 等其他宿主软件中复刻一套同级别的 AI 助手插件。**

---

## 一、 核心需求与场景定位

这是一个深度内嵌于专业宿主软件（AutoCAD / Revit）的 WPF 侧边栏面板（PaletteSet / DockablePane）。
它的核心目标有两个：
1. **绝对的生产力**：能够利用大语言模型（LLM）理解自然语言，读取图纸/模型上下文，并自动生成/执行代码（LISP/Revit API）来操作模型。
2. **绝对的安全性（视觉伪装）**：在开放式办公环境中，必须具备一秒切换到“看似在工作”的伪装状态，且面板 UI 必须与宿主软件的原生属性面板 100% 融为一体。

---

## 二、 核心特性与技术实现指南

### 1. 极致的视觉伪装系统（双模式 UI）

在 UI 设计上，彻底摒弃传统 AI 聊天软件（如 ChatGPT、微信）的圆润设计风格（大圆角、亮色气泡、巨大的发送按钮），采用**伪装成原生参数面板**的策略。

*   **像素级色彩提取**：不要使用标准的“Dark Theme”或“Light Theme”色值。必须通过对 Revit 原生“属性 (Properties)”面板进行截图，逐像素采样其背景色、数据行（Row）底色、表头底色、边框色、选中色。
*   **组件形态伪装**：
    *   **气泡改写**：将对话气泡的 `CornerRadius` 降至极小（如 `2` 或 `0`），取消阴影，使其看起来像一个多行文本参数输入格（TextBox / DataGrid Cell）。
    *   **操作区收敛**：底部输入框内边距缩小，发送按钮改为极小尺寸（如 28x28 或更小），背景色与面板背景融为一体，仅悬停时亮起。
*   **MVVM 双模式切换逻辑**：
    *   在 ViewModel 中维护一个 `bool IsMoyuMode`（摸鱼模式）属性。
    *   通过 WPF 的 `DataTrigger` 绑定此属性。当 `IsMoyuMode = true` 时：
        *   隐藏真实的 AI 输入框、设置按钮。
        *   将对话记录区域的模板切换为伪造的“参数列表”模板（或直接覆盖一层假的参数 UI）。
        *   消除所有引起注意的动画。

### 2. 底层全局热键（无死角监听）

由于 AutoCAD/Revit 等大型软件在其绘图区/模型视图区会强行接管键盘焦点，标准的 WPF `KeyDown` 事件或普通键盘监听会彻底失效（即离开 AI 面板后快捷键无效）。

**技术方案：应用级消息循环拦截 (Message Filter / Hook)**
*   必须使用宿主程序底层的消息过滤机制（如 AutoCAD 的 `Application.PreTranslateMessage` 或基于 `SetWindowsHookEx(WH_KEYBOARD_LL)` 的底层钩子），在消息分发到 UI 线程前强行拦截键盘操作。
*   **64 位系统指针溢出陷阱 (Critical Bug)**：在解析 Windows 消息参数 `e.Message.wParam` 或 `lParam` 时，由于 64 位环境下内存地址超大，直接使用 `.ToInt32()` 会导致 `.NET OverflowException`（算术溢出崩溃）。**必须使用掩码截断：`unchecked((int)e.Message.wParam.ToInt64())`**。

**内置热键动作：**
*   **状态切换键 (如 `Ctrl` 单击)**：仅修改 `ViewModel.IsMoyuMode = !IsMoyuMode`，实现毫秒级的 UI 形态切换（AI 助手 <-> 原生属性面板）。
*   **老板键 (如 `Alt` 单击)**：触发应急响应。
    1. 瞬间隐藏整个 PaletteSet/DockablePane（`Visible = false`）。
    2. 向宿主软件发送一个“合理”的工作指令（如 AutoCAD 中的 `Zoom All`，或 Revit 里的 `Zoom To Fit`），使得屏幕画面发生工作相关的合理变动。

### 3. MCP (Model Context Protocol) 与外部大模型通信

不建议在插件内硬编码与某个特定大模型的 HTTP 请求逻辑，推荐使用 MCP 架构实现解耦：
*   **架构层级**：WPF 插件作为 MCP Client，后台启动一个 Node.js 或 Python 编写的 MCP Server（包含具体的 LLM 通信、Agent 逻辑）。
*   **前端设置**：UI 内部提供一个可折叠的“设置”区域，允许用户随时修改 API Base URL、API Key、Model Name。包含“获取模型列表”和“测试连接”功能。
*   **流式交互 (Streaming)**：必须支持流式输出响应（打字机效果）。在生成过程中，发送按钮应变为红色的“停止 (■)”按钮，绑定 `CancellationTokenSource` 以支持随时打断废话。

### 4. 智能上下文捕获 (Context Awareness)

AI 必须“看”得见用户在干什么。
*   **交互设计**：在输入框旁放置一个“回形针 📎” ToggleButton（上下文开关）。
*   **数据流向**：当开关打开且用户发送消息时，插件首先调用宿主软件 API（Revit API `uidoc.Selection.GetElementIds()`），获取当前被选中的实体。
*   **数据解析**：提取这些实体的核心属性（ID、类别、坐标、关键参数值等），将其序列化为 JSON 或 Markdown 表格，连同用户的提问一并作为 Prompt 隐式发送给 LLM。

### 5. 代码生成与安全执行引擎

*   LLM 负责生成操作宿主的脚本（Revit 推荐生成 C# Macro、Python Shell 脚本或直接通过 Dynamo 调用）。
*   插件需正则提取 Markdown 中的代码块（如 ` ```csharp ... ``` `）。
*   **执行沙盒**：在调用宿主 API 核心代码时，必须封装在事务（Transaction）和异常捕获块（`try-catch`）中。对于 Revit，必须确保在合法的 `IExternalEventHandler` 和 `Transaction` 中执行外部传来的修改指令，防止破坏底层模型数据或引发软件崩溃。

---

## 三、 给接力 AI 开发者的特别提示

1. **宿主差异注意**：从 AutoCAD 移植到 Revit 时，最大的区别在于 API 的结构。AutoCAD 是基于命令的事件循环，而 Revit 要求所有修改模型的操作必须包装在 `Transaction` 中，并且异步修改 UI 之外的数据必须使用 `ExternalEvent`（因为 Revit API 不允许在非主线程直接调用）。
2. **面板注册差异**：AutoCAD 使用 `PaletteSet`，而 Revit 使用 `DockablePane`。在 Revit 中注册 `DockablePane` 需要在 `IExternalApplication.OnStartup` 阶段完成，并且需要实现 `IDockablePaneProvider` 接口。
3. **视觉设计**：Revit 的界面虽然也有深浅色，但与 AutoCAD 的色系完全不同（Revit 的默认属性栏多为亮灰色）。**请务必向用户索要 Revit 的界面截图，重新提取色值进行伪装 UI 的构建。**
