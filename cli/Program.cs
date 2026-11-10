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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LuminaCleaner.Interop;
using LuminaCleaner.Models;
using LuminaCleaner.Modules;
using LuminaCleaner.Services;

namespace LuminaCleaner.Cli;

public static class Program
{
    public const string CliVersion = "1.1.0";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            if (args.Length == 0)
            {
                return TerminalManual.RenderManual("overview", !Console.IsOutputRedirected);
            }

            string command = args[0].Trim().ToLowerInvariant();
            if (command is "--version" or "-v" or "version")
            {
                Console.WriteLine($"Pilgrim Cleaner CLI v{CliVersion} (.NET {Environment.Version} / {RuntimeInformation.OSDescription})");
                return 0;
            }

            if (command is "--help" or "-h" or "/?" or "help")
            {
                string? topic = args.Length > 1 ? args[1] : "overview";
                return TerminalManual.RenderManual(topic, !Console.IsOutputRedirected);
            }

            if (command is "man" or "manual")
            {
                bool useColor = !Console.IsOutputRedirected && !args.Any(a => a.Equals("--no-color", StringComparison.OrdinalIgnoreCase));
                string? topic = args.Length > 1 && !args[1].StartsWith('-') ? args[1] : "overview";
                return TerminalManual.RenderManual(topic, useColor);
            }

            if (!TryParseArguments(args, out var parsed, out string? parseError))
            {
                WriteError(parseError ?? "Invalid command-line arguments.", parsed.UseColor);
                Console.Error.WriteLine("Run 'pilgrim man' or 'pilgrim --help' for usage instructions.");
                return 4;
            }

