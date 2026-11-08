# Pilgrim Cleaner - Release Packaging Script
# Copyright (C) 2026 Pilgrim Software

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
$Version = "1.0.0"
$DistDir = Join-Path $PSScriptRoot "dist"
$AppDir = Join-Path $DistDir "PilgrimCleaner"
$ZipFile = Join-Path $DistDir "PilgrimCleaner-v$Version-Windows-x64.zip"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   Packaging Pilgrim Cleaner v$Version Release Package    " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Publish Application
Write-Host "Publishing Application binaries (Release)..." -ForegroundColor Gray
& "$PSScriptRoot\dotnet.cmd" publish "$PSScriptRoot\PilgrimCleaner.csproj" -c Release -o $AppDir --nologo

# 2. Copy Installer & Documentation
Write-Host "Adding installer scripts and documentation..." -ForegroundColor Gray
Copy-Item "$PSScriptRoot\installer\Install.bat" -Destination $AppDir -Force
Copy-Item "$PSScriptRoot\installer\Install-PilgrimCleaner.ps1" -Destination $AppDir -Force
Copy-Item "$PSScriptRoot\README.md" -Destination $AppDir -Force
Copy-Item "$PSScriptRoot\LICENSE" -Destination $AppDir -Force

# 3. Create ZIP Archive
if (Test-Path $ZipFile) {
    Remove-Item $ZipFile -Force
}

Write-Host "Compressing to $ZipFile..." -ForegroundColor Gray
Compress-Archive -Path "$AppDir\*" -DestinationPath $ZipFile -CompressionLevel Optimal
Copy-Item $ZipFile -Destination (Join-Path $PSScriptRoot "PilgrimCleaner-v$Version-Windows-x64.zip") -Force

# 4. Build Standalone Installer Wizard (PilgrimCleanerSetup.exe)
Write-Host "Building single-file installer wizard (PilgrimCleanerSetup.exe)..." -ForegroundColor Gray
$InstallerPublishDir = Join-Path $DistDir "installer"
if (-not (Test-Path $InstallerPublishDir)) {
    New-Item -Path $InstallerPublishDir -ItemType Directory -Force | Out-Null
}
& "$PSScriptRoot\dotnet.cmd" publish "$PSScriptRoot\installer\PilgrimInstaller.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o $InstallerPublishDir --nologo
Copy-Item (Join-Path $InstallerPublishDir "PilgrimCleanerSetup.exe") -Destination (Join-Path $PSScriptRoot "PilgrimCleanerSetup.exe") -Force

Write-Host "==========================================================" -ForegroundColor Green
Write-Host "   Release Package Built Successfully!                    " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "Distribution Directory: $AppDir" -ForegroundColor White
Write-Host "Portable ZIP Archive:   $ZipFile" -ForegroundColor White
Write-Host "Setup Wizard Executable: $(Join-Path $PSScriptRoot 'PilgrimCleanerSetup.exe')" -ForegroundColor White
