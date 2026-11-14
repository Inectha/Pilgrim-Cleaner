# Changelog — Pilgrim Cleaner

All notable changes to **Pilgrim Cleaner** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.2.2] — 2026-11-14

### Added
- **In-App Updater (`Services/UpdateService`):**
  - Automatically checks GitHub Releases on startup and displays a one-click **Update & Restart** pill in the title bar, sidebar, and *About Pilgrim* view when a newer version is available.
  - Verifies the downloaded `PilgrimCleanerSetup.exe` against `SHA256SUMS.txt` before execution, runs the silent installer (`/relaunch=1`), and restarts Pilgrim Cleaner automatically.

### Changed
- **Branding & Icons:**
  - Updated `Assets/app.ico` (multi-resolution 16–256 px), `Assets/DesktopIcon.png`, `Assets/PilgrimIconP.png`, and `Controls/PilgrimLogoControl.cs` to render the new Material 3 `Pcl` periodic-element icon across the title bar, *About Pilgrim* view, taskbar, and installer.
  - Updated `installer/PilgrimCleaner.iss` to embed `Assets/app.ico`, refresh existing desktop shortcuts on upgrade, and flush the Windows Explorer icon cache (`ie4uinit.exe -show`).

---

## [1.2.0] — 2026-11-11

### Security
- **Junction / symlink protection for all cleanup modules (`Interop/SafeFileOps`):**
  - A standard user could point a scanned folder (e.g. a browser cache) at another location with `mklink /J` and make an elevated or SYSTEM cleanup delete files there.
  - Scan roots that are, or sit below, a junction/symlink not created by SYSTEM, Administrators or TrustedInstaller are now refused.
  - Every file is deleted through a handle after verifying it still lives where the scan found it, which also blocks swapping a folder for a junction after the scan.
- **App Manager:**
  - Uninstall commands from `HKEY_CURRENT_USER` are no longer started "as administrator"; they run as the user, and are refused when Pilgrim Cleaner itself runs elevated.
  - Leftover folders are only offered once the app is really gone from the registry, never pre-selected while it is still installed, and never include shared folders (`Common Files`, `Microsoft`, …), folders used by other apps, or registry values containing path tricks.
- **Windows tools are started from System32 by full path:** `powershell.exe`, `schtasks.exe`, `dism.exe`, `explorer.exe` and `taskschd.msc`, so a planted copy in the working directory cannot run as SYSTEM.
- **Driver install fallback:** update titles are passed to PowerShell as Base64 via `-EncodedCommand` and can no longer inject code.

### Fixed
- **Duplicates:** overlapping scan folders could make a file a "duplicate" of itself and recycle the only copy. Paths are now de-duplicated, and a copy is only removed when a different kept file exists.
- **Recycle Bin:**
  - On network shares and USB drives, "Move to Recycle Bin" silently deleted permanently; those paths are now skipped, and the GUI warns before any permanent delete.
  - The Recycle Bin size was always reported as 0 on 64-bit Windows (wrong struct layout).
- **Uninstall:**
  - A cancelled or failed uninstall was reported as successful; exit codes are now checked.
  - The old `Uninstall.bat` split the install path at spaces and never removed the install folder.
- **CLI:**
  - `clean --json` no longer deletes files without `--force`.
  - `duplicates --recycle` now asks for confirmation.
  - An invalid `--keep` value is now an error.
- `--dry-run` no longer removes empty folders in the Delivery Optimization module.
- The PowerShell driver-install fallback could never run (syntax error).

### Changed
- **.NET 10 LTS:** all projects target `net10.0-windows` (.NET 8 reached end of support on 10 Nov 2026). Requires the .NET 10 Desktop Runtime.
- **New installer (Inno Setup):**
  - Machine-wide only, in `C:\Program Files\Pilgrim Cleaner`.
  - Checks for the .NET 10 Desktop Runtime.
  - Adds `pilgrim` to the system PATH and removes it again on uninstall.
  - Removes the background maintenance task on uninstall.
  - Silent install: `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART`.
  - Replaces the previous custom setup wizard and the `Install.bat` / `Install-PilgrimCleaner.ps1` scripts.
