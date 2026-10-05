@echo off
setlocal EnableExtensions
chcp 65001 >nul 2>&1
cd /d "%~dp0"

where unity >nul 2>&1
if errorlevel 1 (
    echo [错误] 未找到 unity CLI，请先安装并加入 PATH。
    exit /b 1
)

set "PROJECT_ROOT=%CD%"

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0StartUnity.ps1" -ProjectRoot "%PROJECT_ROOT%"
exit /b %ERRORLEVEL%
