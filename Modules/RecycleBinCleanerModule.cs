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

public class RecycleBinCleanerModule : CleanerModuleBase
{
    public override string Id => "recycle_bin";
    public override string Name => "Windows Recycle Bin";
    public override string Description => "Deleted files and folders held in the Windows Shell Recycle Bin across all drives.";
    public override string SecurityNotice => "Native Shell API: Processed strictly via native Windows Shell32 (SHEmptyRecycleBinW) and multi-SID Recycle Bin audit when elevated.";
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
            bool useMultiSidScan = options.AllProfiles && NativeMethods.IsRunningAsAdministrator();

            if (!useMultiSidScan && NativeMethods.QueryRecycleBinTotal(out long bytes, out long items) && items > 0)
            {
                totalBytes = bytes;

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
            else if (useMultiSidScan)
            {
                // Multi-user / SYSTEM context: enumerate $Recycle.Bin across all fixed drives
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (drive.DriveType != DriveType.Fixed || !drive.IsReady) continue;

                    string recycleRoot = Path.Combine(drive.RootDirectory.FullName, "$Recycle.Bin");
                    if (!Directory.Exists(recycleRoot)) continue;

                    foreach (var file in SafeEnumerateFiles(recycleRoot, "*", 5))
                    {
                        if (ct.IsCancellationRequested) break;
                        if (file.Name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;

                        long len = 0;
                        try { len = file.Length; } catch { continue; }

                        var item = new CleanItemModel
                        {
                            FullPath = file.FullName,
                            FileName = file.Name,
                            SizeBytes = len,
                            LastModified = file.LastWriteTime,
                            Category = Category,
                            ModuleName = Name,
                            IsFolder = false,
                            IsSelected = true,
                            Status = CleanItemStatus.Scanned
                        };
                        discovered.Add(item);
                        totalBytes += len;
                    }
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
            if (itemsToClean.Count == 1 && itemsToClean[0].FullPath.StartsWith("RecycleBin:", StringComparison.OrdinalIgnoreCase))
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
            else if (itemsToClean.Count > 0)
            {
                // Multi-SID direct file cleanup + Shell32 call
                if (!options.DryRun)
                {
                    NativeMethods.EmptyRecycleBinSafely();
                }

                for (int i = 0; i < itemsToClean.Count; i++)
                {
                    if (ct.IsCancellationRequested) break;
                    var item = itemsToClean[i];
                    var (success, reason) = SafeDeleteFile(item.FullPath, options.DryRun);
                    if (success)
                    {
                        item.Status = CleanItemStatus.Cleaned;
                        cleanedBytes += item.SizeBytes;
                        cleanedCount++;
                    }
                    else
                    {
                        item.Status = CleanItemStatus.SkippedLocked;
                        item.SkipReason = reason;
                        skippedCount++;
                    }
                }
            }
        }, ct);

        UpdateSelectionMetrics();
        sw.Stop();
        return new ModuleCleanResult(Id, cleanedBytes, cleanedCount, skippedCount, failedCount, sw.Elapsed);
    }
}
