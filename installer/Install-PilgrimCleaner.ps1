# Pilgrim Cleaner - Native Windows & Server 1-Click Installer
# Copyright (C) 2026 Pilgrim Software

[CmdletBinding()]
param (
    [switch]$Silent,
    [switch]$NoLaunch,
    [switch]$Machine
)

$ErrorActionPreference = "Stop"

$AppName = "Pilgrim Cleaner"
$AppVersion = "1.1.0"
$Publisher = "Pilgrim Software"
$ExeName = "PilgrimCleaner.exe"
$CliExeName = "pilgrim.exe"
$SourceDir = Split-Path -Parent $MyInvocation.MyCommand.Path

$IsAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
$UseMachineScope = $IsAdmin -or $Machine

if ($Machine -and -not $IsAdmin) {
    Write-Error "Machine-wide installation (-Machine) requires Administrator privileges."
    exit 3
}

$InstallDir = if ($UseMachineScope) {
    Join-Path $env:ProgramFiles "Pilgrim Cleaner"
} else {
    Join-Path $env:LOCALAPPDATA "Programs\Pilgrim Cleaner"
}

# If running from installer folder, check parent dist\PilgrimCleaner
if (-not (Test-Path (Join-Path $SourceDir $ExeName))) {
    $AltSource = Join-Path (Split-Path -Parent $SourceDir) "dist\PilgrimCleaner"
    if (Test-Path (Join-Path $AltSource $ExeName)) {
        $SourceDir = $AltSource
    }
}

