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

public class DeliveryOptimizationCleanerModule : CleanerModuleBase
{
    public override string Id => "delivery_optimization";
    public override string Name => "Windows Update Download Cache";
    public override string Description => "Staged download payloads and delivery optimization cache from completed Windows Updates.";
    public override string SecurityNotice => "Admin-Protected: Safely removes already installed update staging files. Requires administrative privileges.";
    public override CleanerCategory Category => CleanerCategory.DeliveryOptimization;
    public override SymbolRegular IconSymbol => SymbolRegular.ArrowDownload24;

    public override bool RequiresElevation => true;
    public override bool IsElevationAvailable => NativeMethods.IsRunningAsAdministrator();

    private readonly string _downloadCachePath;

    public DeliveryOptimizationCleanerModule()
    {
        _downloadCachePath = Environment.ExpandEnvironmentVariables(@"%SystemRoot%\SoftwareDistribution\Download");
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

        await Task.Run(() =>
        {
            if (!IsElevationAvailable)
            {
                progress.Report(new ScanProgressReport(
                    "Elevation required: Run Pilgrim Cleaner as Administrator to audit SoftwareDistribution cache.",
                    0,
                    0,
                    Name,
                    100.0));
                return;
            }

            if (!Directory.Exists(_downloadCachePath)) return;

            DateTime now = DateTime.UtcNow;

            foreach (var file in SafeEnumerateFiles(_downloadCachePath))
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
        }, ct);

        foreach (var item in discovered)
        {
            AddDiscoveredItem(item);
        }

        SkippedSizeBytes = skippedBytes;
        SkippedCount = skippedCount;
        SkippedReasonSummary = skippedCount > 0
            ? $"{skippedCount:N0} active update staging files ({CleanItemModel.FormatBytes(skippedBytes)}) protected by 24h buffer."
            : "All inspected update staging files are older than 24h.";

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

            if (!options.DryRun && Directory.Exists(_downloadCachePath))
            {
                SafeDeleteEmptyDirectories(_downloadCachePath);
            }
        }, ct);

        UpdateSelectionMetrics();
        sw.Stop();
        return new ModuleCleanResult(Id, cleanedBytes, cleanedCount, skippedCount, failedCount, sw.Elapsed);
    }
}
