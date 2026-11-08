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
using System.Threading;
using System.Threading.Tasks;
using LuminaCleaner.Interop;
using LuminaCleaner.Models;
using Wpf.Ui.Controls;

namespace LuminaCleaner.Modules;

public class RecycleBinCleanerModule : CleanerModuleBase
{
    public override string Id => "recycle_bin";
    public override string Name => "Windows Recycle Bin";
    public override string Description => "Deleted files and folders held in the Windows Shell Recycle Bin across all drives.";
    public override string SecurityNotice => "Native Shell API: Processed strictly via native Windows Shell32 (SHEmptyRecycleBinW) without external shell injection.";
    public override CleanerCategory Category => CleanerCategory.RecycleBin;
    public override SymbolRegular IconSymbol => SymbolRegular.Delete24;

    public override async Task<ModuleScanResult> ScanAsync(
        EngineOptions options,
        IProgress<ScanProgressReport> progress,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        IsScanning = true;
        ClearResults();

        var discovered = new List<CleanItemModel>();
        long totalBytes = 0;
        int skippedCount = 0;

        await Task.Run(() =>
        {
            if (NativeMethods.QueryRecycleBinTotal(out long bytes, out long items))
            {
                totalBytes = bytes;

                if (items > 0)
                {
                    var item = new CleanItemModel
                    {
                        FullPath = @"RecycleBin:\* (All Fixed Drives)",
                        FileName = $"Recycle Bin Payload ({items:N0} items)",
                        SizeBytes = bytes,
                        LastModified = DateTime.Now,
                        Category = Category,
                        ModuleName = Name,
                        IsFolder = true,
                        IsSelected = true,
                        Status = CleanItemStatus.Scanned
                    };

                    discovered.Add(item);

                    progress.Report(new ScanProgressReport(
                        item.FullPath,
                        1,
                        bytes,
                        Name,
                        100.0));
                }
            }
        }, ct);

        foreach (var item in discovered)
        {
            AddDiscoveredItem(item);
        }

        sw.Stop();
        IsScanning = false;
        return new ModuleScanResult(Id, discovered, totalBytes, 0, skippedCount, sw.Elapsed);
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

        await Task.Run(() =>
        {
            if (itemsToClean.Count > 0)
            {
                var item = itemsToClean[0];
                progress.Report(new CleanProgressReport(
                    "Emptying Windows Recycle Bin via Shell32...",
                    0,
                    0,
                    0,
                    1,
                    20.0,
                    Name));

                bool success = true;
                if (!options.DryRun)
                {
                    success = NativeMethods.EmptyRecycleBinSafely();
                }

                if (success)
                {
                    item.Status = CleanItemStatus.Cleaned;
                    cleanedBytes = item.SizeBytes;
                    cleanedCount = 1;
                }
                else
                {
                    item.Status = CleanItemStatus.Failed;
                    item.SkipReason = "Recycle Bin could not be emptied or is in use.";
                    failedCount = 1;
                }

                progress.Report(new CleanProgressReport(
                    "Recycle Bin Emptied",
                    cleanedBytes,
                    0,
                    1,
                    1,
                    100.0,
                    Name));
            }
        }, ct);

        UpdateSelectionMetrics();
        sw.Stop();
        return new ModuleCleanResult(Id, cleanedBytes, cleanedCount, skippedCount, failedCount, sw.Elapsed);
    }
}
