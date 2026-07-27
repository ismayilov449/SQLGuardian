# Pack SQLGuardian SSMS extension with VsixUtil (VSIX v3: catalog.json + manifest.json).
# Usage: powershell -File scripts\ssms\pack-vsix.ps1

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$extProj = Join-Path $root "src\SQLGuardian.Ssms.Extension\SQLGuardian.Ssms.Extension.csproj"
$outDir = Join-Path $root "src\SQLGuardian.Ssms.Extension\bin\Debug\net472"
$stage = Join-Path $root "artifacts\ssms-vsix-stage"
$vsix = Join-Path $root "artifacts\SQLGuardian.Ssms.Extension.vsix"
$filesJson = Join-Path $stage "files.json"
$manifestSrc = Join-Path $root "src\SQLGuardian.Ssms.Extension\source.extension.vsixmanifest"
$manifestStage = Join-Path $stage "extension.vsixmanifest"

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
if (-not $msbuild) { throw "MSBuild not found. Install Visual Studio 2022." }

$vsixUtil = Join-Path $env:USERPROFILE ".nuget\packages\microsoft.vssdk.buildtools\17.14.2094\tools\VSSDK\bin\VsixUtil.exe"
if (-not (Test-Path $vsixUtil)) {
    # Resolve from restored package after build
    $vsixUtil = Get-ChildItem "$env:USERPROFILE\.nuget\packages\microsoft.vssdk.buildtools" -Recurse -Filter VsixUtil.exe |
        Sort-Object FullName -Descending |
        Select-Object -First 1 -ExpandProperty FullName
}
if (-not $vsixUtil -or -not (Test-Path $vsixUtil)) {
    throw "VsixUtil.exe not found. Restore Microsoft.VSSDK.BuildTools."
}

Write-Host "Building extension..."
& $msbuild $extProj /t:Rebuild /p:Configuration=Debug /v:m /nologo /restore
if ($LASTEXITCODE -ne 0) { throw "Extension build failed." }

$dll = Join-Path $outDir "SQLGuardian.Ssms.Extension.dll"
if (-not (Test-Path $dll)) { throw "Extension DLL missing at $outDir" }

Write-Host "Staging VSIX contents..."
Remove-Item -Recurse -Force $stage -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $stage | Out-Null

Copy-Item $dll $stage
Copy-Item (Join-Path $root "src\SQLGuardian.Ssms.Extension\SQLGuardian.Ssms.Extension.pkgdef") $stage -Force

$icon = Join-Path $root "src\SQLGuardian.Ssms.Extension\Resources\Icon.png"
if (-not (Test-Path $icon)) { throw "Extension icon missing at $icon" }
Copy-Item $icon (Join-Path $stage "Icon.png") -Force

$license = Join-Path $root "src\SQLGuardian.Ssms.Extension\LICENSE.txt"
if (-not (Test-Path $license)) { throw "Extension license missing at $license" }
Copy-Item $license (Join-Path $stage "LICENSE.txt") -Force

# Manifest without XML declaration (matches VSSDK detokenized output).
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
$manifestText = [System.IO.File]::ReadAllText($manifestSrc)
$manifestText = [regex]::Replace($manifestText, '^\s*<\?xml[^>]*\?>\s*', '')
[System.IO.File]::WriteAllText($manifestStage, $manifestText.TrimStart(), $utf8NoBom)

Get-ChildItem $outDir -Filter "*.dll" | Where-Object { $_.Name -ne "SQLGuardian.Ssms.Extension.dll" } | ForEach-Object {
    Copy-Item $_.FullName $stage
}

$cliSrc = Join-Path $outDir "tools\cli"
if (Test-Path $cliSrc) {
    $cliDest = Join-Path $stage "tools\cli"
    New-Item -ItemType Directory -Force -Path $cliDest | Out-Null
    Get-ChildItem $cliSrc -File | Where-Object {
        $_.Extension -in '.dll', '.exe', '.json' -and $_.Name -notmatch '\.resources\.dll$'
    } | ForEach-Object {
        Copy-Item $_.FullName $cliDest -Force
    }
    $sni = Join-Path $cliSrc "runtimes\win-x64\native\Microsoft.Data.SqlClient.SNI.dll"
    if (Test-Path $sni) {
        $nativeDest = Join-Path $cliDest "runtimes\win-x64\native"
        New-Item -ItemType Directory -Force -Path $nativeDest | Out-Null
        Copy-Item $sni $nativeDest -Force
    }
}

# files.json for VsixUtil
$fileEntries = @()
Get-ChildItem -LiteralPath $stage -Recurse -File | Where-Object { $_.Name -ne 'files.json' -and $_.Name -ne 'extension.vsixmanifest' } | ForEach-Object {
    $rel = $_.FullName.Substring($stage.Length).TrimStart('\', '/')
    $relUnix = $rel.Replace('\', '/')
    $dir = [System.IO.Path]::GetDirectoryName($rel) # Windows separators
    if ([string]::IsNullOrEmpty($dir)) { $dir = "" } else { $dir = $dir.Replace('/', '\') }
    $fileEntries += @{
        culture     = ""
        installRoot = ""
        ngen        = $null
        path        = $_.FullName
        targetPath  = $_.Name
        vsixSubPath = $dir
    }
}

$filesObj = @{ files = $fileEntries }
$json = ConvertTo-Json -InputObject $filesObj -Depth 6 -Compress
[System.IO.File]::WriteAllText($filesJson, $json, $utf8NoBom)

New-Item -ItemType Directory -Force -Path (Split-Path $vsix) | Out-Null
Remove-Item -Force $vsix -ErrorAction SilentlyContinue

Write-Host "Packing with VsixUtil (VSIX v3)..."
$toolsRoot = Split-Path (Split-Path $vsixUtil -Parent) -Parent  # .../tools/VSSDK
$schemaDir = Join-Path $toolsRoot "schemas"
if (-not (Test-Path (Join-Path $schemaDir "PackageManifestSchema.xsd"))) {
    $schemaDir = Join-Path (Split-Path $toolsRoot -Parent) "vssdk\schemas"
}
$env:VsSDKSchemaDir = $schemaDir
$env:VsSDKToolsPath = Split-Path $vsixUtil -Parent
Write-Host "VsSDKSchemaDir=$env:VsSDKSchemaDir"

& $vsixUtil package `
    -sourceManifest $manifestStage `
    -outputPath $vsix `
    -files $filesJson `
    -compressionLevel NotCompressed `
    -is64BitBuild
if ($LASTEXITCODE -ne 0) { throw "VsixUtil package failed with exit $LASTEXITCODE" }

# Validate v3 parts exist
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($vsix)
try {
    foreach ($required in @('extension.vsixmanifest', 'manifest.json', 'catalog.json', '[Content_Types].xml')) {
        if (-not $zip.GetEntry($required)) {
            throw "Packed VSIX missing required entry: $required"
        }
    }
}
finally {
    $zip.Dispose()
}

$sizeMb = [math]::Round((Get-Item $vsix).Length / 1MB, 2)
Write-Host ""
Write-Host "VSIX ready: $vsix ($sizeMb MB)"
Write-Host ""
Write-Host "Install (close SSMS first):"
Write-Host "  scripts\ssms\install-vsix.cmd"
Write-Host "Or:"
Write-Host ('  & "{0}" "{1}"' -f `
    "${env:ProgramFiles}\Microsoft SQL Server Management Studio 21\Release\Common7\IDE\VSIXInstaller.exe", `
    $vsix)
