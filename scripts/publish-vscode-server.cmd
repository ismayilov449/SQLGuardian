@echo off
setlocal
cd /d "%~dp0\.."
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish-vscode-server.ps1" %*
exit /b %ERRORLEVEL%
