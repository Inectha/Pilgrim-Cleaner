# Pilgrim Cleaner - Local Release Build
# Copyright (C) 2026 Pilgrim Software
#
# Builds the same artifacts as the release workflow (.github/workflows/build-and-release.yml),
# for testing a release on your own machine. Official releases are made by pushing a tag:
#   git tag v1.2.0
#   git push origin v1.2.0
#
# Usage:  .\build-release.ps1                 (version from Directory.Build.props)
#         .\build-release.ps1 -Version 1.2.0
# Output: dist\PilgrimCleaner\                 published GUI + CLI
#         dist\PilgrimCleaner-v<ver>-Windows-x64.zip
#         dist\installer\PilgrimCleanerSetup.exe   (only if Inno Setup 6.3+ is installed)

[CmdletBinding()]
param (
    [string]$Version,
    # Fail instead of skipping when Inno Setup is not installed (used by CI and the release workflow).
    [switch]$RequireInstaller
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

if (-not $Version) {
    [xml]$props = Get-Content (Join-Path $PSScriptRoot "Directory.Build.props")
    $Version = $props.Project.PropertyGroup.Version
}
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version must look like 1.2.0 (got '$Version')."
}

$DistDir = Join-Path $PSScriptRoot "dist"
$AppDir = Join-Path $DistDir "PilgrimCleaner"
$ZipFile = Join-Path $DistDir "PilgrimCleaner-v$Version-Windows-x64.zip"

Write-Host "Packaging Pilgrim Cleaner v$Version" -ForegroundColor Cyan

if (Test-Path $DistDir) {
    Remove-Item $DistDir -Recurse -Force
}
New-Item -Path $AppDir -ItemType Directory -Force | Out-Null

# 1. Publish GUI and CLI (framework-dependent, sharing one folder)
foreach ($project in "PilgrimCleaner.csproj", "cli\PilgrimCli.csproj") {
    Write-Host "Publishing $project..." -ForegroundColor Gray
    & "$PSScriptRoot\dotnet.cmd" publish (Join-Path $PSScriptRoot $project) -c Release -r win-x64 --self-contained false "-p:Version=$Version" -o $AppDir --nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish $project failed" }
}
Copy-Item (Join-Path $PSScriptRoot "README.md"), (Join-Path $PSScriptRoot "LICENSE") -Destination $AppDir -Force
foreach ($exe in "PilgrimCleaner.exe", "pilgrim.exe") {
    if (-not (Test-Path (Join-Path $AppDir $exe))) { throw "$exe missing from publish output" }
}

# 2. Portable ZIP
Write-Host "Creating $ZipFile..." -ForegroundColor Gray
Compress-Archive -Path "$AppDir\*" -DestinationPath $ZipFile -CompressionLevel Optimal

# 3. Inno Setup installer (optional locally)
$iscc = Get-ChildItem "${env:ProgramFiles(x86)}\Inno Setup *\ISCC.exe", "$env:ProgramFiles\Inno Setup *\ISCC.exe" -ErrorAction SilentlyContinue |
    Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
if ($iscc) {
    Write-Host "Compiling installer with $iscc..." -ForegroundColor Gray
    & $iscc "/DMyAppVersion=$Version" (Join-Path $PSScriptRoot "installer\PilgrimCleaner.iss")
    if ($LASTEXITCODE -ne 0) { throw "ISCC failed with exit code $LASTEXITCODE" }
    if (-not (Test-Path (Join-Path $DistDir "installer\PilgrimCleanerSetup.exe"))) { throw "PilgrimCleanerSetup.exe was not created" }
} elseif ($RequireInstaller) {
    throw "Inno Setup (ISCC.exe) not found."
} else {
    Write-Host "Inno Setup not found: skipping PilgrimCleanerSetup.exe (install Inno Setup 6.3+ to build it)." -ForegroundColor Yellow
}

Write-Host "Done. Output in $DistDir" -ForegroundColor Green
