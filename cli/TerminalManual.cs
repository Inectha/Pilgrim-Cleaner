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

namespace LuminaCleaner.Cli;

public static class TerminalManual
{
    public static int RenderManual(string? topic, bool useColor)
    {
        string normalized = (topic ?? "overview").Trim().ToLowerInvariant();

        switch (normalized)
        {
            case "overview":
            case "general":
            case "all":
                RenderOverview(useColor);
                return 0;

            case "profiles":
            case "profile":
            case "server":
            case "rds":
                RenderProfiles(useColor);
                return 0;

            case "duplicates":
            case "duplicate":
            case "dupes":
                RenderDuplicates(useColor);
                return 0;

            case "spacelens":
            case "space":
            case "large":
                RenderSpaceLens(useColor);
                return 0;

            case "tools":
            case "utilities":
            case "startup":
            case "drivers":
            case "whql":
                RenderTools(useColor);
                return 0;

            case "security":
            case "safety":
            case "edr":
            case "applocker":
                RenderSecurity(useColor);
                return 0;

            case "automation":
            case "rmm":
            case "intune":
            case "powershell":
                RenderAutomation(useColor);
                return 0;

            case "exitcodes":
            case "exit":
            case "codes":
                RenderExitCodes(useColor);
                return 0;

            default:
                WriteLineColored($"Unknown manual topic '{topic}'. Available topics: overview, profiles, duplicates, spacelens, tools, security, automation, exitcodes", ConsoleColor.Yellow, useColor);
                Console.WriteLine();
                RenderOverview(useColor);
                return 4;
        }
    }

    private static void RenderOverview(bool useColor)
    {
        WriteHeader("PILGRIM(1)               Pilgrim Cleaner Sysadmin Manual               PILGRIM(1)", useColor);
        Console.WriteLine();
        WriteSection("NAME", useColor);
        Console.WriteLine("    pilgrim - Non-destructive Windows 10/11 & Windows Server storage maintenance CLI");
        Console.WriteLine();

        WriteSection("SYNOPSIS", useColor);
        Console.WriteLine("    pilgrim audit      [--profile <name>] [--modules <list>] [--all-profiles] [--json]");
        Console.WriteLine("    pilgrim clean      [--profile <name>] [--dry-run] [--force] [--all-profiles] [--json]");
        Console.WriteLine("    pilgrim status     [--json]");
        Console.WriteLine("    pilgrim modules    [--json]");
        Console.WriteLine("    pilgrim duplicates [<path>] [--keep oldest|newest] [--recycle] [--force] [--json]");
        Console.WriteLine("    pilgrim spacelens  [<path>] [--min-size-mb <N>] [--json]");
        Console.WriteLine("    pilgrim startup    [--json]");
        Console.WriteLine("    pilgrim drivers    [--check-whql] [--json]");
        Console.WriteLine("    pilgrim man        [overview | profiles | security | automation | exitcodes]");
        Console.WriteLine();

        WriteSection("DESCRIPTION", useColor);
        Console.WriteLine("    Pilgrim Cleaner CLI is engineered for Windows System Administrators, MSPs, and");
        Console.WriteLine("    DevOps engineers managing Windows Workstations, Windows Servers (2019/2022/2025),");
        Console.WriteLine("    and multi-user Remote Desktop Services (RDS / Citrix / Azure Virtual Desktop).");
        Console.WriteLine();
        Console.WriteLine("    Core Enterprise Principles:");
        Console.WriteLine("      * Zero Registry Tampering: Never modifies or deletes Windows Registry keys.");
        Console.WriteLine("      * Zero RAM Flushing Hacks: Never calls EmptyWorkingSet to fake memory metrics.");
        Console.WriteLine("      * 24-Hour Safety Buffer:   Strictly preserves files created/modified <24h ago");
        Console.WriteLine("                                 to protect active MSI/Windows Updates and user sessions.");
        Console.WriteLine("      * Junction Defense:        SafeFileOps refuses user-planted NTFS junctions/symlinks");
        Console.WriteLine("                                 and verifies real handle paths before unlinking.");
        Console.WriteLine("      * EDR & AppLocker Safe:    Uses atomic Win32/BCL file operations without packers");
        Console.WriteLine("                                 or pre-delete ReadWrite streams.");
        Console.WriteLine();

        WriteSection("OPTIONS", useColor);
        Console.WriteLine("    --profile <name>         Apply preset: workstation | server-safe | rds");
        Console.WriteLine("    --modules <id1,id2>      Comma-separated list of module IDs to include");
        Console.WriteLine("    --exclude <id1,id2>      Comma-separated list of module IDs to exclude");
        Console.WriteLine("    --exclude-path <p1,p2>   Comma-separated folder/file path patterns to whitelist");
        Console.WriteLine("    --all-profiles           Enumerate all user profiles in C:\\Users\\* (Requires Admin)");
        Console.WriteLine("    --min-age <hours>        Safety buffer in hours for temp/cache files (Default: 24)");
        Console.WriteLine("    --server-log-days <days> Retention window in days for IIS/CBS logs (Default: 14)");
        Console.WriteLine("    --include-dism           Include WinSxS DISM /StartComponentCleanup (Admin)");
        Console.WriteLine("    --dry-run, --what-if     Simulate cleanup without deleting any files");
        Console.WriteLine("    --force, -y              Execute cleanup without interactive confirmation prompt");
        Console.WriteLine("    --keep <oldest|newest>   Preserve oldest or newest file when finding/recycling duplicates");
        Console.WriteLine("    --recycle                Move redundant duplicate copies to Windows Recycle Bin");
        Console.WriteLine("    --json                   Emit machine-readable JSON to stdout (suppresses banners)");
        Console.WriteLine("    --event-log              Write execution report to Windows Application Event Log");
        Console.WriteLine("    --log-file <path>        Append execution summary and module details to log file");
        Console.WriteLine("    --fail-threshold-gb <n>  Exit with code 2 during audit if recoverable > N GB");
        Console.WriteLine("    --no-color               Disable ANSI color output");
        Console.WriteLine();

        WriteSection("MANUAL TOPICS", useColor);
        Console.WriteLine("    Run 'pilgrim man <topic>' for detailed administrator documentation:");
        Console.WriteLine("      pilgrim man profiles     Preset profiles for Windows Server, RDS & Workstations");
        Console.WriteLine("      pilgrim man duplicates   Two-tier SHA-256 duplicate auditing, recycling & keep strategies");
        Console.WriteLine("      pilgrim man spacelens    Large and ancient user files library exploration");
        Console.WriteLine("      pilgrim man tools        Startup accelerator and certified WHQL driver health tools");
        Console.WriteLine("      pilgrim man security     EDR/Defender, AppLocker/WDAC, NTFS junctions & SYSTEM context");
        Console.WriteLine("      pilgrim man automation   PowerShell, Intune, NinjaOne/Datto RMM script examples");
        Console.WriteLine("      pilgrim man exitcodes    Standardized exit codes for scripting and monitoring");
    }