if (-not (Test-Path (Join-Path $SourceDir $ExeName))) {
    Write-Error "Could not find $ExeName in $SourceDir. Please compile or publish the project first."
    exit 1
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   Installing $AppName (v$AppVersion)                     " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "Install Scope   : $(if ($UseMachineScope) { 'Machine-Wide (AppLocker/Server Compliant)' } else { 'Current User' })" -ForegroundColor Gray
Write-Host "Target Directory: $InstallDir" -ForegroundColor Gray

# 1. Terminate running instance if any
Get-Process -Name "PilgrimCleaner", "pilgrim", "LuminaCleaner" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

# 2. Create destination folder
if (-not (Test-Path $InstallDir)) {
    New-Item -Path $InstallDir -ItemType Directory -Force | Out-Null
}

# 3. Copy application binaries and assets
Write-Host "Copying application & CLI binaries..." -ForegroundColor Gray
$ResolvedSource = if (Test-Path $SourceDir) { (Resolve-Path $SourceDir).Path } else { $SourceDir }
$ResolvedTarget = if (Test-Path $InstallDir) { (Resolve-Path $InstallDir).Path } else { $InstallDir }
if ($ResolvedSource -ne $ResolvedTarget) {
    Copy-Item -Path "$SourceDir\*" -Destination $InstallDir -Recurse -Force
}

# 4. Add InstallDir to Windows PATH so 'pilgrim' CLI works globally in PowerShell / CMD
Write-Host "Registering 'pilgrim' CLI in PATH..." -ForegroundColor Gray
$PathTarget = if ($UseMachineScope) { [EnvironmentVariableTarget]::Machine } else { [EnvironmentVariableTarget]::User }
$CurrentPath = [Environment]::GetEnvironmentVariable("Path", $PathTarget)
if ([string]::IsNullOrWhiteSpace($CurrentPath)) { $CurrentPath = "" }
$PathEntries = $CurrentPath -split ';' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
if ($PathEntries -notcontains $InstallDir) {
    $NewPath = ($PathEntries + $InstallDir) -join ';'
    [Environment]::SetEnvironmentVariable("Path", $NewPath, $PathTarget)
}
if (($env:Path -split ';') -notcontains $InstallDir) {
    $env:Path = "$env:Path;$InstallDir"
}

# 5. Create Desktop & Start Menu Shortcuts via WScript.Shell
Write-Host "Creating Windows Shortcuts..." -ForegroundColor Gray
$WshShell = New-Object -ComObject WScript.Shell

$StartMenuRoot = if ($UseMachineScope) { [Environment]::GetFolderPath('CommonPrograms') } else { [Environment]::GetFolderPath('Programs') }
$StartMenuDir = Join-Path $StartMenuRoot "Pilgrim Cleaner"
if (-not (Test-Path $StartMenuDir)) {
    New-Item -Path $StartMenuDir -ItemType Directory -Force | Out-Null
}
$StartShortcutPath = Join-Path $StartMenuDir "$AppName.lnk"
$StartShortcut = $WshShell.CreateShortcut($StartShortcutPath)
$StartShortcut.TargetPath = Join-Path $InstallDir $ExeName
$StartShortcut.WorkingDirectory = $InstallDir
$StartShortcut.Description = "Pilgrim Cleaner - Windows Storage Cleaner, App Manager, Hardware Health & Sysadmin CLI"
$StartShortcut.IconLocation = "$(Join-Path $InstallDir $ExeName),0"
$StartShortcut.Save()

$DesktopDir = [Environment]::GetFolderPath('Desktop')
if (-not [string]::IsNullOrWhiteSpace($DesktopDir) -and (Test-Path $DesktopDir)) {
    $DesktopShortcutPath = Join-Path $DesktopDir "$AppName.lnk"
    $DesktopShortcut = $WshShell.CreateShortcut($DesktopShortcutPath)
    $DesktopShortcut.TargetPath = Join-Path $InstallDir $ExeName
    $DesktopShortcut.WorkingDirectory = $InstallDir
    $DesktopShortcut.Description = "Pilgrim Cleaner - Windows Storage Cleaner, App Manager, Hardware Health & Sysadmin CLI"
    $DesktopShortcut.IconLocation = "$(Join-Path $InstallDir $ExeName),0"
    $DesktopShortcut.Save()
}

# 6. Create Uninstaller script in InstallDir
$RegRoot = if ($UseMachineScope) { "HKLM" } else { "HKCU" }
$UninstallScript = @"
@echo off
title Uninstalling Pilgrim Cleaner...
echo Removing Pilgrim Cleaner from your computer...
taskkill /f /im PilgrimCleaner.exe 2>nul
taskkill /f /im pilgrim.exe 2>nul
taskkill /f /im LuminaCleaner.exe 2>nul
timeout /t 1 /nobreak >nul

:: Remove registry entry
reg delete "$RegRoot\Software\Microsoft\Windows\CurrentVersion\Uninstall\PilgrimCleaner" /f >nul 2>&1

:: Remove shortcuts
del "$StartShortcutPath" >nul 2>&1
rd "$StartMenuDir" >nul 2>&1
del "%USERPROFILE%\Desktop\Pilgrim Cleaner.lnk" >nul 2>&1

:: Remove installation folder
cd "%TEMP%"
start /b "" cmd /c "timeout /t 2 /nobreak >nul & rd /s /q `"$InstallDir`""

echo Pilgrim Cleaner was successfully uninstalled.
if not "%1"=="--silent" pause
"@
Set-Content -Path (Join-Path $InstallDir "Uninstall.bat") -Value $UninstallScript -Force

# 7. Register in Windows Settings / Installed Apps (Add or Remove Programs)
Write-Host "Registering in Windows Settings (Installed Apps)..." -ForegroundColor Gray
$UninstallKey = "${RegRoot}:\Software\Microsoft\Windows\CurrentVersion\Uninstall\PilgrimCleaner"
if (-not (Test-Path $UninstallKey)) {
    New-Item -Path $UninstallKey -Force | Out-Null
}
Set-ItemProperty -Path $UninstallKey -Name "DisplayName" -Value $AppName
Set-ItemProperty -Path $UninstallKey -Name "DisplayVersion" -Value $AppVersion
Set-ItemProperty -Path $UninstallKey -Name "Publisher" -Value $Publisher
Set-ItemProperty -Path $UninstallKey -Name "DisplayIcon" -Value (Join-Path $InstallDir $ExeName)
Set-ItemProperty -Path $UninstallKey -Name "InstallLocation" -Value $InstallDir
Set-ItemProperty -Path $UninstallKey -Name "UninstallString" -Value ("`"" + (Join-Path $InstallDir "Uninstall.bat") + "`"")
Set-ItemProperty -Path $UninstallKey -Name "QuietUninstallString" -Value ("`"" + (Join-Path $InstallDir "Uninstall.bat") + "`" --silent")
Set-ItemProperty -Path $UninstallKey -Name "NoModify" -Value 1 -Type DWord
Set-ItemProperty -Path $UninstallKey -Name "NoRepair" -Value 1 -Type DWord

Write-Host "==========================================================" -ForegroundColor Green
Write-Host "   $AppName v$AppVersion Successfully Installed!          " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "GUI App : $(Join-Path $InstallDir $ExeName)" -ForegroundColor White
Write-Host "CLI Tool: pilgrim (available in PowerShell / CMD)" -ForegroundColor Cyan

if (-not $NoLaunch -and -not $Silent) {
    Write-Host "Launching $AppName..." -ForegroundColor Cyan
    Start-Process -FilePath (Join-Path $InstallDir $ExeName)
}
