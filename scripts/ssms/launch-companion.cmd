@echo off
REM Build + launch SQLGuardian for SSMS companion (standalone).
setlocal
set "ROOT=%~dp0..\.."
dotnet build "%ROOT%\src\SQLGuardian.Ssms\SQLGuardian.Ssms.csproj" -c Release
if errorlevel 1 exit /b 1
start "" "%ROOT%\src\SQLGuardian.Ssms\bin\Release\net9.0-windows\SQLGuardian.Ssms.exe" %*