    private static void RenderProfiles(bool useColor)
    {
        WriteHeader("PILGRIM-PROFILES(7)      Pilgrim Cleaner Sysadmin Manual      PILGRIM-PROFILES(7)", useColor);
        Console.WriteLine();
        WriteSection("EXECUTION PROFILES (--profile)", useColor);
        Console.WriteLine("    1. server-safe   (Recommended for Windows Server 2019 / 2022 / 2025)");
        Console.WriteLine("       * Modules:      system_temp, crash_dumps, delivery_optimization, server_logs");
        Console.WriteLine("       * Excluded:     browser_cache, developer_caches, recycle_bin, dism_component_store");
        Console.WriteLine("       * Guardrails:   24h buffer on temp/updates + 14-day retention on IIS & CBS logs.");
        Console.WriteLine("       * Example:      pilgrim clean --profile server-safe --force --event-log");
        Console.WriteLine();
        Console.WriteLine("    2. rds           (Recommended for Terminal Servers / Citrix / AVD Multi-Session)");
        Console.WriteLine("       * Modules:      system_temp, crash_dumps, browser_cache, recycle_bin,");
        Console.WriteLine("                       delivery_optimization, server_logs");
        Console.WriteLine("       * Multi-User:   Automatically enables --all-profiles to scan C:\\Users\\*");
        Console.WriteLine("       * Guardrails:   24h safety buffer ensures active logged-in RDS users never lose");
        Console.WriteLine("                       in-progress session files, cookies, or active downloads.");
        Console.WriteLine("       * Example:      pilgrim clean --profile rds --force --event-log");
        Console.WriteLine();
        Console.WriteLine("    3. workstation   (Recommended for Windows 10 & Windows 11 Endpoints)");
        Console.WriteLine("       * Modules:      system_temp, crash_dumps, browser_cache, recycle_bin,");
        Console.WriteLine("                       delivery_optimization, developer_caches");
        Console.WriteLine("       * Example:      pilgrim clean --profile workstation --force");
    }

