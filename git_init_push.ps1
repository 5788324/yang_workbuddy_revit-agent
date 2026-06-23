# ============================================
# YangTools Git 初始化与推送脚本
# 请先安装 Git: https://git-scm.com/download/win
# 安装后重启终端，然后在项目根目录运行此脚本
# ============================================

# 配置用户信息（首次使用 Git 需要设置）
# git config --global user.name "你的名字"
# git config --global user.email "你的邮箱"

# 进入项目目录
Set-Location "E:\Antigravity\YANG TOOLS_REVIT"

# 初始化 Git 仓库
git init

# 添加远程仓库
git remote add origin https://github.com/5788324/YANG-TOOLS_REVIT.git

# 添加所有文件（.gitignore 会自动过滤不需要的文件）
git add .

# 首次提交
git commit -m "初始提交: YangTools Revit 插件 v0.1

- 核心框架: RibbonBuilder 自动注册, App.cs 入口
- 功能: 从CAD粘贴, 标注文本替换, 项目中文检查, 个人助手
- 主题: 长安暖色调 (唐朝古城风格)
- 图标: 简约铜色线条风格 5 套
- 仅编译 Revit 2025 (net8.0-windows)
- 自动部署至 AppData 用户目录"

# 推送到 GitHub
git branch -M main
git push -u origin main

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host " 推送完成! 代码已备份至 GitHub" -ForegroundColor Green
Write-Host " https://github.com/5788324/YANG-TOOLS_REVIT" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Green
