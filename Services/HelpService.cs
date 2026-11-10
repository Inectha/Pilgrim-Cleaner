// Lumina Cleaner - Enterprise Windows Disk Cleaner & Optimizer
// Copyright (C) 2026 Pilgrim Software
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Linq;
using LuminaCleaner.Models;
using Wpf.Ui.Controls;

namespace LuminaCleaner.Services;

public class HelpService : IHelpService
{
    private readonly List<HelpArticleModel> _articles;

    public HelpService()
    {
        _articles =
        [
            new HelpArticleModel
            {
                Id = "getting_started",
                Title = "1. Introduction & Quick Start Guide",
                Category = "Getting Started",
                Summary = "An overview of Pilgrim Cleaner, the 3-phase safe maintenance workflow, and core navigation.",
                IconSymbol = SymbolRegular.Sparkle24,
                Keywords = ["start", "intro", "introduction", "begin", "dashboard", "scan", "audit", "basics", "workflow"],
                NavigationTargetView = "Dashboard",
                Content =
@"WELCOME TO PILGRIM CLEANER
Pilgrim Cleaner is a Windows disk optimization and systems utility developed by Pilgrim Software. It combines the visual polish of modern Fluent Design and Mica backdrops with safe, transparent maintenance standards.

THE THREE-PHASE ENTERPRISE MAINTENANCE WORKFLOW:
1. Audit & Inspection:
   When you click 'Audit System Storage', Pilgrim Cleaner performs a non-destructive dry-run across all enabled cleaner modules. Every candidate file is identified, categorized, and calculated to the exact byte. Nothing is ever deleted during the audit phase.

2. Review & Fine-Tuning:
   In the 'Storage Modules' view, you can inspect each individual cleaner category. Expandable cards show the exact file path, last modified date, and size of every candidate item. You have complete control to toggle categories or uncheck specific items.

3. Safe Execution:
   Only after your explicit review and confirmation are selected files cleaned. An animated 60 FPS donut gauge and progress bar follow the operation in real time. Once finished, the progress gauge always locks cleanly at 100%."
            },

            new HelpArticleModel
            {
                Id = "safety_manifesto",
                Title = "2. The Enterprise Safety Manifesto",
                Category = "Safety & Policies",
                Summary = "Why Pilgrim Cleaner never tampers with the Registry, never flushes RAM, and enforces a 24-hour age buffer.",
                IconSymbol = SymbolRegular.ShieldCheckmark24,
                Keywords = ["safety", "manifesto", "registry", "ram", "buffer", "24h", "locked", "resilience", "enterprise", "policies"],
                NavigationTargetView = "About",
                Content =
@"- THE SAFETY FOUNDATION:
Pilgrim Cleaner stands fundamentally apart from shady third-party utilities through immutable, hardcoded guardrails designed to prevent system corruption:

1. Zero Registry Tampering:
   Pilgrim Cleaner NEVER modifies or deletes Windows Registry keys for so-called 'cleaning'. Decades of industry research confirm that registry cleaners offer zero measurable performance benefit while risking irreparable OS corruption.

2. Zero RAM Flushing Hacks:
   Pilgrim Cleaner NEVER forces artificial 'working set dumps' or memory purges. Modern Windows dynamically manages available RAM as a high-speed file cache. Forcibly emptying RAM degrades performance by forcing Windows to reread data from disk.

3. The 24-Hour Safety Buffer:
   Temporary files created or modified within the last 24 hours are STRICTLY PRESERVED. This prevents disruption to active software installers, running background updaters, and active office sessions.

4. Resilient File Handling:
   Files currently locked or opened by running applications are gracefully bypassed without halting the audit or cleanup process."
            },

            new HelpArticleModel
            {
                Id = "storage_modules",
                Title = "3. Storage Modules & Safe Cleanup Rules",
                Category = "Storage & Cleanup",
                Summary = "Detailed breakdown of the 8 core cleaner modules, from System Temp and Browser Caches to Server Logs and DISM WinSxS.",
                IconSymbol = SymbolRegular.Storage24,
                Keywords = ["modules", "temp", "crash", "dumps", "browser", "recycle", "delivery", "developer", "dism", "winsxs", "server", "iis", "cbs", "storage"],
                NavigationTargetView = "Modules",
                Content =
@"CORE STORAGE CLEANER MODULES:

• System Temporary Files (%TEMP%, C:\Windows\Temp):
  Cleans stale temporary files left behind by completed application sessions, strictly preserving files modified within the last 24 hours. Supports multi-profile RDS scanning (--all-profiles).

• Windows Crash Dumps & Diagnostic Minidumps:
  Reclaims disk space by purging historical memory crash dumps (%SystemRoot%\MEMORY.DMP, Minidump, and WER ReportQueue/ReportArchive) while keeping recent diagnostics (>24h safety buffer).

• Browser Caches (Edge, Chrome, Brave, Firefox):
  Cleans cached web images, script buffers, and offline media caches (>24h) without deleting saved passwords, cookies, or browser history.

• Windows Recycle Bin (Native Shell32 API):
  Safely queries and empties the Recycle Bin across all fixed drives via the official Windows Shell32 API (SHEmptyRecycleBinW).

• Delivery Optimization Cache:
  Purges stale peer-to-peer Windows Update delivery cache packages (>24h) that have already been distributed.

• Developer & Package Manager Caches:
  Reclaims gigabytes of disk space from package caches: NuGet, npm, pnpm, Python Pip, and Rust Cargo.

• Windows Server & IIS Logs (W3SVC & CBS):
  Purges rotated IIS W3SVC access logs (%SystemDrive%\inetpub\logs\LogFiles) and archived CBS servicing logs (CbsPersist_*.log/.cab) older than 14 days while never touching active log files.

• DISM Windows Component Store (WinSxS):
  Analyzes real WinSxS Temp/Backup staging directories and optimizes the Windows Component Store using DISM (/StartComponentCleanup) with graceful administrator elevation."
            },

            new HelpArticleModel
            {
                Id = "space_lens",
                Title = "4. Space Lens: Large & Ancient Files Explorer",
                Category = "Storage & Cleanup",
                Summary = "Visually discover and reclaim storage occupied by massive archives, media, and forgotten downloads.",
                IconSymbol = SymbolRegular.FolderZip24,
                Keywords = ["space lens", "large", "ancient", "files", "video", "iso", "archives", "downloads", "threshold"],
                NavigationTargetView = "SpaceLens",
                Content =
@"SPACE LENS GUIDE:
Traditional disk cleaners only look at temp folders. Space Lens scans your real user libraries (Downloads, Documents, Videos, Desktop) to reveal where your disk space actually went.

FEATURES:
- Intelligent Size Threshold: Focuses on significant space-consumers (>50 MB).
- Age Filter: Instantly sort and filter files untouched for more than 6 months or 1 year.
- Category Classification: Automatically tags video files (.mp4, .mkv), archives (.zip, .rar, .7z), disk images (.iso, .vhd), and software installers (.exe, .msi).
- Safe Recycle Action: When you select items to clean, Space Lens safely sends them to the Windows Recycle Bin using native Shell32, allowing full undo at any time."
            },

            new HelpArticleModel
            {
                Id = "duplicate_finder",
                Title = "5. Duplicate File Finder & SHA-256 Engine",
                Category = "Storage & Cleanup",
                Summary = "Identify and remove byte-identical duplicate files with cryptographic 100% precision.",
                IconSymbol = SymbolRegular.DocumentCopy24,
                Keywords = ["duplicates", "sha256", "hash", "redundant", "copies", "photos", "finder", "recycle"],
                NavigationTargetView = "Duplicates",
                Content =
@"DUPLICATE FINDER GUIDE:
Users frequently accumulate duplicate downloads, duplicate photos, and repeated document copies without realizing how much disk space is wasted.

HOW IT WORKS (2-TIER PRECISION):
1. Size Pre-Filter: Only files with exact matching byte sizes are considered candidates.
2. Fast Header Screening: Compares initial 4KB file headers to eliminate false collisions instantly without disk thrashing.
3. Cryptographic SHA-256 Hash: Candidate files are verified using full SHA-256 cryptographic hashes. Two distinct files can NEVER match by mistake!
4. Smart Pre-Selection: The original oldest file is preserved, while redundant copies are pre-selected for safe deletion to the Windows Recycle Bin."
            },

            new HelpArticleModel
            {
                Id = "app_manager",
                Title = "6. App Manager & Leftover Residue Hunter",
                Category = "System Management",
                Summary = "Inspect installed Win32 software and Windows Apps, execute quiet uninstalls, and purge leftover folders.",
                IconSymbol = SymbolRegular.Apps24,
                Keywords = ["apps", "uninstaller", "applications", "win32", "appx", "leftovers", "residue", "purge"],
                NavigationTargetView = "Applications",
                Content =
@"APP MANAGER & UNINSTALLER GUIDE:

CAPABILITIES:
• Comprehensive Software Inventory:
  Enumerates 64-bit and 32-bit Win32 programs via Windows Registry as well as packaged modern Windows Store Apps (AppX/MSIX).

• System Component Exclusions:
  Core Windows OS components, security hotfixes, and driver packages are protected and excluded from deletion targets.

• Quiet & Standard Uninstall:
  Launches official vendor uninstallers silently (/quiet, /passive) or via interactive setup.

• Post-Uninstall Leftover Residue Hunter:
  Following an uninstallation, Pilgrim Cleaner scans %APPDATA% and %LOCALAPPDATA% for abandoned residue folders matching the software name, presenting them in a dedicated review drawer for explicit user confirmation before deletion."
            },

            new HelpArticleModel
            {
                Id = "driver_health",
                Title = "7. Driver Health & Certified WHQL Updates",
                Category = "System Management",
                Summary = "Inventory installed hardware drivers and audit official Microsoft WHQL updates directly via Windows Update.",
                IconSymbol = SymbolRegular.DeveloperBoard24,
                Keywords = ["drivers", "whql", "hardware", "windows update", "certified", "graphics", "network", "audio"],
                NavigationTargetView = "Drivers",
                Content =
@"DRIVER HEALTH & WHQL AUDIT GUIDE:

ZERO SHADY REPOSITORIES POLICY:
Many commercial 'driver updaters' download unverified or beta drivers from dubious overseas mirrors, causing blue screens (BSODs). Pilgrim Cleaner connects EXCLUSIVELY to official Microsoft Windows Update servers via the native Windows Update Session API (WUApiLib).

CATEGORIES & INVENTORY:
- Graphics Adapters: NVIDIA, AMD, Intel displays and GPUs.
- Network Adapters: Wi-Fi 6/7, Gigabit Ethernet, and Bluetooth controllers.
- Audio Controllers: High Definition Audio, Realtek, and USB DACs.
- Storage Controllers: NVMe SSD controllers, RAID, and SATA controllers.
- System Devices: Motherboard chipsets and ACPI controllers.

Direct Installation: Pending WHQL certified drivers can be installed directly through the Windows Update service with automatic restart coordination."
            },

            new HelpArticleModel
            {
                Id = "startup_accelerator",
                Title = "8. Startup Boot Accelerator",
                Category = "System Management",
                Summary = "Accelerate Windows boot times using native StartupApproved bitmasks without deleting registry keys.",
                IconSymbol = SymbolRegular.Flash24,
                Keywords = ["startup", "boot", "accelerator", "run", "startupapproved", "toggle", "impact"],
                NavigationTargetView = "Startup",
                Content =
@"STARTUP ACCELERATOR GUIDE:

SAFE NATIVE CONTROL (STARTUPAPPROVED):
Instead of recklessly deleting registry keys from HKLM\Run (which breaks software updaters), Pilgrim Cleaner utilizes the official Windows Task Manager architecture: the StartupApproved registry bitmask:
- 0x02 = Enabled for Windows boot.
- 0x03 = Disabled / deferred.

IMPACT CLASSIFICATION:
Startup programs are analyzed for estimated boot delay impact:
- High: Large memory footprints, background helper updaters, and browser launchers.
- Medium: Utility sync tools and system tray monitors.
- Low: Lightweight driver control panels and single-threaded helpers.

Use the toggle switch on any item to instantly enable or disable it for Windows startup."
            },

            new HelpArticleModel
            {
                Id = "system_vitals",
                Title = "9. System Vitals & Hardware Integrity",
                Category = "Vitals & Privacy",
                Summary = "Real-time CPU telemetry, RAM allocation breakdown, storage volume partitions, and battery wear health.",
                IconSymbol = SymbolRegular.Heart24,
                Keywords = ["vitals", "cpu", "ram", "memory", "battery", "drives", "storage", "gpu", "uptime", "hardware"],
                NavigationTargetView = "Telemetry",
                Content =
@"SYSTEM VITALS & HARDWARE TELEMETRY:

DEEP HARDWARE INTROSPECTION:
• Processor (CPU):
  Displays processor model name, active cores, logical threads, system-wide CPU utilization, and running process counts.

• Memory (RAM) & Pagefile:
  Real-time physical RAM usage percentage, available memory, committed pagefile buffer, and standby cache tracking.

• Logical Disk Volumes:
  Inventories all fixed and removable storage drives (C:\, D:\, etc.) with volume label, file system (NTFS, FAT32), used vs free space, and visual capacity indicators.

• Graphics Controller (GPU):
  Displays adapter name, WHQL driver version, and dedicated VRAM capacity.

• Laptop Battery & Power Health:
  Queries native Windows battery telemetry to report charge percentage, charging state, estimated wear level, and health verdict. Displays direct AC status on desktop workstations."
            },

            new HelpArticleModel
            {
                Id = "privacy_shield",
                Title = "10. Privacy & Footprint Shield",
                Category = "Vitals & Privacy",
                Summary = "Inspect and eradicate Windows Recent Documents, Taskbar Jump Lists, Explorer MRU, and Clipboard traces.",
                IconSymbol = SymbolRegular.Shield24,
                Keywords = ["privacy", "shield", "recent", "jump lists", "mru", "clipboard", "traces", "inspect"],
                NavigationTargetView = "Privacy",
                Content =
@"PRIVACY & FOOTPRINT SHIELD GUIDE:

COMPLETE FILE-LEVEL INSPECTION:
Unlike tools that only show total numbers without proof, Pilgrim Cleaner's Privacy Shield lets you expand each category to inspect every single trace file, access timestamp, and file size before cleaning.

SUPPORTED TARGETS:
• Windows Recent Documents:
  Recent file shortcuts (%APPDATA%\Microsoft\Windows\Recent) created when files are opened in File Explorer.

• Taskbar Jump Lists (Automatic & Custom Destinations):
  Frequent folder pins and per-app recent file histories registered on the Windows Taskbar.

• File Explorer Search & Run History:
  Typed commands in the Windows Run dialog and File Explorer address bar history (Explorer MRU).

• Live Clipboard Buffer:
  Active copied text, images, or credentials residing in system clipboard memory."
            },

            new HelpArticleModel
            {
                Id = "automation_scheduler",
                Title = "11. Background Automation & Windows 11 Alerts",
                Category = "System Management",
                Summary = "Configure scheduled weekly maintenance in Windows Task Scheduler and native toast notifications.",
                IconSymbol = SymbolRegular.Timer24,
                Keywords = ["automation", "scheduler", "weekly", "toast", "notifications", "silent", "schtasks"],
                NavigationTargetView = "Automation",
                Content =
@"BACKGROUND AUTOMATION GUIDE:

ZERO IDLE CPU USAGE:
Pilgrim Cleaner does NOT run a permanent background memory-eating daemon. Instead, it leverages the native Windows Task Scheduler (schtasks.exe).

HOW IT OPERATES:
1. When activated, a scheduled task ('PilgrimCleanerAutoMaintenance') is registered to run weekly (Sundays at 02:00 AM).
2. The task launches Pilgrim Cleaner in silent audit mode ('--silent-audit').
3. If recoverable junk storage exceeds the threshold, Pilgrim Cleaner dispatches a native Windows 11 Toast Notification inviting you to review or purge.
4. You can toggle the automation switch ON or OFF at any time."
            },

            new HelpArticleModel
            {
                Id = "terminal_and_license",
                Title = "12. Action Terminal, Logs & Open Source License",
                Category = "Safety & Policies",
                Summary = "Live operational terminal stream, forensic action logs, and the GNU General Public License v3.",
                IconSymbol = SymbolRegular.DocumentBulletListClock24,
                Keywords = ["terminal", "logs", "license", "gnu", "gpl", "open source", "pilgrim", "audit"],
                NavigationTargetView = "Logs",
                Content =
@"ACTION TERMINAL & OPEN SOURCE GNU LICENSE:

FORENSIC AUDIT TRAIL:
The Action Terminal provides a transparent live log stream of every file operation, elevation check, and safety guardrail decision executed by the engine.

FREQUENTLY ASKED QUESTIONS (FAQ):
Q: Can I restore deleted files?
A: Yes! Space Lens and Duplicate Finder send files to the Windows Recycle Bin using native Shell32, allowing full restoration at any time.

Q: Under which license is Pilgrim Cleaner released?
A: Pilgrim Cleaner is Free and Open Source Software released under the GNU General Public License version 3 (GPLv3). You have the complete freedom to inspect, run, modify, and redistribute the software.

Copyright (C) 2026 Pilgrim Software. All rights reserved."
            },

            new HelpArticleModel
            {
                Id = "sysadmin_cli_guide",
                Title = "13. Sysadmin CLI (pilgrim.exe) & Windows Server Guide",
                Category = "System Management",
                Summary = "Headless CLI tool, Windows Server & RDS profiles, JSON telemetry pipelines, Event Log integration, and EDR/AppLocker safety.",
                IconSymbol = SymbolRegular.WindowConsole20,
                Keywords = ["cli", "sysadmin", "server", "rds", "json", "powershell", "intune", "rmm", "winget", "applocker", "manual"],
                NavigationTargetView = "Automation",
                Content =
@"SYSADMIN CLI (pilgrim.exe) & WINDOWS SERVER MAINTENANCE:

Pilgrim Cleaner includes a native console companion binary ('pilgrim.exe') designed for Windows System Administrators, MSPs, and DevOps engineers managing Windows 10/11 Workstations, Windows Servers (2019/2022/2025), and Remote Desktop Services (RDS / Citrix / AVD).

1. CORE CLI COMMANDS:
• pilgrim audit   [--profile <name>] [--modules <list>] [--all-profiles] [--json]
• pilgrim clean   [--profile <name>] [--dry-run] [--force] [--all-profiles] [--event-log] [--json]
• pilgrim status  [--json]
• pilgrim modules [--json]
• pilgrim man     [overview | profiles | security | automation | exitcodes]

2. BUILT-IN EXECUTION PROFILES (--profile):
• server-safe : Targets system_temp, crash_dumps, delivery_optimization, and server_logs (IIS W3SVC & CBS archived logs >14d). Skips browser and developer caches.
• rds         : Automatically activates --all-profiles across C:\Users\* for multi-session Terminal Servers while enforcing the 24-hour active session guardrail.
• workstation : Standard endpoint cleanup across temp, dumps, browser caches, recycle bin, delivery optimization, and dev caches.

3. POWERSHELL & RMM JSON PIPELINE EXAMPLE:
  $audit = pilgrim audit --profile server-safe --json | ConvertFrom-Json
  if ($audit.totalRecoverableBytes -gt 5GB) {
      pilgrim clean --profile server-safe --force --event-log
  }

4. IN-TERMINAL MANUAL:
Open PowerShell or Command Prompt and run 'pilgrim man' (or 'pilgrim man security', 'pilgrim man automation', 'pilgrim man exitcodes') for complete offline sysadmin documentation."
            }
        ];
    }