            return parsed.Command switch
            {
                "audit" or "scan" => await ExecuteAuditAsync(parsed, cts.Token),
                "clean" or "purge" => await ExecuteCleanAsync(parsed, cts.Token),
                "status" or "vitals" or "info" => await ExecuteStatusAsync(parsed),
                "modules" or "list" => ExecuteModulesList(parsed),
                _ => UnknownCommand(parsed.Command, parsed.UseColor)
            };
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("\n[CANCELLED] Operation aborted by user signal (Ctrl+C).");
            return 10;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[FATAL ERROR] {ex.Message}");
            return 10;
        }
    }

    private static async Task<int> ExecuteAuditAsync(CliOptions opts, CancellationToken ct)
    {
        var engine = new EngineService();
        var engineOptions = BuildEngineOptions(opts);

        if (opts.AllProfiles && !NativeMethods.IsRunningAsAdministrator())
        {
            if (opts.Json)
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    status = "error",
                    exitCode = 3,
                    error = "The --all-profiles flag requires Administrator or SYSTEM privileges."
                }, JsonOptions));
            }
            else
            {
                WriteError("Elevation Required: '--all-profiles' requires running from an elevated Administrator or SYSTEM shell.", opts.UseColor);
            }
            return 3;
        }

        if (!ConfigureModules(engine, opts, engineOptions, out string? modError))
        {
            WriteError(modError!, opts.UseColor);
            return 4;
        }

        if (!opts.Json)
        {
            RenderBanner("STORAGE AUDIT (NON-DESTRUCTIVE)", opts, engineOptions);
        }

        var progress = new Progress<ScanProgressReport>(p =>
        {
            if (!opts.Json && !Console.IsOutputRedirected)
            {
                Console.Write($"\r  [AUDITING] {p.ModuleName,-42} | Found: {p.ItemsFound,6:N0} files ({CleanItemModel.FormatBytes(p.TotalBytesFound),10})   ");
            }
        });

        var sw = Stopwatch.StartNew();
        var results = await engine.ScanAllAsync(engineOptions, progress, ct);
        sw.Stop();

        if (!opts.Json && !Console.IsOutputRedirected)
        {
            Console.Write("\r" + new string(' ', GetSafeConsoleWidth() - 1) + "\r");
        }

        long totalRecoverableBytes = results.Sum(r => r.TotalBytes);
        int totalRecoverableFiles = results.Sum(r => r.Items.Count(i => i.Status == CleanItemStatus.Scanned));
        long totalProtectedBytes = results.Sum(r => r.SkippedBytes);
        int totalProtectedFiles = results.Sum(r => r.SkippedCount);

        bool thresholdExceeded = opts.FailThresholdGb.HasValue &&
                                 (totalRecoverableBytes / (1024.0 * 1024.0 * 1024.0)) >= opts.FailThresholdGb.Value;

        int exitCode = thresholdExceeded ? 2 : 0;

        if (opts.WriteEventLog)
        {
            EventLogService.WriteEvent(
                thresholdExceeded ? EventLogService.EventIdThresholdWarning : EventLogService.EventIdAuditSuccess,
                thresholdExceeded ? EventLogEntryType.Warning : EventLogEntryType.Information,
                $"Storage Audit finished in {sw.Elapsed.TotalSeconds:N1}s. Recoverable: {CleanItemModel.FormatBytes(totalRecoverableBytes)} ({totalRecoverableFiles:N0} files). Protected by safety buffer: {CleanItemModel.FormatBytes(totalProtectedBytes)} ({totalProtectedFiles:N0} files). Profile: {engineOptions.Profile}.");
        }

        if (!string.IsNullOrWhiteSpace(opts.LogFile))
        {
            AppendLogFile(opts.LogFile!, "AUDIT", engineOptions, totalRecoverableBytes, totalRecoverableFiles, totalProtectedBytes, totalProtectedFiles, sw.Elapsed);
        }

        if (opts.Json)
        {
            var payload = new
            {
                command = "audit",
                version = CliVersion,
                computerName = Environment.MachineName,
                userName = Environment.UserName,
                isAdministrator = NativeMethods.IsRunningAsAdministrator(),
                profile = engineOptions.Profile.ToString(),
                allProfiles = engineOptions.AllProfiles,
                targetProfilesCount = UserProfileResolver.GetTargetUserProfiles(engineOptions.AllProfiles).Count,
                ageSafetyBufferHours = engineOptions.AgeSafetyBuffer.TotalHours,
                elapsedSeconds = Math.Round(sw.Elapsed.TotalSeconds, 2),
                totalRecoverableBytes,
                formattedRecoverableBytes = CleanItemModel.FormatBytes(totalRecoverableBytes),
                totalRecoverableFiles,
                totalProtectedBytes,
                formattedProtectedBytes = CleanItemModel.FormatBytes(totalProtectedBytes),
                totalProtectedFiles,
                thresholdExceeded,
                exitCode,
                modules = engine.Modules.Where(m => m.IsEnabled).Select(m => new
                {
                    id = m.Id,
                    name = m.Name,
                    requiresElevation = m.RequiresElevation,
                    elevationAvailable = m.IsElevationAvailable,
                    recoverableBytes = m.TotalSizeBytes,
                    formattedRecoverable = m.FormattedTotalSize,
                    recoverableFiles = m.ItemCount,
                    protectedBytes = m.SkippedSizeBytes,
                    protectedFiles = m.SkippedCount
                })
            };
            Console.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
            return exitCode;
        }

        RenderAuditTable(engine.Modules.Where(m => m.IsEnabled), totalRecoverableBytes, totalRecoverableFiles, totalProtectedBytes, totalProtectedFiles, sw.Elapsed, opts.UseColor);

        if (thresholdExceeded)
        {
            Console.WriteLine();
            WriteColored($"  [ALERT] Recoverable storage ({CleanItemModel.FormatBytes(totalRecoverableBytes)}) exceeds threshold ({opts.FailThresholdGb:N1} GB). Returning exit code 2.\n", ConsoleColor.Yellow, opts.UseColor);
        }

        return exitCode;
    }

    private static async Task<int> ExecuteCleanAsync(CliOptions opts, CancellationToken ct)
    {
        var engine = new EngineService();
        var engineOptions = BuildEngineOptions(opts);

        if (opts.AllProfiles && !NativeMethods.IsRunningAsAdministrator())
        {
            if (opts.Json)
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    status = "error",
                    exitCode = 3,
                    error = "The --all-profiles flag requires Administrator or SYSTEM privileges."
                }, JsonOptions));
            }
            else
            {
                WriteError("Elevation Required: '--all-profiles' requires running from an elevated Administrator or SYSTEM shell.", opts.UseColor);
            }
            return 3;
        }

        if (!ConfigureModules(engine, opts, engineOptions, out string? modError))
        {
            WriteError(modError!, opts.UseColor);
            return 4;
        }

        if (!opts.Json)
        {
            RenderBanner(engineOptions.DryRun ? "STORAGE CLEANUP (DRY-RUN SIMULATION)" : "STORAGE CLEANUP & PURGE", opts, engineOptions);
        }

        var scanProgress = new Progress<ScanProgressReport>(p =>
        {
            if (!opts.Json && !Console.IsOutputRedirected)
            {
                Console.Write($"\r  [PHASE 1/2: AUDIT] {p.ModuleName,-36} | Found: {p.ItemsFound,6:N0} files ({CleanItemModel.FormatBytes(p.TotalBytesFound),10})   ");
            }
        });

        var sw = Stopwatch.StartNew();
        var scanResults = await engine.ScanAllAsync(engineOptions, scanProgress, ct);

        if (!opts.Json && !Console.IsOutputRedirected)
        {
            Console.Write("\r" + new string(' ', GetSafeConsoleWidth() - 1) + "\r");
        }

        long totalRecoverableBytes = scanResults.Sum(r => r.TotalBytes);
        int totalRecoverableFiles = scanResults.Sum(r => r.Items.Count(i => i.Status == CleanItemStatus.Scanned));
        long totalProtectedBytes = scanResults.Sum(r => r.SkippedBytes);
        int totalProtectedFiles = scanResults.Sum(r => r.SkippedCount);

        if (!opts.Json)
        {
            RenderAuditTable(engine.Modules.Where(m => m.IsEnabled), totalRecoverableBytes, totalRecoverableFiles, totalProtectedBytes, totalProtectedFiles, sw.Elapsed, opts.UseColor);
        }

        if (totalRecoverableBytes == 0 && totalRecoverableFiles == 0)
        {
            if (opts.Json)
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    command = "clean",
                    dryRun = engineOptions.DryRun,
                    status = "clean",
                    totalCleanedBytes = 0L,
                    formattedCleanedBytes = "0 B",
                    totalCleanedFiles = 0,
                    exitCode = 0
                }, JsonOptions));
            }
            else
            {
                Console.WriteLine();
                WriteColored("  [OK] System storage is already clean. No eligible files to purge.\n", ConsoleColor.Green, opts.UseColor);
            }
            return 0;
        }

        // Interactive confirmation unless --force / -y or --dry-run or --json is specified
        if (!opts.Force && !engineOptions.DryRun && !opts.Json)
        {
            if (Console.IsInputRedirected)
            {
                WriteError("Unattended session detected without '--force' (-y) or '--dry-run'. Aborting to prevent unintended deletion.", opts.UseColor);
                return 4;
            }

            Console.WriteLine();
            WriteColored($"  Proceed with purging {totalRecoverableFiles:N0} files ({CleanItemModel.FormatBytes(totalRecoverableBytes)})? [y/N]: ", ConsoleColor.Yellow, opts.UseColor);
            string? response = Console.ReadLine()?.Trim();
            if (!string.Equals(response, "y", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(response, "yes", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("  [ABORTED] Cleanup cancelled by administrator. Zero files were modified.");
                return 0;
            }
        }

        var cleanProgress = new Progress<CleanProgressReport>(p =>
        {
            if (!opts.Json && !Console.IsOutputRedirected)
            {
                Console.Write($"\r  [PHASE 2/2: PURGE] {p.ModuleName,-36} | {p.Percentage,5:N1}% | Freed: {CleanItemModel.FormatBytes(p.CleanedBytes),10}   ");
            }
        });

        var cleanResults = await engine.CleanSelectedAsync(engineOptions, cleanProgress, ct);
        sw.Stop();

        if (!opts.Json && !Console.IsOutputRedirected)
        {
            Console.Write("\r" + new string(' ', GetSafeConsoleWidth() - 1) + "\r");
        }

        long totalCleanedBytes = cleanResults.Sum(r => r.CleanedBytes);
        int totalCleanedFiles = cleanResults.Sum(r => r.CleanedCount);
        int totalSkippedLocked = cleanResults.Sum(r => r.SkippedCount);
        int totalFailed = cleanResults.Sum(r => r.FailedCount);

        if (opts.WriteEventLog)
        {
            EventLogService.WriteEvent(
                EventLogService.EventIdCleanSuccess,
                EventLogEntryType.Information,
                $"Storage {(engineOptions.DryRun ? "Dry-Run" : "Cleanup")} finished in {sw.Elapsed.TotalSeconds:N1}s. Reclaimed: {CleanItemModel.FormatBytes(totalCleanedBytes)} ({totalCleanedFiles:N0} files). Locked/Skipped: {totalSkippedLocked:N0}. Protected (<{engineOptions.AgeSafetyBuffer.TotalHours:N0}h): {totalProtectedFiles:N0}. Profile: {engineOptions.Profile}.");
        }

        if (!string.IsNullOrWhiteSpace(opts.LogFile))
        {
            AppendLogFile(opts.LogFile!, engineOptions.DryRun ? "DRY-RUN" : "CLEAN", engineOptions, totalCleanedBytes, totalCleanedFiles, totalProtectedBytes, totalProtectedFiles, sw.Elapsed);
        }

        int exitCode = opts.DetailedExitCode && totalCleanedBytes > 0 ? 1 : 0;

        if (opts.Json)
        {
            var payload = new
            {
                command = "clean",
                version = CliVersion,
                computerName = Environment.MachineName,
                userName = Environment.UserName,
                isAdministrator = NativeMethods.IsRunningAsAdministrator(),
                dryRun = engineOptions.DryRun,
                profile = engineOptions.Profile.ToString(),
                allProfiles = engineOptions.AllProfiles,
                elapsedSeconds = Math.Round(sw.Elapsed.TotalSeconds, 2),
                totalCleanedBytes,
                formattedCleanedBytes = CleanItemModel.FormatBytes(totalCleanedBytes),
                totalCleanedFiles,
                totalSkippedLockedFiles = totalSkippedLocked,
                totalProtectedFiles,
                totalFailedFiles = totalFailed,
                exitCode,
                modules = cleanResults.Select(r => new
                {
                    id = r.ModuleId,
                    cleanedBytes = r.CleanedBytes,
                    formattedCleaned = CleanItemModel.FormatBytes(r.CleanedBytes),
                    cleanedFiles = r.CleanedCount,
                    skippedLockedFiles = r.SkippedCount,
                    failedFiles = r.FailedCount,
                    elapsedMs = Math.Round(r.Elapsed.TotalMilliseconds, 0)
                })
            };
            Console.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
            return exitCode;
        }

        Console.WriteLine();
        string modeTag = engineOptions.DryRun ? "[DRY-RUN COMPLETE]" : "[CLEANUP COMPLETE]";
        WriteColored($"  {modeTag} Reclaimed {CleanItemModel.FormatBytes(totalCleanedBytes)} across {totalCleanedFiles:N0} items in {sw.Elapsed.TotalSeconds:N1}s.\n", ConsoleColor.Green, opts.UseColor);
        if (totalSkippedLocked > 0)
        {
            WriteColored($"  [GUARDRAIL] Safely bypassed {totalSkippedLocked:N0} files currently locked by active processes/services.\n", ConsoleColor.Cyan, opts.UseColor);
        }

        return exitCode;
    }

    private static async Task<int> ExecuteStatusAsync(CliOptions opts)
    {
        var telemetryService = new SystemTelemetryService();
        var vitals = await telemetryService.SampleTelemetryAsync();
        bool isAdmin = NativeMethods.IsRunningAsAdministrator();
        var targetProfiles = UserProfileResolver.GetTargetUserProfiles(includeAllProfiles: isAdmin);

        if (opts.Json)
        {
            var payload = new
            {
                command = "status",
                version = CliVersion,
                computerName = Environment.MachineName,
                osDescription = RuntimeInformation.OSDescription,
                userName = Environment.UserName,
                isAdministrator = isAdmin,
                discoveredUserProfiles = targetProfiles.Count,
                cpu = new
                {
                    name = vitals.CpuName,
                    cores = Environment.ProcessorCount,
                    coresDescription = vitals.CpuCoresDescription,
                    usagePercent = Math.Round(vitals.CpuUsagePercent, 1)
                },
                memory = new
                {
                    totalBytes = vitals.RamTotalBytes,
                    usedBytes = vitals.RamUsedBytes,
                    availableBytes = vitals.RamAvailableBytes,
                    usagePercent = vitals.RamUsagePercent,
                    formattedUsed = vitals.FormattedRamUsed,
                    formattedTotal = vitals.FormattedRamTotal
                },
                drives = vitals.Drives.Select(d => new
                {
                    name = d.DriveLetter,
                    label = d.VolumeLabel,
                    fileSystem = d.FileSystem,
                    totalBytes = d.TotalSizeBytes,
                    freeBytes = d.FreeSizeBytes,
                    usedBytes = d.UsedSizeBytes,
                    usagePercent = Math.Round(d.UsagePercent, 1),
                    formattedFree = d.FormattedFree,
                    formattedTotal = d.FormattedTotal
                })
            };
            Console.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
            return 0;
        }

        WriteColored("==================================================================================\n", ConsoleColor.Cyan, opts.UseColor);
        WriteColored($"  PILGRIM CLEANER CLI v{CliVersion} — SYSTEM & SERVER TELEMETRY\n", ConsoleColor.Cyan, opts.UseColor);
        WriteColored("==================================================================================\n", ConsoleColor.Cyan, opts.UseColor);
        Console.WriteLine($"  Host / OS        : {Environment.MachineName} ({RuntimeInformation.OSDescription})");
        Console.WriteLine($"  Execution Context: {Environment.UserDomainName}\\{Environment.UserName} ({(isAdmin ? "Administrator / Elevated" : "Standard User")})");
        Console.WriteLine($"  User Profiles    : {targetProfiles.Count} profile(s) accessible");
        Console.WriteLine($"  Processor (CPU)  : {vitals.CpuName} ({Environment.ProcessorCount} logical cores) @ {vitals.CpuUsagePercent:N1}% load");
        Console.WriteLine($"  Physical Memory  : {vitals.FormattedRamUsed} / {vitals.FormattedRamTotal} ({vitals.RamUsagePercent}% utilized)");
        Console.WriteLine();
        WriteColored("  LOGICAL STORAGE VOLUMES:\n", ConsoleColor.White, opts.UseColor);
        Console.WriteLine("  --------------------------------------------------------------------------------");
        Console.WriteLine($"  {"Drive",-8} {"Label",-18} {"FS",-8} {"Total Size",14} {"Free Space",14} {"Used %",10}");
        Console.WriteLine("  --------------------------------------------------------------------------------");
        foreach (var d in vitals.Drives)
        {
            ConsoleColor rowColor = d.UsagePercent >= 90 ? ConsoleColor.Red : (d.UsagePercent >= 80 ? ConsoleColor.Yellow : ConsoleColor.Gray);
            WriteColored($"  {d.DriveLetter,-8} {Truncate(d.VolumeLabel, 17),-18} {d.FileSystem,-8} {d.FormattedTotal,14} {d.FormattedFree,14} {d.UsagePercent,9:N1}%\n", rowColor, opts.UseColor);
        }
        Console.WriteLine("  --------------------------------------------------------------------------------");
        return 0;
    }

    private static int ExecuteModulesList(CliOptions opts)
    {
        var engine = new EngineService();
        if (opts.Json)
        {
            var payload = engine.Modules.Select(m => new
            {
                id = m.Id,
                name = m.Name,
                category = m.Category.ToString(),
                requiresElevation = m.RequiresElevation,
                elevationAvailable = m.IsElevationAvailable,
                description = m.Description,
                securityNotice = m.SecurityNotice
            });
            Console.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
            return 0;
        }

        WriteColored("==================================================================================\n", ConsoleColor.Cyan, opts.UseColor);
        WriteColored($"  PILGRIM CLEANER CLI v{CliVersion} — AVAILABLE STORAGE MODULES\n", ConsoleColor.Cyan, opts.UseColor);
        WriteColored("==================================================================================\n", ConsoleColor.Cyan, opts.UseColor);
        Console.WriteLine($"  {"Module ID",-24} {"Admin Req",-11} {"Module Name"}");
        Console.WriteLine("  --------------------------------------------------------------------------------");
        foreach (var m in engine.Modules)
        {
            string adminTag = m.RequiresElevation ? "Yes" : "No";
            Console.WriteLine($"  {m.Id,-24} {adminTag,-11} {m.Name}");
        }
        Console.WriteLine("  --------------------------------------------------------------------------------");
        Console.WriteLine("  Tip: Filter modules via '--modules system_temp,server_logs' or '--exclude browser_cache'");
        return 0;
    }

    private static EngineOptions BuildEngineOptions(CliOptions opts)
    {
        return new EngineOptions
        {
            HeadlessMode = true,
            DryRun = opts.DryRun,
            AllProfiles = opts.AllProfiles,
            AgeSafetyBuffer = TimeSpan.FromHours(opts.MinAgeHours),
            ServerLogRetention = TimeSpan.FromDays(opts.ServerLogDays),
            Profile = opts.Profile
        };
    }

    private static bool ConfigureModules(EngineService engine, CliOptions opts, EngineOptions engineOptions, out string? error)
    {
        error = null;

        // 1. Apply profile defaults first
        if (engineOptions.Profile != CleanupProfile.Default)
        {
            engine.ApplyProfile(engineOptions);
        }
        else
        {
            // By default in CLI, DISM component store is opt-in via --include-dism because it takes minutes
            foreach (var m in engine.Modules)
            {
                if (m.Id == "dism_component_store")
                    m.IsEnabled = opts.IncludeDism;
                else
                    m.IsEnabled = true;
            }
        }

        if (opts.IncludeDism)
        {
            var dism = engine.Modules.FirstOrDefault(m => m.Id == "dism_component_store");
            if (dism != null) dism.IsEnabled = true;
        }

        // 2. Explicit --modules filter
        if (opts.IncludedModules.Count > 0)
        {
            var validIds = engine.Modules.Select(m => m.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var req in opts.IncludedModules)
            {
                if (!validIds.Contains(req))
                {
                    error = $"Unknown module ID '{req}'. Run 'pilgrim modules' to view valid IDs.";
                    return false;
                }
            }
            engine.SetEnabledModules(opts.IncludedModules);
        }

        // 3. Explicit --exclude filter
        if (opts.ExcludedModules.Count > 0)
        {
            foreach (var m in engine.Modules)
            {
                if (opts.ExcludedModules.Contains(m.Id))
                {
                    m.IsEnabled = false;
                }
            }
        }

        return true;
    }

    private static void RenderBanner(string operationTitle, CliOptions opts, EngineOptions engineOptions)
    {
        bool isAdmin = NativeMethods.IsRunningAsAdministrator();
        var profiles = UserProfileResolver.GetTargetUserProfiles(engineOptions.AllProfiles);

        WriteColored("==================================================================================\n", ConsoleColor.Cyan, opts.UseColor);
        WriteColored($"  PILGRIM CLEANER CLI v{CliVersion} — {operationTitle}\n", ConsoleColor.Cyan, opts.UseColor);
        WriteColored("==================================================================================\n", ConsoleColor.Cyan, opts.UseColor);
        Console.WriteLine($"  Host & Context   : {Environment.MachineName} | {Environment.UserName} ({(isAdmin ? "Elevated Admin" : "Standard User")})");
        Console.WriteLine($"  Active Profile   : {engineOptions.Profile} | Scope: {(engineOptions.AllProfiles ? $"All User Profiles ({profiles.Count} found)" : "Current User Profile")}");
        Console.WriteLine($"  Safety Guardrail : {engineOptions.AgeSafetyBuffer.TotalHours:N0}h temp buffer | {engineOptions.ServerLogRetention.TotalDays:N0}d server log retention");
        Console.WriteLine("----------------------------------------------------------------------------------");
    }

    private static void RenderAuditTable(
        IEnumerable<ICleanerModule> modules,
        long totalRecoverableBytes,
        int totalRecoverableFiles,
        long totalProtectedBytes,
        int totalProtectedFiles,
        TimeSpan elapsed,
        bool useColor)
    {
        Console.WriteLine($"  {"Module ID",-22} {"Module Name",-32} {"Files",8} {"Recoverable",14}");
        Console.WriteLine("  --------------------------------------------------------------------------------");

        foreach (var m in modules)
        {
            if (m.RequiresElevation && !m.IsElevationAvailable)
            {
                WriteColored($"  {m.Id,-22} {Truncate(m.Name, 31),-32} {"[SKIP]",8} {"Admin Required",14}\n", ConsoleColor.DarkYellow, useColor);
                continue;
            }

            ConsoleColor color = m.TotalSizeBytes > 0 ? ConsoleColor.White : ConsoleColor.Gray;
            WriteColored($"  {m.Id,-22} {Truncate(m.Name, 31),-32} {m.ItemCount,8:N0} {m.FormattedTotalSize,14}\n", color, useColor);
        }

        Console.WriteLine("  --------------------------------------------------------------------------------");
        WriteColored($"  {"TOTAL RECOVERABLE",-55} {totalRecoverableFiles,8:N0} {CleanItemModel.FormatBytes(totalRecoverableBytes),14}\n", ConsoleColor.Green, useColor);
        WriteColored($"  {"PROTECTED BY SAFETY BUFFER (<24h / Retention)",-55} {totalProtectedFiles,8:N0} {CleanItemModel.FormatBytes(totalProtectedBytes),14}\n", ConsoleColor.Cyan, useColor);
        Console.WriteLine($"  Completed in {elapsed.TotalSeconds:N2}s.");
    }

    private static void AppendLogFile(
        string logFilePath,
        string action,
        EngineOptions options,
        long bytes,
        int files,
        long protectedBytes,
        int protectedFiles,
        TimeSpan elapsed)
    {
        try
        {
            string? dir = Path.GetDirectoryName(Path.GetFullPath(logFilePath));
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string line = $"{DateTime.UtcNow:O} | {Environment.MachineName} | {action} | Profile={options.Profile} | AllProfiles={options.AllProfiles} | Reclaimed/Recoverable={CleanItemModel.FormatBytes(bytes)} ({files} files) | Protected={CleanItemModel.FormatBytes(protectedBytes)} ({protectedFiles} files) | Elapsed={elapsed.TotalSeconds:N2}s{Environment.NewLine}";
            File.AppendAllText(logFilePath, line);
        }
        catch
        {
            // Do not crash CLI execution if log file path is unwritable
        }
    }

    private static bool TryParseArguments(string[] args, out CliOptions options, out string? error)
    {
        options = new CliOptions
        {
            Command = args[0].Trim().ToLowerInvariant(),
            UseColor = !Console.IsOutputRedirected
        };
        error = null;

        for (int i = 1; i < args.Length; i++)
        {
            string arg = args[i].Trim();
            string lower = arg.ToLowerInvariant();

            switch (lower)
            {
                case "--json":
                case "-j":
                    options.Json = true;
                    options.UseColor = false;
                    break;

                case "--no-color":
                    options.UseColor = false;
                    break;

                case "--dry-run":
                case "--what-if":
                case "-n":
                    options.DryRun = true;
                    break;

                case "--force":
                case "--yes":
                case "-y":
                case "-f":
                    options.Force = true;
                    break;

                case "--all-profiles":
                case "--all-users":
                    options.AllProfiles = true;
                    break;

                case "--include-dism":
                    options.IncludeDism = true;
                    break;

                case "--event-log":
                    options.WriteEventLog = true;
                    break;

                case "--detailed-exit-code":
                    options.DetailedExitCode = true;
                    break;

                case "--profile":
                case "-p":
                    if (i + 1 >= args.Length)
                    {
                        error = "Missing value for --profile (expected: workstation, server-safe, or rds).";
                        return false;
                    }
                    string profVal = args[++i].Trim().ToLowerInvariant();
                    switch (profVal)
                    {
                        case "workstation":
                        case "desktop":
                            options.Profile = CleanupProfile.Workstation;
                            break;
                        case "server-safe":
                        case "server":
                            options.Profile = CleanupProfile.ServerSafe;
                            break;
                        case "rds":
                        case "terminal-server":
                        case "citrix":
                        case "avd":
                            options.Profile = CleanupProfile.Rds;
                            options.AllProfiles = true;
                            break;
                        default:
                            error = $"Unknown profile '{profVal}'. Valid profiles: workstation, server-safe, rds.";
                            return false;
                    }
                    break;

                case "--modules":
                case "-m":
                    if (i + 1 >= args.Length)
                    {
                        error = "Missing comma-separated module IDs for --modules.";
                        return false;
                    }
                    foreach (var m in args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        options.IncludedModules.Add(m);
                    }
                    break;

                case "--exclude":
                case "-e":
                    if (i + 1 >= args.Length)
                    {
                        error = "Missing comma-separated module IDs for --exclude.";
                        return false;
                    }
                    foreach (var m in args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        options.ExcludedModules.Add(m);
                    }
                    break;

                case "--min-age":
                    if (i + 1 >= args.Length || !double.TryParse(args[++i], out double hours) || hours < 0)
                    {
                        error = "Invalid value for --min-age. Expected non-negative number of hours (e.g. 24).";
                        return false;
                    }
                    options.MinAgeHours = hours;
                    break;

                case "--server-log-days":
                    if (i + 1 >= args.Length || !double.TryParse(args[++i], out double days) || days < 0)
                    {
                        error = "Invalid value for --server-log-days. Expected non-negative number of days (e.g. 14).";
                        return false;
                    }
                    options.ServerLogDays = days;
                    break;

                case "--fail-threshold-gb":
                    if (i + 1 >= args.Length || !double.TryParse(args[++i], out double threshGb) || threshGb <= 0)
                    {
                        error = "Invalid value for --fail-threshold-gb. Expected positive number in GB (e.g. 10).";
                        return false;
                    }
                    options.FailThresholdGb = threshGb;
                    break;

                case "--log-file":
                case "-l":
                    if (i + 1 >= args.Length)
                    {
                        error = "Missing file path for --log-file.";
                        return false;
                    }
                    options.LogFile = args[++i];
                    break;

                default:
                    error = $"Unrecognized option '{arg}'.";
                    return false;
            }
        }

        return true;
    }

    private static int UnknownCommand(string cmd, bool useColor)
    {
        WriteError($"Unknown command '{cmd}'. Valid commands: audit, clean, status, modules, man.", useColor);
        return 4;
    }

    private static void WriteError(string msg, bool useColor)
    {
        if (useColor)
        {
            var prev = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"[ERROR] {msg}");
            Console.ForegroundColor = prev;
        }
        else
        {
            Console.Error.WriteLine($"[ERROR] {msg}");
        }
    }

    private static void WriteColored(string text, ConsoleColor color, bool useColor)
    {
        if (useColor)
        {
            var prev = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.Write(text);
            Console.ForegroundColor = prev;
        }
        else
        {
            Console.Write(text);
        }
    }

    private static int GetSafeConsoleWidth()
    {
        try
        {
            return Math.Max(85, Console.WindowWidth);
        }
        catch
        {
            return 90;
        }
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= maxLength ? value : value[..(maxLength - 2)] + "..";
    }

    private sealed class CliOptions
    {
        public string Command { get; set; } = "audit";
        public CleanupProfile Profile { get; set; } = CleanupProfile.Default;
        public HashSet<string> IncludedModules { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ExcludedModules { get; } = new(StringComparer.OrdinalIgnoreCase);
        public bool AllProfiles { get; set; }
        public double MinAgeHours { get; set; } = 24.0;
        public double ServerLogDays { get; set; } = 14.0;
        public bool IncludeDism { get; set; }
        public bool DryRun { get; set; }
        public bool Force { get; set; }
        public bool Json { get; set; }
        public bool WriteEventLog { get; set; }
        public bool DetailedExitCode { get; set; }
        public bool UseColor { get; set; } = true;
        public double? FailThresholdGb { get; set; }
        public string? LogFile { get; set; }
    }
}
