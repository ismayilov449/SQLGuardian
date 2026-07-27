@echo off
REM SQLGuardian — SSMS External Tool: Analyze Folder of Scripts
REM Use after scripting objects from Object Explorer into a folder.
REM   Title:   SQLGuardian: Analyze Script Folder
REM   Command: <repo>\scripts\ssms\analyze-folder.cmd
REM   Args:    $(ItemDir)
REM   Or leave Args empty and pick a folder in the UI.

setlocal
set "FOLDER=%~1"
set "ROOT=%~dp0..\.."
set "SSMS_EXE=%ROOT%\src\SQLGuardian.Ssms\bin\Release\net9.0-windows\SQLGuardian.Ssms.exe"

if not exist "%SSMS_EXE%" (
  dotnet build "%ROOT%\src\SQLGuardian.Ssms\SQLGuardian.Ssms.csproj" -c Release
)

if "%FOLDER%"=="" (
  start "" "%SSMS_EXE%"
) else (
  start "" "%SSMS_EXE%" --folder "%FOLDER%"
)
exit /b 0
