@echo off
REM SQLGuardian — SSMS External Tool: Analyze Active Script
REM Configure in SSMS: Tools → External Tools
REM   Title:   SQLGuardian: Analyze Active Script
REM   Command: <repo>\scripts\ssms\analyze-active-script.cmd
REM   Args:    $(ItemPath)
REM   Initial dir: $(ItemDir)
REM   Check: Use Output window  (optional — companion UI also opens)

setlocal
set "SCRIPT=%~1"
if "%SCRIPT%"=="" (
  echo No file path was passed. In SSMS External Tools, set Arguments to: $(ItemPath)
  pause
  exit /b 2
)

set "ROOT=%~dp0..\.."
set "SSMS_EXE=%ROOT%\src\SQLGuardian.Ssms\bin\Release\net9.0-windows\SQLGuardian.Ssms.exe"
set "CLI_DLL=%ROOT%\src\SQLGuardian.Cli\bin\Release\net9.0\sqlguardian.dll"

if not exist "%SSMS_EXE%" (
  echo Building SQLGuardian.Ssms...
  dotnet build "%ROOT%\src\SQLGuardian.Ssms\SQLGuardian.Ssms.csproj" -c Release
)

if exist "%SSMS_EXE%" (
  start "" "%SSMS_EXE%" --file "%SCRIPT%"
  exit /b 0
)

REM Fallback: CLI text report in the SSMS Output window
if not exist "%CLI_DLL%" (
  dotnet build "%ROOT%\src\SQLGuardian.Cli\SQLGuardian.Cli.csproj" -c Release
)

echo Running CLI fallback...
dotnet exec "%CLI_DLL%" analyze "%SCRIPT%" --format text --fail-on never
echo.
pause
exit /b %ERRORLEVEL%
