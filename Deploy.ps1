# YangTools Revit Plugin - 部署脚本
# 自动安装到 Revit 2022/2023/2024 的 Addins 目录
# 请以管理员身份运行此脚本

param(
    [string[]]$Versions = @("2022", "2023", "2024")
)

$ErrorActionPreference = "Stop"
$AppData = [Environment]::GetFolderPath("ApplicationData")
$SourceRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  YangTools Revit Plugin - Deploy" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 检查 Revit 是否运行
$revitRunning = Get-Process "Revit" -ErrorAction SilentlyContinue
if ($revitRunning) {
    Write-Host "[WARN] Revit is running. Please close Revit before deploying." -ForegroundColor Yellow
    Write-Host "       Press Y to continue anyway, or any other key to exit."
    $key = Read-Host
    if ($key -ne 'Y' -and $key -ne 'y') { exit 0 }
}

foreach ($ver in $Versions) {
    $srcDir = Join-Path $SourceRoot "deploy\$ver"
    if (-not (Test-Path $srcDir)) {
        Write-Host "[SKIP] Revit $ver - no deploy files found." -ForegroundColor Gray
        continue
    }

    $addinDir = Join-Path $AppData "Autodesk\Revit\Addins\$ver"
    Write-Host "[INSTALL] Revit $ver -> $addinDir" -ForegroundColor Green

    # 确保目标目录存在
    New-Item -ItemType Directory -Force -Path $addinDir | Out-Null

    # 先清理旧的 YangTools 目录 (以防 DLL 版本冲突)
    $yangDir = Join-Path $addinDir "YangTools"
    if (Test-Path $yangDir) {
        Write-Host "          Removing old YangTools folder..."
        Remove-Item -Recurse -Force $yangDir -ErrorAction SilentlyContinue
    }

    # 拷贝所有文件
    Copy-Item -Recurse -Force "$srcDir\*" $addinDir

    Write-Host "          Done." -ForegroundColor Green
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Deploy Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Start Revit and you should see the 'YangTools' tab in the Ribbon."
Write-Host "If Revit was running, restart it now."
Write-Host ""
Read-Host "Press Enter to exit"
