# BuildInstaller.ps1
# 由于网络限制，如果自动下载失败，请手动安装 Inno Setup 编译器

$ErrorActionPreference = "Stop"

# 尝试查找已安装的 Inno Setup
$isccPath = ""
$possiblePaths = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    "D:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "$PSScriptRoot\Tools\InnoSetup\ISCC.exe"
)

foreach ($path in $possiblePaths) {
    if (Test-Path $path) {
        $isccPath = $path
        break
    }
}

if ($isccPath -ne "") {
    Write-Host "Found Inno Setup at $isccPath" -ForegroundColor Green
    Write-Host "Compiling YangTools.iss..." -ForegroundColor Green
    Start-Process -FilePath $isccPath -ArgumentList "`"$PSScriptRoot\YangTools.iss`"" -Wait
    Write-Host "Done! Installer is generated in the Installer/ folder." -ForegroundColor Green
} else {
    Write-Host "==========================================================" -ForegroundColor Red
    Write-Host "未能找到 Inno Setup 编译器！" -ForegroundColor Red
    Write-Host "请前往官网下载安装：https://jrsoftware.org/isdl.php" -ForegroundColor Yellow
    Write-Host "安装完成后，您可以直接右键点击 YangTools.iss 选择 Compile，" -ForegroundColor Yellow
    Write-Host "或者再次运行此脚本。" -ForegroundColor Yellow
    Write-Host "==========================================================" -ForegroundColor Red
}
