@echo off
setlocal
cd /d "%~dp0"
set "CDN_ROOT=%~dp0."
if exist "%~dp0..\..\Assets\Zoo" set "CDN_ROOT=%~dp0..\..\..\cdn_server"
if not defined CDN_PYTHON if exist "%USERPROFILE%\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe" set "CDN_PYTHON=%USERPROFILE%\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe"
if not defined CDN_PYTHON set "CDN_PYTHON=python"
"%CDN_PYTHON%" -u "%~dp0cdn_server.py" -p 8080 -d "%CDN_ROOT%"
