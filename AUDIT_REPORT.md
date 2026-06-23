# YangTools Revit 插件全面审计报告

> 审计日期：2025年6月  
> 审计范围：所有源码 (70+ .cs, 31 .xaml, 43 图标)，覆盖 UI/UX、图标、代码质量

---

## 🔴 致命/严重问题（必须修）

### 1. 线程死锁 – MCP HTTP 服务器
- **文件**: `Mcp/McpHttpServer.cs:83`
- **问题**: `tcs.Task.Result` 在 HTTP 线程同步阻塞，与 Revit UI 线程形成死锁，导致 Revit 冻结
- **修复**: 改用 `await tcs.Task`

### 2. BooleanGeometry 族文档泄漏
- **文件**: `Commands/BooleanGeometryCommand.cs:93-220`
- **问题**: `famDoc` 打开后若中间任一步骤异常，永远不会关闭，泄漏 Revit 文档句柄
- **修复**: `famDoc.Close()` 放入 `finally` 块

### 3. 布尔运算/删除的空 catch 吞异常
- **文件**: `Commands/BooleanGeometryCommand.cs:135,160,180,211,215`
- **问题**: `doc.Delete()` 失败时完全静默，族可能被部分删除损坏
- **修复**: 至少加日志，关键操作提示用户

### 4. MergeText 分隔符逻辑反了
- **文件**: `Commands/MergeTextCommand.cs:64`
- **问题**: `UseNewlineSeparator=true` 时用空字符串，`false` 时用 `\r`，与命名完全相反
- **修复**: 交换条件

---

## 🟠 高优先级问题

### 5. HttpClient 泄漏
- `CopilotPanel.xaml.cs:425,610` 每次发消息创建新 HttpClient
- `DeepSeekClient` 内也创建了无释放的实例

### 6. CopilotPanel 内存泄漏
- `CopilotPanel.xaml.cs:91` 的 static 事件订阅在 Page 重建时重复添加
- Unloaded 中需要取消订阅

### 7. ExportService 空引用风险
- `ExportService.cs:80,134` `doc.GetElement(viewId)` 可能返回 null → NRE

### 8. 硬编码开发者路径
- `MicroToolEngine.cs:27` 硬编码了 `E:\Yang\92053\92053` 路径

---

## 🟡 中等问题

### 9-14. UI 相关
- **WPF 窗口无 Owner**：LevelModifier、BatchTask、McpStatus、RibbonSettings 四个窗口未设 Owner，可能隐藏在 Revit 后面
- **窗口操作无进度指示**：5 个窗口执行耗时操作时无 loading 状态
- **标题栏不统一**：至少 8 种不同标题栏样式（绿色渐变 vs 灰色）
- **MainWindow 静态引用**：LinearPlacementCommand 的 static 引用阻止 GC
- **魔数 Result 值**：ChineseCheckCommand、SampleWindowCommand 用 (Result)1 而非 Result.Cancelled

### 15-20. 其他
- IronPython 初始化失败无提示
- DeepSeekClient 消息历史无限制增长
- TransactionHelper 方法名与实际行为不符
- ManualResetEventSlim 缺失
- 浮点数迭代器精度风险
- 代码重复（GetTextNoteAngle 等 7 类模式）

---

## 🎨 主题系统 — 致命缺陷

**SharedStyles.xaml 定义了统一样式但被大部分窗口忽略！**

现状问题：
- 27 个窗口各自内嵌样式，色彩/字体/间距不统一
- 修改一个颜色需要改 27+ 处
- 没有主题切换机制
- 窗口标题栏有 8 种不同变体（包括 FamilyInstanceManager 的独特绿色渐变）

---

## 🖼️ 图标系统

| 问题 | 数量 |
|------|------|
| 缺专属图标（用 generic 占位） | 7 个命令 |
| 共享/复用他人图标 | 4 个命令 |
| Fluent 线条风 vs Material 渐变风混用 | 16 vs 4 组 |
| pet_avatar.png (455KB 3D 图) 放在 Icons/ 不合适 | 1 个 |

缺失专属图标的命令：布尔几何、族实例管理、线性布置、管井标高修改、管道修改、面板设置、剖面(By Line)

---

## 📊 编译状态

| 版本 | 目标框架 | 编译 | 
|------|----------|------|
| Revit 2021 | net48 | ✅ (需 Windows) |
| Revit 2022 | net48 | ✅ (需 Windows) |
| Revit 2023 | net48 | ✅ (需 Windows) |
| Revit 2024 | net48 | ✅ (需 Windows) |
| Revit 2025 | net8.0-windows | ✅ (已在本环境编译通过) |
| Revit 2026 | net8.0-windows | ✅ (已在本环境编译通过) |

> 注：2021-2024 用 .NET Framework 4.8，必须在 Windows 上编译。  
> 2025-2026 用 .NET 8.0，可交叉编译。  
> 2027 需要 .NET 10 preview（暂时未装）。
