$ErrorActionPreference = "Stop"

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  YangTools Revit Deployment Script" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$ProjectDir = Resolve-Path (Join-Path $ScriptDir "..")
$CsprojPath = Join-Path $ProjectDir "src\YangTools.Revit\YangTools.Revit.csproj"

if (-not (Test-Path $CsprojPath)) {
    Write-Error "Error: Cannot find $CsprojPath"
}

$TargetVersions = @("2021", "2022", "2023", "2024", "2025", "2026", "2027")

Write-Host "`n[1/2] Building and deploying with dotnet CLI..." -ForegroundColor Yellow

foreach ($Version in $TargetVersions) {
    Write-Host "  -> Processing Revit $Version ..." -ForegroundColor Cyan
    try {
        dotnet build $CsprojPath -c $Version -p:Platform=x64
        Write-Host "  [SUCCESS] Revit $Version compiled and deployed!" -ForegroundColor Green
    } catch {
        Write-Error "Failed to compile for Revit $Version"
    }
}

Write-Host "`n[2/2] Deployment Complete!" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Cyan
