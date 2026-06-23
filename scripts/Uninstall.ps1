# ==============================================================================
# YangTools Revit Plugin - 非管理员一键卸载脚本
# ==============================================================================
# 作用：从当前用户的 AppData 目录中安全清除已部署的插件及描述文件，恢复干净环境。
# 权限要求：不需要管理员权限
# ==============================================================================

$ErrorActionPreference = "Continue"

Write-Host "=========================================" -ForegroundColor Magenta
Write-Host "  YangTools Revit 插件非管理员一键卸载脚本" -ForegroundColor Magenta
Write-Host "=========================================" -ForegroundColor Magenta

# 1. 定义用户 Revit 插件目录路径
$UserAppData = $env:APPDATA
$RevitAddinsRoot = Join-Path $UserAppData "Autodesk\Revit\Addins"
$TargetVersions = @("2021", "2022", "2023", "2024", "2025", "2026", "2027")

Write-Host "`n正在从 AppData 路径中查找并清除插件..." -ForegroundColor Yellow

$ClearedCount = 0

foreach ($Version in $TargetVersions) {
    $VersionAddinsDir = Join-Path $RevitAddinsRoot $Version
    
    $AddinFilePath = Join-Path $VersionAddinsDir "YangTools.Revit.addin"
    $YangToolsDir = Join-Path $VersionAddinsDir "YangTools"

    # 清理 .addin 清单文件
    if (Test-Path $AddinFilePath) {
        Remove-Item -Path $AddinFilePath -Force
        Write-Host "  ✔ 已清除 Revit $Version 的 Addin 清单文件。" -ForegroundColor Green
        $ClearedCount++
    }

    # 清理 YangTools DLL 文件夹
    if (Test-Path $YangToolsDir) {
        Remove-Item -Path $YangToolsDir -Recurse -Force
        Write-Host "  ✔ 已清除 Revit $Version 的 DLL 二进制目录。" -ForegroundColor Green
        $ClearedCount++
    }
}

# 2. 卸载状态汇报
Write-Host "`n=========================================" -ForegroundColor Magenta
if ($ClearedCount -gt 0) {
    Write-Host " 卸载流程已成功完成！已将所有相关文件干净清除。" -ForegroundColor Green
} else {
    Write-Host " 未在您的电脑中检测到已安装的 YangTools 插件文件，无需清理。" -ForegroundColor Yellow
}
Write-Host "=========================================" -ForegroundColor Magenta