    private static void RenderDuplicates(bool useColor)
    {
        WriteHeader("PILGRIM-DUPLICATES(7)     Pilgrim Cleaner Sysadmin Manual     PILGRIM-DUPLICATES(7)", useColor);
        Console.WriteLine();
        WriteSection("CRYPTOGRAPHIC SHA-256 DUPLICATE FINDER", useColor);
        Console.WriteLine("    pilgrim duplicates [<path>...] [--keep oldest|newest] [--recycle] [--force] [--dry-run] [--json]");
        Console.WriteLine();
        Console.WriteLine("    DESCRIPTION:");
        Console.WriteLine("      Scans user libraries or custom target folders for byte-identical files using a high-");
        Console.WriteLine("      speed two-tier algorithm:");
        Console.WriteLine("        1. Size Partitioning: Fast group-by exact byte length.");
        Console.WriteLine("        2. Prefix Verification: 4 KB head-byte fast-check.");
        Console.WriteLine("        3. Cryptographic SHA-256: Only files matching steps 1 & 2 are hashed fully.");
        Console.WriteLine();
        Console.WriteLine("    DEFAULT TARGETS:");
        Console.WriteLine("      If no paths are passed, scans: %USERPROFILE%\\Downloads, Documents, and Desktop.");
        Console.WriteLine("      Custom targets can be specified as positional arguments: pilgrim duplicates D:\\Data E:\\Photos");
        Console.WriteLine();
        Console.WriteLine("    OPTIONS:");
        Console.WriteLine("      --keep <oldest|newest>  Preserve the oldest (default) or newest modified file.");
        Console.WriteLine("      --recycle               Send redundant copies to the Windows Recycle Bin.");
        Console.WriteLine("      --dry-run               Simulate duplicate recycling without moving files.");
        Console.WriteLine("      --force, -y             Bypass interactive confirmation prompt [y/N].");
        Console.WriteLine("                              Required for unattended scripts or when using --json.");
        Console.WriteLine("      --json                  Emit machine-readable JSON structure with SHA-256 hashes.");
        Console.WriteLine();
        Console.WriteLine("    SAFETY GUARDRAILS:");
        Console.WriteLine("      * Overlapping Roots:    Nested or redundant roots (e.g. D:\\Data and D:\\Data\\Sub)");
        Console.WriteLine("                              are de-duplicated so a file is never paired with itself.");
        Console.WriteLine("      * Separate File Check:  A copy is only recycled if the kept file is physically verified");
        Console.WriteLine("                              to exist, differ in path, and match in exact byte length.");
        Console.WriteLine("      * Network/Removable:    Refuses recycling on drives lacking a Windows Recycle Bin");
        Console.WriteLine("                              to prevent accidental permanent deletion.");
        Console.WriteLine();
        Console.WriteLine("    EXAMPLES:");
        Console.WriteLine("      pilgrim duplicates");
        Console.WriteLine("      pilgrim duplicates \"D:\\Media\\Photos\" --keep oldest --recycle");
        Console.WriteLine("      pilgrim duplicates \"C:\\Shares\\Data\" --recycle --dry-run");
        Console.WriteLine("      pilgrim duplicates \"D:\\Backup\" --keep newest --recycle --force --json");
    }

    private static void RenderSpaceLens(bool useColor)
    {
        WriteHeader("PILGRIM-SPACELENS(7)      Pilgrim Cleaner Sysadmin Manual      PILGRIM-SPACELENS(7)", useColor);
        Console.WriteLine();
        WriteSection("LARGE & ANCIENT USER FILES EXPLORER", useColor);
        Console.WriteLine("    pilgrim spacelens [<path>...] [--min-size-mb <N>] [--json]");
        Console.WriteLine();
        Console.WriteLine("    DESCRIPTION:");
        Console.WriteLine("      Catalogs storage across user libraries or custom target folders to identify disk-space");
        Console.WriteLine("      hogs, stale video files, ISO images, virtual disks, and installer archives.");
        Console.WriteLine();
        Console.WriteLine("    DEFAULT TARGETS:");
        Console.WriteLine("      If no paths are passed, scans: %USERPROFILE%\\Downloads, Documents, Videos, and Desktop.");
        Console.WriteLine();
        Console.WriteLine("    OPTIONS:");
        Console.WriteLine("      --min-size-mb <N>       Minimum file size threshold in Megabytes (Default: 50 MB).");
        Console.WriteLine("      --json                  Emit JSON array sorted by size in descending order.");
        Console.WriteLine();
        Console.WriteLine("    EXAMPLES:");
        Console.WriteLine("      pilgrim spacelens");
        Console.WriteLine("      pilgrim spacelens \"D:\\VirtualMachines\" --min-size-mb 1024");
        Console.WriteLine("      pilgrim spacelens --min-size-mb 500 --json");
    }

