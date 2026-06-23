# YangTools 项目微工具 (Micro Tools) 开发指南

这是一份写给 AI 编程助手（如 ChatGPT, Claude）的开发规范。
当用户要求您为其编写一个专门适用于 **YangTools 项目微工具箱** 的 Revit 插件时，请严格遵守以下开发规则，以确保编译出的 `.dll` 能够被母包的“无锁热更新沙盒”完美加载。

## 🎯 1. 核心架构与原理
YangTools 微工具箱采用了**内存映射 (In-Memory Load)** 技术。它会读取 `.dll` 的二进制数据进入内存执行，而不会在硬盘上锁定文件。
这使得用户可以极限热更新（修改代码 -> 重新编译覆盖 -> 立即生效，无需重启 Revit）。

为了完美融合，您的代码**不需要**任何奇技淫巧，只需要实现最标准的 Revit `IExternalCommand` 接口即可。如果代码写得好，用户未来甚至可以直接把您的 `.cs` 源码复制进主工程直接“转正”。

## 🛠️ 2. 环境配置要求
请在编写代码时，提醒用户使用以下 `.csproj` 框架进行编译：

- **Revit 2024 及以下**：目标框架必须为 `.NET Framework 4.8`
- **Revit 2025 及以上**：目标框架必须为 `.NET 8.0-windows`
- **依赖项**：引用 `RevitAPI.dll` 和 `RevitAPIUI.dll`（不要拷贝到输出目录，即 `Copy Local = False` 或 `<Private>False</Private>`）

```xml
<!-- 极简 csproj 模板示例 (适用于 Revit 2024) -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <UseWPF>true</UseWPF>
  </PropertyGroup>
  <ItemGroup>
    <!-- 请替换为实际的 Revit 安装路径 -->
    <Reference Include="RevitAPI" HintPath="C:\Program Files\Autodesk\Revit 2024\RevitAPI.dll" Private="False"/>
    <Reference Include="RevitAPIUI" HintPath="C:\Program Files\Autodesk\Revit 2024\RevitAPIUI.dll" Private="False"/>
  </ItemGroup>
</Project>
```

## 📝 3. 代码编写四大铁律

> [!IMPORTANT]
> **铁律 1：必须且只实现一个 `IExternalCommand`**
> 宿主沙盒通过反射（Reflection）扫描 DLL。它会自动寻找并实例化**第一个**实现了 `IExternalCommand` 的非抽象类。不要在同一个 DLL 里塞入多个 Command，微工具的核心是一 DLL 一功能。

> [!IMPORTANT]
> **铁律 2：强制手动事务，支持 Ctrl+Z**
> 宿主沙盒**没有**为您开启全局 Transaction！必须声明 `[Transaction(TransactionMode.Manual)]`，并且在代码内部使用 `using (Transaction tx = new Transaction(doc, "操作名称"))` 包裹修改逻辑。这个操作名称将直接显示在 Revit 的撤销菜单中。

> [!WARNING]
> **铁律 3：异常会被宿主捕获，请专注于逻辑**
> 宿主沙盒自带了 `try-catch` 全局异常拦截护盾。因此，您不需要在主逻辑外层写冗长的 try-catch，只需大胆执行；如果崩溃，宿主会优雅拦截并报错给用户。但请确保失败时调用 `tx.RollBack()` 保证数据安全。

> [!TIP]
> **铁律 4：命名空间保持整洁**
> 建议使用 `namespace YangTools.MicroProject.YourFeature`，这样方便未来代码转正合并。

> [!CAUTION]
> **铁律 5：严禁在事务 Commit 后弹出任何 UI**
> 由于宿主沙盒使用了 `TransactionGroup.Assimilate()` 来完美折叠撤销节点，如果您在调用 `tx.Commit()` 之后（且在返回 `Result.Succeeded` 之前）调用了 `TaskDialog.Show` 或任何 WPF 窗口，UI 消息泵会打断 Revit 的日志流，导致所有修改**无法被 Ctrl+Z 撤销**。
> **正确做法**：前置 UI（如用户选择窗口）必须在 `tx.Start()` **之前**弹出完毕。执行成功的提示弹窗请直接省略，沙盒在合并完撤销步后会自动提示成功。

## 💻 4. 标准代码模板示例

请参照以下标准模板为用户生成代码：

```csharp
using System;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI.Selection;

namespace YangTools.MicroProject.WallUpdater
{
    // 必须声明事务模式
    [Transaction(TransactionMode.Manual)]
    public class RunCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // 1. 获取核心上下文
            UIApplication uiapp = commandData.Application;
            Document doc = uiapp.ActiveUIDocument.Document;

            // 2. 数据收集与逻辑判断 (在事务外)
            var walls = new FilteredElementCollector(doc)
                .OfClass(typeof(Wall))
                .WhereElementIsNotElementType()
                .ToElements();

            if (!walls.Any())
            {
                TaskDialog.Show("提示", "当前项目中没有找到任何墙体。");
                return Result.Cancelled;
            }

            // 3. 开启事务 (必须开启，以支持用户 Ctrl+Z 撤销)
            using (Transaction tx = new Transaction(doc, "微工具：批量修改墙体"))
            {
                tx.Start();

                try
                {
                    // 在此编写核心修改逻辑...
                    foreach (var wall in walls)
                    {
                        // 示例：修改某个参数
                        // wall.LookupParameter("Comments")?.Set("YangTools 处理完毕");
                    }

                    tx.Commit();
                    
                    // 🚨 铁律5：绝对不要在这里弹出 TaskDialog！沙盒会在安全的时机代为弹出成功提示。
                    // 直接静默返回成功即可，保障原生的撤销功能不被破坏。
                    return Result.Succeeded;
                }
                catch (Exception ex)
                {
                    tx.RollBack();
                    message = ex.Message; // 传递给宿主的异常护盾
                    return Result.Failed;
                }
            }
        }
    }
}
```

## 🚀 5. 使用与交付流程
生成完代码后，请告诉用户：
1. 新建一个 C# 类库项目，粘贴上述代码。
2. 编译生成 `.dll` 文件。
3. 把这个 `.dll` 文件直接拖进或复制到 YangTools 项目微工具对应的**项目文件夹**里。
4. 无需重启 Revit，在界面上重新点开“项目微工具”，点击新的 DLL 即可瞬间运行！

## 🔄 6. 微工具箱界面功能与插件更新指南
微工具箱界面提供了强大的项目与插件管理能力：

### 项目管理
* **新建项目**：可以在 UI 侧边栏直接新建分类项目文件夹，配置自动保存。
* **移除项目**：可以从列表中移除项目配置。为防止误删，移除配置后会自动打开 Windows 文件夹供您手动确认是否删除真实的 DLL 文件。

### 插件更新与删除指南
* **热更新 (覆盖)**：如果您修改了代码并重新编译了 `.dll`，**完全不需要重启 Revit**。直接将新的 `.dll` 覆盖粘贴到对应的项目文件夹中，下次在面板点击运行该微工具时，就会自动加载并执行最新版本！
* **删除插件**：在微工具列表右侧选中某个插件后，点击界面上的 **[删除插件]** 按钮。这不仅会从列表中移除，还会**物理删除**该插件的 `.dll` 及其关联的配置文件（如 `.pdb` 等），且删除后无法恢复，请谨慎操作。
