; ==============================================================================
; YangTools Revit 插件 Inno Setup 安装脚本
; 
; 用法：
; 1. 下载并安装 Inno Setup: https://jrsoftware.org/isdl.php
; 2. 右键点击本文件，选择 "Compile" 即可在 Installer 目录下生成 exe
; ==============================================================================

[Setup]
AppName=YangTools for Revit
AppVersion=2.0
AppPublisher=YangTools
AppCopyright=Copyright (C) 2026 YangTools
; 默认安装到当前用户的 AppData，不需要管理员权限
DefaultDirName={userappdata}\Autodesk\Revit\Addins
DefaultGroupName=YangTools
DisableProgramGroupPage=yes
; 卸载时显示的图标
UninstallDisplayIcon={app}\2025\YangTools\YangTools.Revit.dll
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=lowest
OutputDir=.\Installer
OutputBaseFilename=YangTools_Setup_v2.0
; 移除了可能不存在的 SetupIconFile 以防编译报错
; SetupIconFile=compiler:SetupClassicIcon.ico

[Tasks]
Name: "revit2021"; Description: "安装到 Revit 2021"; GroupDescription: "选择目标 Revit 版本:"
Name: "revit2022"; Description: "安装到 Revit 2022"; GroupDescription: "选择目标 Revit 版本:"
Name: "revit2023"; Description: "安装到 Revit 2023"; GroupDescription: "选择目标 Revit 版本:"
Name: "revit2024"; Description: "安装到 Revit 2024"; GroupDescription: "选择目标 Revit 版本:"
Name: "revit2025"; Description: "安装到 Revit 2025"; GroupDescription: "选择目标 Revit 版本:"; Flags: checked
Name: "revit2026"; Description: "安装到 Revit 2026"; GroupDescription: "选择目标 Revit 版本:"
Name: "revit2027"; Description: "安装到 Revit 2027"; GroupDescription: "选择目标 Revit 版本:"

[Files]
; ================= Deploy for Revit 2021 =================
Source: "src\YangTools.Revit\bin\x64\2021\*"; DestDir: "{app}\2021\YangTools"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: InstallRevit2021
Source: "src\YangTools.Revit\YangTools.Revit.addin"; DestDir: "{app}\2021"; Flags: ignoreversion; Check: InstallRevit2021

; ================= Deploy for Revit 2022 =================
Source: "src\YangTools.Revit\bin\x64\2022\*"; DestDir: "{app}\2022\YangTools"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: InstallRevit2022
Source: "src\YangTools.Revit\YangTools.Revit.addin"; DestDir: "{app}\2022"; Flags: ignoreversion; Check: InstallRevit2022

; ================= Deploy for Revit 2023 =================
Source: "src\YangTools.Revit\bin\x64\2023\*"; DestDir: "{app}\2023\YangTools"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: InstallRevit2023
Source: "src\YangTools.Revit\YangTools.Revit.addin"; DestDir: "{app}\2023"; Flags: ignoreversion; Check: InstallRevit2023

; ================= Deploy for Revit 2024 =================
Source: "src\YangTools.Revit\bin\x64\2024\*"; DestDir: "{app}\2024\YangTools"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: InstallRevit2024
Source: "src\YangTools.Revit\YangTools.Revit.addin"; DestDir: "{app}\2024"; Flags: ignoreversion; Check: InstallRevit2024

; ================= Deploy for Revit 2025 =================
Source: "src\YangTools.Revit\bin\x64\2025\*"; DestDir: "{app}\2025\YangTools"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: InstallRevit2025
Source: "src\YangTools.Revit\YangTools.Revit.addin"; DestDir: "{app}\2025"; Flags: ignoreversion; Check: InstallRevit2025

; ================= Deploy for Revit 2026 =================
Source: "src\YangTools.Revit\bin\x64\2026\*"; DestDir: "{app}\2026\YangTools"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: InstallRevit2026
Source: "src\YangTools.Revit\YangTools.Revit.addin"; DestDir: "{app}\2026"; Flags: ignoreversion; Check: InstallRevit2026

; ================= Deploy for Revit 2027 =================
Source: "src\YangTools.Revit\bin\x64\2027\*"; DestDir: "{app}\2027\YangTools"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: InstallRevit2027
Source: "src\YangTools.Revit\YangTools.Revit.addin"; DestDir: "{app}\2027"; Flags: ignoreversion; Check: InstallRevit2027

[Code]
function InstallRevit2021: Boolean;
begin
  Result := WizardIsTaskSelected('revit2021');
end;

function InstallRevit2022: Boolean;
begin
  Result := WizardIsTaskSelected('revit2022');
end;

function InstallRevit2023: Boolean;
begin
  Result := WizardIsTaskSelected('revit2023');
end;

function InstallRevit2024: Boolean;
begin
  Result := WizardIsTaskSelected('revit2024');
end;

function InstallRevit2025: Boolean;
begin
  Result := WizardIsTaskSelected('revit2025');
end;

function InstallRevit2026: Boolean;
begin
  Result := WizardIsTaskSelected('revit2026');
end;

function InstallRevit2027: Boolean;
begin
  Result := WizardIsTaskSelected('revit2027');
end;

procedure InitializeWizard;
begin
  WizardForm.WelcomeLabel1.Caption := '欢迎使用 YangTools Revit 插件安装程序';
  WizardForm.WelcomeLabel2.Caption := '此程序将在您的电脑上安装 YangTools，支持 Revit 2021 到 2027 的所有版本。' + #13#10#13#10 + '安装不需要管理员权限。';
end;
