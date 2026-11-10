# Changelog — Pilgrim Cleaner

All notable changes to **Pilgrim Cleaner** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
