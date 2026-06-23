$releaseDir = 'E:\Antigravity\YANG TOOLS_REVIT\src\YangTools.Revit\bin\x64\Release'
$addinFile = 'E:\Antigravity\YANG TOOLS_REVIT\src\YangTools.Revit\YangTools.Revit.addin'
$outZip = 'E:\Antigravity\YANG TOOLS_REVIT\YangTools_Revit_Release_20260601.zip'

if (Test-Path $outZip) { Remove-Item $outZip }

$staging = 'E:\Antigravity\YANG TOOLS_REVIT\PackageStaging'
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
New-Item -ItemType Directory -Path $staging | Out-Null

Copy-Item -Path $releaseDir\* -Destination $staging -Recurse
Copy-Item -Path $addinFile -Destination $staging

Compress-Archive -Path "$staging\*" -DestinationPath $outZip
Remove-Item $staging -Recurse -Force
