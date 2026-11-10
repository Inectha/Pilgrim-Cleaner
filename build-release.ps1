# Pilgrim Cleaner - Release Packaging Script (GUI + Sysadmin CLI + Winget)
# Copyright (C) 2026 Pilgrim Software

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
$Version = "1.1.0"
$DistDir = Join-Path $PSScriptRoot "dist"
$AppDir = Join-Path $DistDir "PilgrimCleaner"
$ZipFile = Join-Path $DistDir "PilgrimCleaner-v$Version-Windows-x64.zip"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   Packaging Pilgrim Cleaner v$Version Release Package    " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

if (Test-Path $AppDir) {
    Remove-Item $AppDir -Recurse -Force
}
New-Item -Path $AppDir -ItemType Directory -Force | Out-Null

# 1. Publish WPF GUI Application
Write-Host "1. Publishing WPF GUI Application (PilgrimCleaner.exe)..." -ForegroundColor Gray
& "$PSScriptRoot\dotnet.cmd" publish "$PSScriptRoot\PilgrimCleaner.csproj" -c Release -o $AppDir --no-restore --nologo

# 2. Publish Sysadmin CLI Application (pilgrim.exe) into same directory
Write-Host "2. Publishing Sysadmin CLI (pilgrim.exe)..." -ForegroundColor Gray
& "$PSScriptRoot\dotnet.cmd" publish "$PSScriptRoot\cli\PilgrimCli.csproj" -c Release -o $AppDir --no-restore --nologo

# 3. Copy Installer & Documentation
Write-Host "3. Adding installer scripts and documentation..." -ForegroundColor Gray
Copy-Item "$PSScriptRoot\installer\Install.bat" -Destination $AppDir -Force
Copy-Item "$PSScriptRoot\installer\Install-PilgrimCleaner.ps1" -Destination $AppDir -Force
Copy-Item "$PSScriptRoot\README.md" -Destination $AppDir -Force
Copy-Item "$PSScriptRoot\INSTALL.md" -Destination $AppDir -Force
Copy-Item "$PSScriptRoot\LICENSE" -Destination $AppDir -Force

# 4. Create ZIP Archive
if (Test-Path $ZipFile) {
    Remove-Item $ZipFile -Force
}
$OldRootZip = Join-Path $PSScriptRoot "PilgrimCleaner-v1.0.0-Windows-x64.zip"
if (Test-Path $OldRootZip) {
    Remove-Item $OldRootZip -Force
}

Write-Host "4. Compressing to $ZipFile..." -ForegroundColor Gray
Compress-Archive -Path "$AppDir\*" -DestinationPath $ZipFile -CompressionLevel Optimal
Copy-Item $ZipFile -Destination (Join-Path $PSScriptRoot "PilgrimCleaner-v$Version-Windows-x64.zip") -Force

# 5. Build Standalone Installer Wizard (PilgrimCleanerSetup.exe)
Write-Host "5. Building single-file installer wizard (PilgrimCleanerSetup.exe)..." -ForegroundColor Gray
$InstallerPublishDir = Join-Path $DistDir "installer"
if (-not (Test-Path $InstallerPublishDir)) {
    New-Item -Path $InstallerPublishDir -ItemType Directory -Force | Out-Null
}
& "$PSScriptRoot\dotnet.cmd" publish "$PSScriptRoot\installer\PilgrimInstaller.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o $InstallerPublishDir --no-restore --nologo
$SetupExePath = Join-Path $PSScriptRoot "PilgrimCleanerSetup.exe"
Copy-Item (Join-Path $InstallerPublishDir "PilgrimCleanerSetup.exe") -Destination $SetupExePath -Force

# 6. Update SHA256 in Winget Manifest
$WingetInstallerManifest = Join-Path $PSScriptRoot "winget\PilgrimSoftware.PilgrimCleaner.installer.yaml"
if ((Test-Path $SetupExePath) -and (Test-Path $WingetInstallerManifest)) {
    $SetupHash = (Get-FileHash -Path $SetupExePath -Algorithm SHA256).Hash.ToUpperInvariant()
    Write-Host "6. Updating Winget Manifest SHA256 ($SetupHash)..." -ForegroundColor Gray
    $ManifestContent = Get-Content -Path $WingetInstallerManifest -Raw
    $ManifestContent = $ManifestContent -replace 'InstallerSha256:\s*[0-9A-Fa-f]{64}', "InstallerSha256: $SetupHash"
    Set-Content -Path $WingetInstallerManifest -Value $ManifestContent -NoNewline
}

Write-Host "==========================================================" -ForegroundColor Green
Write-Host "   Release Package v$Version Built Successfully!          " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "Distribution Directory: $AppDir" -ForegroundColor White
Write-Host "Portable ZIP Archive:   $ZipFile" -ForegroundColor White
Write-Host "Setup Wizard Executable: $SetupExePath" -ForegroundColor White
Write-Host "Winget Manifests:        $(Join-Path $PSScriptRoot 'winget')" -ForegroundColor Cyan
