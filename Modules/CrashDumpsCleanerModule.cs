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
using LuminaCleaner.Models;
using LuminaCleaner.Services;
using Wpf.Ui.Controls;

namespace LuminaCleaner.Modules;

public class CrashDumpsCleanerModule : CleanerModuleBase
{
    public override string Id => "crash_dumps";
    public override string Name => "Crash Dumps & Error Reports";
    public override string Description => "Orphaned application memory dump files (.dmp) and Windows Error Reporting (WER) telemetry archives.";
    public override string SecurityNotice => "Zero System Impact: Memory dumps and error telemetry older than the 24h guardrail are safely removed while preserving recent incident dumps.";
    public override CleanerCategory Category => CleanerCategory.CrashDumps;
    public override SymbolRegular IconSymbol => SymbolRegular.Bug24;

    private List<string> ResolveTargetDirectories(bool includeAllProfiles)
    {
        var dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddIfValid(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            {
                dirs.Add(Path.TrimEndingDirectorySeparator(path));
            }
        }

        foreach (var profile in UserProfileResolver.GetTargetUserProfiles(includeAllProfiles))
        {
            string localAppData = Path.Combine(profile, "AppData", "Local");
            AddIfValid(Path.Combine(localAppData, "CrashDumps"));
            AddIfValid(Path.Combine(localAppData, @"Microsoft\Windows\WER\ReportArchive"));
            AddIfValid(Path.Combine(localAppData, @"Microsoft\Windows\WER\ReportQueue"));
            AddIfValid(Path.Combine(localAppData, @"Microsoft\Windows\WER\Temp"));
        }

        string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        AddIfValid(Path.Combine(programData, @"Microsoft\Windows\WER\ReportArchive"));
        AddIfValid(Path.Combine(programData, @"Microsoft\Windows\WER\ReportQueue"));
        AddIfValid(Path.Combine(programData, @"Microsoft\Windows\WER\Temp"));

        string minidump = Environment.ExpandEnvironmentVariables(@"%SystemRoot%\Minidump");
        AddIfValid(minidump);

        return dirs.ToList();
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
        var targetDirs = ResolveTargetDirectories(options.AllProfiles);

        await Task.Run(() =>
        {
            DateTime now = DateTime.UtcNow;

            foreach (var dir in targetDirs)
            {
                if (ct.IsCancellationRequested) break;

                foreach (var file in SafeEnumerateFiles(dir))
                {
                    if (ct.IsCancellationRequested) break;

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
                    if (age < options.AgeSafetyBuffer)
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
            ? $"{skippedCount:N0} recent crash reports ({CleanItemModel.FormatBytes(skippedBytes)}) preserved by 24h buffer."
            : "All inspected crash reports are older than 24h.";

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

            if (!options.DryRun)
            {
                foreach (var dir in ResolveTargetDirectories(options.AllProfiles))
                {
                    if (ct.IsCancellationRequested) break;
                    SafeDeleteEmptyDirectories(dir);
                }
            }
        }, ct);

        UpdateSelectionMetrics();
        sw.Stop();
        return new ModuleCleanResult(Id, cleanedBytes, cleanedCount, skippedCount, failedCount, sw.Elapsed);
    }
}
