@echo off
chcp 65001 >nul
echo ========================================
echo   YangTools Revit Plugin - 部署
echo ========================================
echo.

:: 关闭 Revit
tasklist /FI "IMAGENAME eq Revit.exe" 2>NUL | find /I /N "Revit.exe" >NUL
if "%ERRORLEVEL%"=="0" (
    echo [WARN] Revit 正在运行中，请先关闭 Revit 再部署！
    echo.
    pause
    exit /b 1
)

set "APPDATA_ROAMING=%APPDATA%"
set "SRC_DIR=%~dp0deploy"

for %%v in (2022 2023 2024) do (
    if exist "%SRC_DIR%\%%v\YangTools.Revit.addin" (
        set "DEST=%APPDATA_ROAMING%\Autodesk\Revit\Addins\%%v"
        echo [INSTALL] Revit %%v --^> !DEST!
        
        if not exist "!DEST!" mkdir "!DEST!"
        
        :: 删除旧版本
        if exist "!DEST!\YangTools" (
            echo           删除旧版本...
            rmdir /s /q "!DEST!\YangTools"
        )
        
        :: 拷贝 .addin + YangTools 目录
        copy /Y "%SRC_DIR%\%%v\YangTools.Revit.addin" "!DEST!\" >nul
        xcopy /E /I /Y "%SRC_DIR%\%%v\YangTools" "!DEST!\YangTools" >nul
        
        echo           Done.
    ) else (
        echo [SKIP] Revit %%v - 没有部署文件
    )
)

echo.
echo ========================================
echo   部署完成！
echo ========================================
echo.
echo 启动 Revit 后应该能看到 YangTools 标签页。
echo.
pause