    private static void RenderTools(bool useColor)
    {
        WriteHeader("PILGRIM-TOOLS(7)          Pilgrim Cleaner Sysadmin Manual          PILGRIM-TOOLS(7)", useColor);
        Console.WriteLine();
        WriteSection("SYSADMIN UTILITIES: STARTUP & WHQL DRIVERS", useColor);
        Console.WriteLine("    1. STARTUP ACCELERATOR (pilgrim startup [--json])");
        Console.WriteLine("       Audits all startup applications across HKLM/HKCU StartupApproved registry keys");
        Console.WriteLine("       and startup folders. Displays application names, boot impact ratings (High,");
        Console.WriteLine("       Medium, Low), target executables, and enabled/disabled binary status.");
        Console.WriteLine();
        Console.WriteLine("    2. CERTIFIED WHQL DRIVER HEALTH (pilgrim drivers [--check-whql] [--json])");
        Console.WriteLine("       Catalogs installed hardware devices across Graphics, Audio, Network, Storage,");
        Console.WriteLine("       and System classes, verifying WHQL compliance via Win32_PnPSignedDriver.");
        Console.WriteLine("       When --check-whql is passed, queries the native Microsoft Windows Update API");
        Console.WriteLine("       (WUApiLib) for official certified driver updates.");
        Console.WriteLine();
        Console.WriteLine("    EXAMPLES:");
        Console.WriteLine("      pilgrim startup");
        Console.WriteLine("      pilgrim startup --json");
        Console.WriteLine("      pilgrim drivers");
        Console.WriteLine("      pilgrim drivers --check-whql");
    }

    private static void RenderSecurity(bool useColor)
    {
        WriteHeader("PILGRIM-SECURITY(7)      Pilgrim Cleaner Sysadmin Manual      PILGRIM-SECURITY(7)", useColor);
        Console.WriteLine();
        WriteSection("WINDOWS SERVER, EDR & APPLOCKER ARCHITECTURE", useColor);
        Console.WriteLine("    1. CrowdStrike / Defender for Endpoint / SentinelOne Compatibility:");
        Console.WriteLine("       Many legacy cleaners trigger behavioral ransomware alerts because they open");
        Console.WriteLine("       thousands of files with FileAccess.ReadWrite streams to test file locks before");
        Console.WriteLine("       unlinking them. Pilgrim Cleaner uses direct atomic file deletion and relies on");
        Console.WriteLine("       native Windows kernel sharing-violation checks, avoiding false-positive EDR blocks.");
        Console.WriteLine();
        Console.WriteLine("    2. AppLocker & Windows Defender Application Control (WDAC):");
        Console.WriteLine("       The setup wizard (PilgrimCleanerSetup.exe) installs machine-wide, so the");
        Console.WriteLine("       binaries reside in 'C:\\Program Files\\Pilgrim Cleaner\\', satisfying default");
        Console.WriteLine("       AppLocker and WDAC Program Files path rules.");
        Console.WriteLine();
        Console.WriteLine("    3. Running under NT AUTHORITY\\SYSTEM (Intune / RMM / Scheduled Tasks):");
        Console.WriteLine("       When invoked under the SYSTEM account, %TEMP% and %USERPROFILE% point to");
        Console.WriteLine("       'C:\\Windows\\System32\\config\\systemprofile'. Pilgrim Cleaner automatically");
        Console.WriteLine("       detects SYSTEM context and enumerates all real user profiles in C:\\Users\\*");
        Console.WriteLine("       (skipping junctions, ReparsePoints, Default, and Public profiles).");
        Console.WriteLine();
        Console.WriteLine("    4. NTFS Junction & Symlink Redirection Protection (SafeFileOps):");
        Console.WriteLine("       When running elevated or as SYSTEM, an unprivileged user could plant junctions");
        Console.WriteLine("       (mklink /J) in cache/temp roots to redirect deletions to victim folders.");
        Console.WriteLine("       Pilgrim Cleaner validates that scan roots are not junctions owned by normal");
        Console.WriteLine("       users, and unlinks files via direct handle checks without following symlinks.");
        Console.WriteLine();
        Console.WriteLine("    5. System Binary Defense & Privilege Guardrails (SystemTools):");
        Console.WriteLine("       Windows tools (powershell.exe, schtasks.exe, dism.exe) are invoked exclusively");
        Console.WriteLine("       by absolute System32 paths to prevent current-directory PATH planting.");
    }

