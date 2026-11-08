# Changelog — Pilgrim Cleaner

All notable changes to **Pilgrim Cleaner** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