- **Winget:** the manifest now uses the Inno Setup installer with machine scope and depends on `Microsoft.DotNet.DesktopRuntime.10`.
- **Version number in one place** (`Directory.Build.props`, overridden by the release tag). The GUI badge, About page and `pilgrim` read it from the build.

### Added
- **CI on every pull request:** builds GUI, CLI and tests, runs the test suite, and builds the installer.
- **Automated releases:** pushing a `vX.Y.Z` tag builds, tests and publishes `PilgrimCleanerSetup.exe`, the portable ZIP and `SHA256SUMS.txt`, plus ready-to-submit winget manifests.
- **`AGENTS.md`:** shared project and safety rules for contributors and AI coding agents (Antigravity, Claude Code, Cursor).
- **9 new tests (30 in total)**, including tests that plant real junctions and verify nothing outside the scanned folder is deleted.

### Upgrade notes
- Installing 1.2.0 over a machine-wide 1.1 install replaces it and removes the old uninstall entry. A **per-user** 1.1 install (`%LOCALAPPDATA%\Programs\Pilgrim Cleaner`) is not touched; uninstall it first via *Settings → Apps*.

---

## [1.1.0] — 2026-11-10

### Added
- **Sysadmin CLI (`pilgrim.exe`) & Windows Server Edition (`cli/PilgrimCli.csproj`):**
  - Native Windows Console Subsystem binary (`pilgrim.exe`) supporting interactive terminal tables, ANSI color themes, and headless execution in PowerShell, SSH, WinRM, Intune, and RMM tools.
  - Subcommands: `pilgrim audit`, `pilgrim clean`, `pilgrim status`, `pilgrim modules`, and `pilgrim man [topic]`.
  - Structured JSON output (`--json`) for seamless PowerShell pipeline integration (`ConvertFrom-Json`).
  - Built-in offline Sysadmin Terminal Manual (`pilgrim man overview|profiles|security|automation|exitcodes`).
- **Execution Profiles (`--profile`):**
  - `server-safe`: Targets `system_temp`, `crash_dumps`, `delivery_optimization`, and `server_logs` while skipping browser and developer caches.
  - `rds`: Designed for multi-session Terminal Servers (RDS / Citrix / AVD); automatically enables `--all-profiles`.
  - `workstation`: Standard endpoint cleanup profile.
- **Multi-User Profile Enumeration (`--all-profiles` & `SYSTEM` Context):**
  - `UserProfileResolver` safely enumerates all user profiles in `C:\Users\*` (excluding `Public`, `Default`, and NTFS ReparsePoints/junctions) when running elevated or under `NT AUTHORITY\SYSTEM`.
  - Multi-SID `$Recycle.Bin` enumeration when running elevated with `--all-profiles`.
- **Windows Server & CBS Archived Logs Module (`server_logs`):**
  - Safely audits and purges rotated IIS Web Server logs (`%SystemDrive%\inetpub\logs\LogFiles\W3SVC*`) and archived Windows Component-Based Servicing (`CbsPersist_*`) logs older than the configurable retention window (default 14 days).
- **Windows Event Log Integration (`--event-log`):**
  - Writes structured operational events (IDs `1000`–`1003`, Source `PilgrimCleaner`) to the Windows Application Event Log for enterprise RMM monitoring.
- **Enterprise Machine-Wide Installer & Winget Manifests:**
  - `PilgrimCleanerSetup.exe` and `Install-PilgrimCleaner.ps1` now automatically install to `C:\Program Files\Pilgrim Cleaner` (AppLocker/WDAC compliant) when run as Administrator, register `pilgrim.exe` in `PATH`, and support silent `/S` installation.
  - Added multi-file Winget package manifests (`winget/PilgrimSoftware.PilgrimCleaner*.yaml`).

### Changed & Fixed (Security & Engine Audit)
- **EDR & Ransomware Heuristic Prevention (`CleanerModuleBase.SafeDeleteFile`):**
  - Removed pre-delete `FileStream(..., FileAccess.ReadWrite, FileShare.None)` lock probing which doubled I/O syscalls and could trigger behavioral ransomware alerts in enterprise EDR sensors (CrowdStrike, Defender for Endpoint). File deletion now uses direct atomic unlink with native kernel sharing-violation handling.