    public IReadOnlyList<HelpArticleModel> GetAllArticles() => _articles;

    public IReadOnlyList<HelpArticleModel> SearchArticles(string query, string categoryFilter)
    {
        var result = _articles.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(categoryFilter))
        {
            string cat = categoryFilter.Trim();

            // Map English and legacy Dutch aliases
            if (cat.Equals("All", StringComparison.OrdinalIgnoreCase) || cat.Equals("Alles", StringComparison.OrdinalIgnoreCase))
            {
                // Match all
            }
            else if (cat.Equals("Storage & Cleanup", StringComparison.OrdinalIgnoreCase) || cat.Equals("Opslag & Schoonmaken", StringComparison.OrdinalIgnoreCase))
            {
                result = result.Where(a => a.Category == "Storage & Cleanup");
            }
            else if (cat.Equals("System Management", StringComparison.OrdinalIgnoreCase) || cat.Equals("Systeembeheer", StringComparison.OrdinalIgnoreCase))
            {
                result = result.Where(a => a.Category == "System Management");
            }
            else if (cat.Equals("Vitals & Privacy", StringComparison.OrdinalIgnoreCase))
            {
                result = result.Where(a => a.Category == "Vitals & Privacy");
            }
            else if (cat.Equals("Safety & Policies", StringComparison.OrdinalIgnoreCase) || cat.Equals("Veiligheid", StringComparison.OrdinalIgnoreCase) || cat.Equals("Veiligheid & Policies", StringComparison.OrdinalIgnoreCase))
            {
                result = result.Where(a => a.Category == "Safety & Policies");
            }
            else if (cat.Equals("Getting Started", StringComparison.OrdinalIgnoreCase) || cat.Equals("Aan de slag", StringComparison.OrdinalIgnoreCase))
            {
                result = result.Where(a => a.Category == "Getting Started");
            }
            else
            {
                result = result.Where(a => a.Category.Equals(cat, StringComparison.OrdinalIgnoreCase));
            }
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            string q = query.Trim();
            result = result.Where(a =>
                a.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                a.Summary.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                a.Content.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                a.Keywords.Any(k => k.Contains(q, StringComparison.OrdinalIgnoreCase)));
        }

        return result.ToList();
    }
}
