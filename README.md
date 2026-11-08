# Pilgrim Cleaner
An open-source, non-destructive Windows maintenance utility built with C# and WPF.

[![Framework](https://img.shields.io/badge/.NET-8.0%20WPF-512BD4.svg)](https://dotnet.microsoft.com/)
[![UI](https://img.shields.io/badge/UI-WPF--UI%20%28Fluent%202%20%2F%20Mica%29-0078D4.svg)](https://wpfui.lepo.co/)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%20Windows%2011-0078D4.svg)](https://www.microsoft.com/windows)
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

<img width="1720" height="1392" alt="image" src="https://github.com/user-attachments/assets/62d78e0e-31d5-43ec-831e-b1fdd28b909e" />

<img width="1720" height="1392" alt="image" src="https://github.com/user-attachments/assets/3706efc0-5fc0-46c2-a6e5-ced0ec77e652" />

<img width="1720" height="1392" alt="image" src="https://github.com/user-attachments/assets/c2d4d7e2-e281-4d9c-b2a3-e751c59a90b9" />

<img width="1720" height="1392" alt="image" src="https://github.com/user-attachments/assets/b1e55fb6-e814-49de-8ec7-2b588798ada1" />

<img width="1720" height="1392" alt="image" src="https://github.com/user-attachments/assets/0d17f082-7bb5-4bd8-be71-5fe18c01192a" />

<img width="1720" height="1392" alt="image" src="https://github.com/user-attachments/assets/ce6cf8d4-f01a-469f-808a-c8f269fb2fd8" />

<img width="1720" height="1392" alt="image" src="https://github.com/user-attachments/assets/db5f0730-4d36-47c1-9151-0ce62cc5b482" />

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
- **12 Searchable Chapters:** Built directly into the application with instant keyword search covering safety rules, architecture, module explanations, and FAQ.

---

## Technical Stack & Architecture

- **Runtime:** .NET 8.0 Desktop (`net8.0-windows`)
- **UI Framework:** Windows Presentation Foundation (WPF) with `WPF-UI 4.3.0`
- **Design System:** Windows 11 Fluent Design 2 with Mica / Acrylic backdrop materials
- **Architecture Pattern:** MVVM (Model-View-ViewModel) using `CommunityToolkit.Mvvm 8.4.2`
- **Multi-Threading:** Collection synchronization via `BindingOperations.EnableCollectionSynchronization` for thread-safe UI updates from background workers
- **Native Interop:** Direct P/Invoke to `Shell32`, `Kernel32`, `Advapi32`, and COM interop for `WUApiLib` (Windows Update API)

---

## Installation Options

### 1. 1-Click Native Installer (Recommended)
Download the release archive, extract it, and run **`Install.bat`**. This deploys Pilgrim Cleaner to `%LOCALAPPDATA%\Programs\Pilgrim Cleaner`, creates Desktop and Start Menu shortcuts, and registers the app in Windows Settings (*Installed Apps*).

### 2. Portable ZIP (Zero Installation)
Download **`PilgrimCleaner-v1.0.0-Windows-x64.zip`**, extract anywhere, and launch `PilgrimCleaner.exe` directly. No registry entries or system folders are modified.

### 3. Setup Wizard (`PilgrimCleanerSetup.exe`)
Download and run the standalone setup wizard from [GitHub Releases](https://github.com/Inectha/Pilgrim-Cleaner/releases) for a guided installation.

See [**INSTALL.md**](INSTALL.md) for detailed installation and uninstallation instructions.

---

## Building from Source

### Prerequisites
- Windows 10 (Version 1903+) or Windows 11
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Clone & Build
```powershell
# Clone the repository
git clone https://github.com/Inectha/Pilgrim-Cleaner.git
cd Pilgrim-Cleaner

# Build the solution
dotnet build

# Run the application
dotnet run

# Run the 21-point architecture test suite
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
