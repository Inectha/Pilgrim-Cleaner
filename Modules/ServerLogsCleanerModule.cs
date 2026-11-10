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
using System.Threading;
using System.Threading.Tasks;
using LuminaCleaner.Interop;
using LuminaCleaner.Models;
using Wpf.Ui.Controls;

namespace LuminaCleaner.Modules;

public class ServerLogsCleanerModule : CleanerModuleBase
{
    public override string Id => "server_logs";
    public override string Name => "Server & CBS Archived Logs (IIS / CBS)";
    public override string Description => "Rotated IIS Web Server request logs (W3SVC) and archived Windows Component-Based Servicing (CbsPersist) logs.";
    public override string SecurityNotice => "Server Retention Guardrail: Active CBS.log and recent IIS logs within the retention window (default 14 days) are strictly preserved for auditing.";
    public override CleanerCategory Category => CleanerCategory.ServerLogs;
    public override SymbolRegular IconSymbol => SymbolRegular.Server24;

    public override bool RequiresElevation => true;
    public override bool IsElevationAvailable => NativeMethods.IsRunningAsAdministrator();

    private static List<(string Path, string Pattern)> ResolveServerLogTargets()
    {
        var targets = new List<(string Path, string Pattern)>();

        string? systemDrive = Path.GetPathRoot(Environment.SystemDirectory);
        if (string.IsNullOrWhiteSpace(systemDrive))
            systemDrive = @"C:\";

        string iisLogs = Path.Combine(systemDrive, "inetpub", "logs", "LogFiles");
        if (Directory.Exists(iisLogs))
        {
            targets.Add((iisLogs, "*.log"));
        }

        string cbsLogs = Environment.ExpandEnvironmentVariables(@"%SystemRoot%\Logs\CBS");
        if (Directory.Exists(cbsLogs))
        {
            targets.Add((cbsLogs, "CbsPersist_*.*"));
        }

        return targets;
    }

    public override async Task<ModuleScanResult> ScanAsync(
        EngineOptions options,
        IProgress<ScanProgressReport> progress,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        IsScanning = true;
        ClearResults();

        var discovered = new List<CleanItemModel>();
        int skippedCount = 0;
        long skippedBytes = 0;
        long totalBytes = 0;

        TimeSpan retentionWindow = options.ServerLogRetention > options.AgeSafetyBuffer
            ? options.ServerLogRetention
            : options.AgeSafetyBuffer;

        await Task.Run(() =>
        {
            if (!IsElevationAvailable)
            {
                progress.Report(new ScanProgressReport(
                    "Elevation required: Run as Administrator to audit IIS & CBS server logs.",
                    0,
                    0,
                    Name,
                    100.0));
                return;
            }

            DateTime now = DateTime.UtcNow;
            var targets = ResolveServerLogTargets();

            foreach (var (targetDir, pattern) in targets)
            {
                if (ct.IsCancellationRequested) break;

                foreach (var file in SafeEnumerateFiles(targetDir, pattern, 4))
                {
                    if (ct.IsCancellationRequested) break;

                    // Strictly never touch active CBS.log
                    if (file.Name.Equals("CBS.log", StringComparison.OrdinalIgnoreCase))
                        continue;

                    long length = 0;
                    DateTime lastWriteUtc = DateTime.MinValue;
                    try
                    {
                        length = file.Length;
                        lastWriteUtc = file.LastWriteTimeUtc;
                    }
                    catch
                    {
                        continue;
                    }

                    TimeSpan age = now - lastWriteUtc;
                    if (age < retentionWindow)
                    {
                        skippedCount++;
                        skippedBytes += length;
                        continue;
                    }

                    var item = new CleanItemModel
                    {
                        FullPath = file.FullName,
                        FileName = file.Name,
                        SizeBytes = length,
                        LastModified = file.LastWriteTime,
                        Category = Category,
                        ModuleName = Name,
                        IsFolder = false,
                        IsSelected = true,
                        Status = CleanItemStatus.Scanned
                    };

                    discovered.Add(item);
                    totalBytes += length;

                    progress.Report(new ScanProgressReport(
                        file.FullName,
                        discovered.Count,
                        totalBytes,
                        Name));
                }
            }
        }, ct);

        foreach (var item in discovered)
        {
            AddDiscoveredItem(item);
        }

        SkippedSizeBytes = skippedBytes;
        SkippedCount = skippedCount;
        SkippedReasonSummary = skippedCount > 0
            ? $"{skippedCount:N0} recent server logs ({CleanItemModel.FormatBytes(skippedBytes)}) preserved by {retentionWindow.TotalDays:N0}d retention policy."
            : $"All inspected server logs exceed {retentionWindow.TotalDays:N0}d retention policy.";

        sw.Stop();
        IsScanning = false;
        return new ModuleScanResult(Id, discovered, totalBytes, skippedBytes, skippedCount, sw.Elapsed);
    }

    public override async Task<ModuleCleanResult> CleanAsync(
        EngineOptions options,
        IProgress<CleanProgressReport> progress,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        long cleanedBytes = 0;
        int cleanedCount = 0;
        int skippedCount = 0;
        int failedCount = 0;

        if (!IsElevationAvailable)
        {
            sw.Stop();
            return new ModuleCleanResult(Id, 0, 0, DiscoveredItems.Count, 0, sw.Elapsed);
        }

        var itemsToClean = DiscoveredItems.Where(i => i.IsSelected).ToList();
        int totalToClean = itemsToClean.Count;

        await Task.Run(() =>
        {
            int index = 0;
            foreach (var item in itemsToClean)
            {
                if (ct.IsCancellationRequested) break;

                index++;
                var (success, skipReason) = SafeDeleteFile(item.FullPath, options.DryRun);

                if (success)
                {
                    item.Status = CleanItemStatus.Cleaned;
                    cleanedBytes += item.SizeBytes;
                    cleanedCount++;
                }
                else
                {
                    item.Status = CleanItemStatus.SkippedLocked;
                    item.SkipReason = skipReason;
                    skippedCount++;
                }

                double pct = totalToClean > 0 ? (double)index / totalToClean * 100.0 : 100.0;
                progress.Report(new CleanProgressReport(
                    item.FullPath,
                    cleanedBytes,
                    item.SizeBytes,
                    index,
                    totalToClean,
                    pct,
                    Name));
            }
        }, ct);

        UpdateSelectionMetrics();
        sw.Stop();
        return new ModuleCleanResult(Id, cleanedBytes, cleanedCount, skippedCount, failedCount, sw.Elapsed);
    }
}
