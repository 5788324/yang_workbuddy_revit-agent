@echo off
chcp 65001 >nul
echo ========================================
echo   YangTools Installer Builder
echo   生成单文件 EXE 安装器
echo ========================================
echo.

:: 确保 deploy.zip 是最新的
echo [1/3] 更新部署包...
cd /d "%~dp0..\.."
if exist deploy.zip del deploy.zip
cd deploy
powershell -Command "Compress-Archive -Path 2022,2024 -DestinationPath ..\deploy.zip -Force"
cd ..
echo.

:: 编译安装器
echo [2/3] 编译安装器...
cd Installer\YangToolsInstaller
dotnet build -c Release -p:EnableWindowsTargeting=true
if %ERRORLEVEL% NEQ 0 (
    echo 编译失败！
    pause
    exit /b 1
)

:: 发布为单文件 EXE
echo [3/3] 发布单文件 EXE...
dotnet publish -c Release -p:EnableWindowsTargeting=true -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -o publish
if %ERRORLEVEL% NEQ 0 (
    echo 发布失败！
    pause
    exit /b 1
)

echo.
echo ========================================
echo   ✅ 完成！
echo   EXE 位置: Installer\YangToolsInstaller\publish\YangTools_Installer.exe
echo ========================================
echo.
echo 无需管理员权限。发给同事双击即可安装。
pause