- **Universal 24-Hour Age Safety Buffer:**
  - Enforced `EngineOptions.AgeSafetyBuffer` (default 24h) across `DeliveryOptimizationCleanerModule`, `CrashDumpsCleanerModule`, and `BrowserCacheCleanerModule` to protect in-progress Windows Updates and fresh incident crash dumps.
- **Authentic WinSxS Measurement (`DismComponentCleanerModule`):**
  - Replaced the static 2.0 GB estimate with real byte measurement of WinSxS staging/temp/manifest folders and prevented interactive UAC prompts during headless CLI runs.

---

## [1.0.0] — 2026-11-08

### Added
- **Storage Health Audit & Donut Gauge:**
  - Real-time disk hygiene scan with smooth vector gauge.
  - Multi-threaded Dispatcher synchronization via `BindingOperations.EnableCollectionSynchronization`.
  - 24-Hour Age Safety Buffer preserving active installers and running files.
- **Granular Storage Cleaning Engine (7 Modules):**
  - System Temp & Logs with 24-hour age buffer.
  - Crash Dumps & Windows Error Reporting (WER) queues.
  - Non-destructive Chromium & Edge browser cache cleaning (passwords and cookies strictly preserved).
  - Native Windows Shell32 Recycle Bin query and purge.
  - Windows Delivery Optimization staging cache.
  - Developer Caches (NPM, NuGet, Cargo, Pip, Gradle, Rust).
  - Windows Thumbnail & Icon database rebuilder.
- **Space Lens (Large & Old Files Explorer):**
  - High-capacity file cataloging across Downloads, Documents, Videos, and Desktop.
  - Category filters (Videos, Archives, Documents, Images, Executables).
  - Configurable threshold filters by size (>50MB, >100MB, >500MB) and age (>1 Month, >6 Months, >1 Year).
- **Duplicate & Redundant Files Finder:**
  - Two-tier verification: 4KB prefix hashing followed by full SHA-256 analysis.
  - Byte-for-byte exact matches only; original copy safely preserved.
  - Redundant copies moved to the Windows Recycle Bin for safe recovery.
- **Application Manager & Leftover Residue Hunter:**
  - Complete inventory of 64-bit and 32-bit Win32 programs and packaged Store apps.
  - Quiet/unattended uninstallation switch support (`/qn`, `/quiet`, `/verysilent`).
  - Scans `%APPDATA%` and `%LOCALAPPDATA%` for orphaned leftover folders after uninstall.
- **Hardware & WHQL Driver Health:**
  - Hardware device classification across Graphics, Audio, Network, Storage, and System.
  - Queries official Microsoft Windows Update API (`WUApiLib`) for certified WHQL updates.
  - Zero third-party driver scraping.
- **Startup Boot Accelerator:**
  - Reversible toggle using Windows `StartupApproved` binary bitmasks (`0x02` enabled, `0x03` disabled).
  - Matches Windows Task Manager behavior; zero registry run keys deleted.
- **System Telemetry & Vitals:**
  - Real-time CPU utilization via `GetSystemTimes`.
  - Memory analysis via `GlobalMemoryStatusEx` without destructive memory flushing.
  - GPU telemetry (adapter description, driver version, memory).
  - Logical partition volume cards (capacity, free space, filesystem).
- **Recent Activity & Privacy Shield:**
  - Audits Windows Recent Items, Taskbar Jump Lists, Explorer Run history, and Clipboard.
  - Transparent item drawer to inspect individual paths, timestamps, and sizes before clearing.
- **Background Automation:**
  - Windows Task Scheduler (`schtasks.exe`) integration for weekly background audits (`--silent-audit`).
  - Zero idle background CPU or memory usage.
  - Native Windows 11 Toast Notifications when recoverable cache exceeds threshold.
- **Built-In Documentation:**
  - 12 comprehensive chapters with instant keyword search.
- **Testing & Verification Suite:**
  - 21-point architecture test suite and 11-module end-user audit harness.
