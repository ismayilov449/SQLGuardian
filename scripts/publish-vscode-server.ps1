# Publish Language Server into the VS Code extension folder.
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\SQLGuardian.LanguageServer\SQLGuardian.LanguageServer.csproj"
$outDir = Join-Path $root "extensions\vscode\server"

Write-Host "Building Language Server (Release)..."
dotnet publish $project -c Release -o $outDir --self-contained false

Write-Host "Published to $outDir"
Get-ChildItem $outDir -Filter "SQLGuardian.LanguageServer.dll" | ForEach-Object { Write-Host "  $($_.FullName)" }
