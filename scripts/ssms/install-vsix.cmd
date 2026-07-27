@echo off
setlocal
set VSIX=%~dp0..\..\artifacts\SQLGuardian.Ssms.Extension.vsix
set INSTALLER=%ProgramFiles%\Microsoft SQL Server Management Studio 21\Release\Common7\IDE\VSIXInstaller.exe

if not exist "%VSIX%" (
  echo VSIX not found: %VSIX%
  echo Run scripts\ssms\pack-vsix.cmd first.
  exit /b 1
)

if not exist "%INSTALLER%" (
  echo SSMS 21 VSIXInstaller not found:
  echo   %INSTALLER%
  exit /b 1
)

echo Installing SQLGuardian into SSMS 21...
echo Close SSMS before continuing.
echo.
"%INSTALLER%" "%VSIX%"
exit /b %ERRORLEVEL%