    private static void RenderAutomation(bool useColor)
    {
        WriteHeader("PILGRIM-AUTOMATION(7)    Pilgrim Cleaner Sysadmin Manual    PILGRIM-AUTOMATION(7)", useColor);
        Console.WriteLine();
        WriteSection("1. POWERSHELL / RMM JSON TELEMETRY PIPELINE", useColor);
        Console.WriteLine("    $audit = pilgrim audit --profile server-safe --json | ConvertFrom-Json");
        Console.WriteLine("    if ($audit.totalRecoverableBytes -gt 5GB) {");
        Console.WriteLine("        $clean = pilgrim clean --profile server-safe --force --event-log --json | ConvertFrom-Json");
        Console.WriteLine("        Write-Output \"Reclaimed $($clean.formattedCleanedBytes) across $($clean.totalCleanedFiles) files.\"");
        Console.WriteLine("    }");
        Console.WriteLine();
        WriteSection("2. RMM THRESHOLD MONITORING CHECK (NINJAONE / DATTO / ZABBIX)", useColor);
        Console.WriteLine("    # Returns Exit Code 0 if < 10 GB recoverable, or Exit Code 2 (Warning) if >= 10 GB:");
        Console.WriteLine("    pilgrim audit --profile server-safe --fail-threshold-gb 10");
        Console.WriteLine();
        WriteSection("3. WINDOWS SERVER NIGHTLY SCHEDULED TASK (RUN AS SYSTEM)", useColor);
        Console.WriteLine("    schtasks /create /tn \"PilgrimServerMaintenance\" ^");
        Console.WriteLine("      /tr \"\\\"C:\\Program Files\\Pilgrim Cleaner\\pilgrim.exe\\\" clean --profile server-safe --force --event-log\" ^");
        Console.WriteLine("      /sc weekly /d SUN /st 03:00 /ru SYSTEM /rl HIGHEST /f");
    }

    private static void RenderExitCodes(bool useColor)
    {
        WriteHeader("PILGRIM-EXITCODES(7)     Pilgrim Cleaner Sysadmin Manual     PILGRIM-EXITCODES(7)", useColor);
        Console.WriteLine();
        WriteSection("STANDARDIZED EXIT CODES ($LASTEXITCODE / %ERRORLEVEL%)", useColor);
        Console.WriteLine("    Code   Meaning                       Description");
        Console.WriteLine("    ----   ---------------------------   ---------------------------------------------------");
        Console.WriteLine("    0      OK / Success                  Audit or cleanup succeeded; within threshold.");
        Console.WriteLine("    1      Cleaned (Space Reclaimed)     Cleanup succeeded and freed >0 bytes (or use 0).");
        Console.WriteLine("    2      Warning / Threshold Exceeded  Audit exceeded --fail-threshold-gb.");
        Console.WriteLine("    3      Elevation Required            Requested operation requires Administrator rights.");
        Console.WriteLine("    4      Invalid Arguments / Aborted   Invalid flag/module, or clean/recycle aborted without --force in JSON mode.");
        Console.WriteLine("    10     Unhandled Error               Execution failed due to an unexpected exception.");
    }

    private static void WriteHeader(string text, bool useColor)
    {
        WriteLineColored(text, ConsoleColor.Cyan, useColor);
    }

    private static void WriteSection(string title, bool useColor)
    {
        WriteLineColored(title, ConsoleColor.White, useColor);
    }

    private static void WriteLineColored(string text, ConsoleColor color, bool useColor)
    {
        if (useColor)
        {
            var prev = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ForegroundColor = prev;
        }
        else
        {
            Console.WriteLine(text);
        }
    }
}
