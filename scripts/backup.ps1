$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path "$PSScriptRoot\..").Path
$versionsDir = Join-Path $projectRoot "Versions"

if (-not (Test-Path $versionsDir)) {
    New-Item -ItemType Directory -Path $versionsDir | Out-Null
}

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$zipFileName = "YangTools_Backup_$timestamp.zip"
$zipFilePath = Join-Path $versionsDir $zipFileName

Write-Host "[Backup] 开始打包备份项目..." -ForegroundColor Cyan

$tempDir = Join-Path $env:TEMP "YangTools_Temp_$timestamp"
New-Item -ItemType Directory -Path $tempDir | Out-Null

Copy-Item -Path "$projectRoot\*" -Destination $tempDir -Recurse -Exclude @("bin", "obj", ".vs", "Versions", "TestResults", "Packages", ".git")

Compress-Archive -Path "$tempDir\*" -DestinationPath $zipFilePath -Force

Remove-Item -Path $tempDir -Recurse -Force

Write-Host "[Backup] 备份成功！文件保存在: $zipFilePath" -ForegroundColor Green
