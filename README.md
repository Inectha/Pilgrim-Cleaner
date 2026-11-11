# Pilgrim Cleaner
An open-source, non-destructive Windows 10/11 & Windows Server maintenance utility and Sysadmin CLI built with C# and WPF.

[![Framework](https://img.shields.io/badge/.NET-10.0%20WPF%20%2B%20CLI-512BD4.svg)](https://dotnet.microsoft.com/)
[![UI](https://img.shields.io/badge/UI-WPF--UI%20%28Fluent%202%20%2F%20Mica%29-0078D4.svg)](https://wpfui.lepo.co/)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11%20%7C%20Windows%20Server-0078D4.svg)](https://www.microsoft.com/windows)
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](https://www.gnu.org/licenses/gpl-3.0)
[![Built With](https://img.shields.io/badge/Pair%20Programmed%20With-Google%20Antigravity-4285F4.svg)](https://deepmind.google/technologies/gemini/)

---

## About This Project

I built Pilgrim Cleaner for my own personal use on Windows 11 as a clean, transparent alternative to commercial system maintenance utilities. Many existing tools are overloaded with telemetry, ads, aggressive background services, or risky registry "cleaners" that often do more harm than good.

I developed this utility using **[Google Antigravity](https://deepmind.google/technologies/gemini/)** (agentic AI pair programming) and decided to share it publicly on GitHub under the GPLv3 license for anyone looking for a modern, honest, and safe system maintenance tool.

### Core Principles
- **No Registry Cleaners:** The Windows Registry is a structured system database. Decades of technical research show that registry cleaners provide zero performance gain and risk breaking installed applications. Pilgrim Cleaner never cleans or deletes registry keys.
- **No Artificial RAM Flushing:** Many utilities use memory-trimming hacks (`EmptyWorkingSet`) to show a lower RAM number in Task Manager. In reality, this forces Windows to dump memory to the page file on disk, slowing down the system when data needs to be reloaded. Pilgrim Cleaner reports authentic memory metrics without manipulating the working set.
- **24-Hour Age Buffer:** Temporary files created or modified within the last 24 hours are automatically preserved to protect active software installations, in-progress downloads, and running background processes.
- **Transparent Preview First:** No blind deletions. The three-step workflow (Audit $\rightarrow$ Review $\rightarrow$ Clean) ensures every single file path, size, and category is visible before any action is taken.
- **Native OS APIs:** Deleting redundant files uses the Windows Recycle Bin (`Shell32`), driver checks use the official Microsoft Windows Update API (`WUApiLib`), and scheduled maintenance uses the built-in Windows Task Scheduler (`schtasks.exe`). No background daemons stay running in memory when idle.

---

## Screenshots

<!-- To add your own screenshots, place image files in a `docs/screenshots/` folder and link them here, or drag & drop images directly into GitHub issues/releases. -->

<img width="1240" height="820" alt="image" src="https://github.com/user-attachments/assets/17147b33-d366-4677-9ac8-f75d5b3dc8c3" />
<br><br>
<img width="1240" height="820" alt="image" src="https://github.com/user-attachments/assets/5f0778c2-f8fc-4320-a79a-ea522e07c2b7" />
<br><br>
<img width="1240" height="820" alt="image" src="https://github.com/user-attachments/assets/69ddd250-b14f-466d-aa63-d6e712a9c87d" />
<br><br>
<img width="1240" height="820" alt="image" src="https://github.com/user-attachments/assets/5a7b140a-a96f-4f1a-8155-b3671fdc18c6" />
<br><br>
<img width="1240" height="820" alt="image" src="https://github.com/user-attachments/assets/9b83c255-3741-4b75-ad0d-756792c26dd5" />
<br><br>
<img width="1240" height="820" alt="image" src="https://github.com/user-attachments/assets/b1037804-135f-44a2-9b94-7fa37201ddd0" />
<br><br>
<img width="1240" height="820" alt="image" src="https://github.com/user-attachments/assets/5a1a9d75-8b76-45ac-82a4-95712ca8907b" />
<br><br>
<img width="1240" height="820" alt="image" src="https://github.com/user-attachments/assets/c3307e8a-a922-4254-ae9a-f041322f9f6b" />
<br><br>
<img width="1240" height="820" alt="image" src="https://github.com/user-attachments/assets/a1456d43-4ce7-424b-9f6f-91555b218ad4" />
<br><br>
<img width="1240" height="820" alt="image" src="https://github.com/user-attachments/assets/c0a648e3-64ee-426a-8e4c-70d37a4c0d17" />

---

## Features & Technical Implementation

### 1. Dashboard & Storage Health Audit
- **Real-Time Storage Scan:** Asynchronously audits enabled modules and calculates the exact recoverable space in bytes.
- **Vector Donut Gauge:** Custom WPF control using vector math (`ArcSegment`, `PathGeometry`) that animates disk usage and recoverable space smoothly at display refresh rate.
- **Guardrail Metrics:** Tracks and displays how many files and bytes were protected by safety buffers during the audit.

### 2. Granular Storage Cleaning Modules
- **System Temp & Logs:** Scans `%TEMP%` and `C:\Windows\Temp` while enforcing the 24-hour age safety threshold.
- **Crash Dumps & Error Reporting:** Cleans memory crash dumps (`MEMORY.DMP`, minidumps) and Windows Error Reporting (WER) queue directories.
- **Browser Caches (Non-Destructive):** Cleans cached images and media files for Chromium-based browsers (Chrome, Edge). Strictly excludes SQLite databases containing user cookies, login sessions, history, and saved passwords. Checks if browser processes are active before touching files.
- **Recycle Bin:** Queries and empties the Windows Recycle Bin using native `SHEmptyRecycleBinW` and `SHQueryRecycleBinW` APIs.
- **Windows Delivery Optimization:** Cleans staged update packages in `%SystemRoot%\SoftwareDistribution\Download`.
- **Developer Caches:** Safely removes intermediate build artifacts and package manager caches for NPM, NuGet, Cargo, Pip, Gradle, and Rust (`target` folders).
- **Server & CBS Archived Logs (`server_logs`):** Reclaims disk space from rotated IIS Web Server logs (`%SystemDrive%\inetpub\logs\LogFiles\W3SVC*`) and archived Windows Component-Based Servicing (`CbsPersist_*`) logs while strictly preserving active logs and enforcing a configurable retention window (default 14 days).
- **Thumbnail Cache:** Reclaims corrupted or bloated Windows Explorer thumbnail databases (`thumbcache_*.db`).

### 3. Space Lens (Large & Old Files Explorer)
- **Directory Cataloging:** Recursively indexes user libraries (Downloads, Documents, Videos, Desktop) without locking the UI.
- **Category Filters:** Quick filters for Videos, Archives, Documents, Images, Executables, and All Types.
- **Configurable Thresholds:** Filter files by size (>50 MB, >100 MB, >500 MB) and age (>1 Month, >6 Months, >1 Year).
- **Recycle Bin Deletion:** Selected files are moved to the Recycle Bin rather than permanently deleted, allowing easy recovery.

### 4. Duplicate & Redundant Files Finder
- **Two-Tier Hashing:** To avoid reading entire files unnecessarily, the scanner first groups files by exact byte size. For candidates with matching sizes, it reads a rapid 4KB prefix hash. Only files with matching prefix hashes undergo a full cryptographic SHA-256 hash.
- **Byte-for-Byte Verification:** Only files that are 100% byte-identical are grouped together.
- **Smart Selection:** Protects the original file (`[Original (Keep)]`) while selecting duplicates for review.
- **Safe Removal:** Marked duplicates are sent to the Windows Recycle Bin.

### 5. Application Manager & Residue Hunter
- **Software Inventory:** Catalogs 64-bit and 32-bit Win32 desktop applications (`HKLM` and `HKCU` uninstall keys) along with packaged Windows Store apps (`Appx/MSIX`).
- **Safety Filters:** System components, security updates, and driver packages are automatically marked and protected.
- **Unattended Uninstall:** Dispatches official silent uninstall arguments (`/qn`, `/quiet`, `/verysilent`) when supported.
- **Leftover Hunter:** After an uninstallation, scans `%APPDATA%` and `%LOCALAPPDATA%` for orphaned folders matching the software name, allowing the user to review and confirm removal.

### 6. Hardware & WHQL Driver Health
- **Device Inventory:** Organizes connected hardware into Graphics, Audio, Network, Storage, and System Devices.
- **WHQL Verification:** Checks driver signature status through `Win32_PnPSignedDriver` to verify Microsoft Windows Hardware Quality Labs compliance.
- **Official Updates Only:** Queries available driver updates exclusively through the native Microsoft Windows Update Session (`WUApiLib`). Never scrapes or downloads files from third-party driver sites.

### 7. Startup Applications Manager
- **Reversible StartupApproved Bitmask:** Toggles startup programs on or off using the official Windows Task Manager binary structure (`Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run` and `StartupFolder`).
- **Zero Deletions:** Uses `0x02` for enabled and `0x03` for disabled. Registry keys and startup shortcuts remain intact and fully reversible.
- **Impact Rating:** Evaluates boot impact (High, Medium, Low) based on file size, application type, and known helper patterns.

### 8. System Telemetry & Vitals
- **CPU Telemetry:** Calculates real-time processor utilization using the native Win32 `GetSystemTimes` API (comparing idle, kernel, and user times across sample intervals).
- **RAM Telemetry:** Accurate physical memory breakdown (used, available, total, and commit limit) using `GlobalMemoryStatusEx`.
- **GPU Telemetry:** Adapter name, driver version, and dedicated video memory.
- **Disk Partitions:** Live capacity bars, free space, and filesystem type for all logical drives.

### 9. Privacy & Activity Footprint
- **Trace Audit:** Catalogs traces across Windows Recent Items, Taskbar Jump Lists, Explorer Run history (MRU), and Clipboard buffer.
- **Inspection Drawer:** Expandable tree view allowing users to inspect individual file names, paths, sizes, and timestamps before deciding to purge.

### 10. Background Automation
- **Windows Task Scheduler Integration:** Registers a standard Windows scheduled task via `schtasks.exe` to run periodic silent audits (`--silent-audit`).
- **Zero Idle Resource Usage:** The application does not run a continuous background service or system tray process. The scheduled task runs headlessly in under 3 seconds, audits storage targets, and exits.
- **Native Toast Notifications:** If recoverable cache exceeds the configured threshold, a native Windows 11 notification banner is displayed inviting the user to open the app and review.

### 11. Built-In User Manual
- **13 Searchable Chapters:** Built directly into the application with instant keyword search covering safety rules, architecture, module explanations, Sysadmin CLI reference, and FAQ.

### 12. Sysadmin CLI (`pilgrim.exe`) & Windows Server Edition
- **Native Console Binary (`pilgrim.exe`):** Ships alongside the WPF GUI for headless execution in PowerShell, SSH, WinRM, Microsoft Intune, SCCM, and RMM platforms (NinjaOne, Datto, Atera).
- **Preset Execution Profiles (`--profile`):**
  - `server-safe` — Targets `system_temp`, `crash_dumps`, `delivery_optimization`, and `server_logs` (IIS & CBS logs >14 days); skips browser and developer caches.
  - `rds` — Automatically enables `--all-profiles` to audit and clean across all user profiles (`C:\Users\*`) on Remote Desktop Services / Citrix / AVD multi-session hosts while enforcing the 24-hour active file guardrail.
  - `workstation` — Standard endpoint maintenance across user and system caches.
- **Structured JSON Output (`--json`):** Pipe audit and cleanup telemetry directly into PowerShell (`pilgrim audit --json | ConvertFrom-Json`).
- **Windows Event Log Integration (`--event-log`):** Writes structured events (IDs `1000`–`1003`, Source `PilgrimCleaner`) to the Windows Application Event Log for RMM monitoring.
- **In-Terminal Sysadmin Manual (`pilgrim man`):** Offline terminal man-pages covering `overview`, `profiles`, `security`, `automation`, and `exitcodes`.

```powershell
# Interactive storage audit using the Windows Server safe profile
pilgrim audit --profile server-safe

# RMM / PowerShell JSON pipeline with threshold check
$audit = pilgrim audit --profile server-safe --json | ConvertFrom-Json
if ($audit.totalRecoverableBytes -gt 5GB) {
    pilgrim clean --profile server-safe --force --event-log
}

# Multi-user Terminal Server (RDS / Citrix / AVD) cleanup across all C:\Users\* profiles
pilgrim clean --profile rds --force --event-log

# View the built-in terminal manual
pilgrim man
pilgrim man automation
```

---

## Technical Stack & Architecture

- **Runtime:** .NET 10.0 Desktop (`net10.0-windows`)
- **UI Framework:** Windows Presentation Foundation (WPF) with `WPF-UI 4.3.0`
- **CLI Companion:** Native Console Subsystem (`pilgrim.exe`, `cli/PilgrimCli.csproj`) sharing the core module engine
- **Design System:** Windows 11 Fluent Design 2 with Mica / Acrylic backdrop materials
- **Architecture Pattern:** MVVM (Model-View-ViewModel) using `CommunityToolkit.Mvvm 8.4.2`
- **Multi-Threading:** Collection synchronization via `BindingOperations.EnableCollectionSynchronization` for thread-safe UI updates from background workers
- **Native Interop:** Direct P/Invoke to `Shell32`, `Kernel32`, `Advapi32`, and COM interop for `WUApiLib` (Windows Update API)

---

## Installation Options

Requires the [.NET 10 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/10.0).

### 1. Setup Wizard (`PilgrimCleanerSetup.exe`) & Unattended Deploy
Download and run **`PilgrimCleanerSetup.exe`** from [GitHub Releases](https://github.com/Inectha/Pilgrim-Cleaner/releases).
- Installs machine-wide to `C:\Program Files\Pilgrim Cleaner` (AppLocker & WDAC compliant) and registers `pilgrim.exe` in the system `PATH`.
- Unattended deployment for RMM/Intune: `PilgrimCleanerSetup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART`

### 2. Winget
`winget install PilgrimSoftware.PilgrimCleaner` (installs the .NET runtime dependency automatically).

### 3. Portable ZIP (Zero Installation)
Download **`PilgrimCleaner-v<version>-Windows-x64.zip`**, extract it, and run `PilgrimCleaner.exe` (GUI) or `pilgrim.exe` (CLI) directly.

See [**INSTALL.md**](INSTALL.md) for detailed workstation and Windows Server deployment instructions.

---

## Building from Source

### Prerequisites
- Windows 10 (Version 1903+) or Windows 11
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Clone & Build
```powershell
# Clone the repository
git clone https://github.com/Inectha/Pilgrim-Cleaner.git
cd Pilgrim-Cleaner

# Build the solution
dotnet build

# Run the application
dotnet run

# Run the 30-point architecture test suite
dotnet run --project Tests/PilgrimCleaner.Tests.csproj

# Run the 11-module end-user audit harness
dotnet run --project Tests/PilgrimCleaner.Tests.csproj -- --user-audit
```

---

## Documentation

- [**INSTALL.md**](INSTALL.md) — Installation and uninstallation instructions.
- [**CHANGELOG.md**](CHANGELOG.md) — Version history and release notes.
- [**CONTRIBUTING.md**](CONTRIBUTING.md) — Developer setup and contribution guidelines.
- [**SECURITY.md**](SECURITY.md) — Security policy and vulnerability disclosure.

---

## License

This project is licensed under the **GNU General Public License v3.0 (GPLv3)** — see the [LICENSE](LICENSE) file for details.
