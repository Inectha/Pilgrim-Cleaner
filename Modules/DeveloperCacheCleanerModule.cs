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
using Wpf.Ui.Controls;

namespace LuminaCleaner.Modules;

public class DeveloperCacheCleanerModule : CleanerModuleBase
{
    public override string Id => "developer_caches";
    public override string Name => "Developer & Package Manager Caches";
    public override string Description => "Reclaim disk space from NuGet, npm/pnpm, pip, and Visual Studio build caches.";
    public override string SecurityNotice => "Safely targets temporary downloaded package tarballs and compiler cache files that rebuild automatically.";
    public override CleanerCategory Category => CleanerCategory.DeveloperCaches;
    public override SymbolRegular IconSymbol => SymbolRegular.Code24;

    private static readonly string[] DeveloperCachePaths =
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "npm-cache"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "pnpm", "store"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "pip", "cache"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cargo", "registry", "cache")
    ];

    public override async Task<ModuleScanResult> ScanAsync(EngineOptions options, IProgress<ScanProgressReport> progress, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        ClearResults();
        IsScanning = true;

        var discovered = new List<CleanItemModel>();
        int skipped = 0;
        long totalBytes = 0;
        long skippedBytes = 0;
        DateTime now = DateTime.UtcNow;

        try
        {
            await Task.Run(() =>
            {
                foreach (string rootPath in DeveloperCachePaths)
                {
                    ct.ThrowIfCancellationRequested();
                    if (!Directory.Exists(rootPath)) continue;

                    foreach (var fi in SafeEnumerateFiles(rootPath))
                    {
                        ct.ThrowIfCancellationRequested();

                        // Check age buffer (default 24h)
                        if (now - fi.LastWriteTimeUtc < options.AgeSafetyBuffer)
                        {
                            skipped++;
                            skippedBytes += fi.Length;
                            continue;
                        }

                        var item = new CleanItemModel
                        {
                            FullPath = fi.FullName,
                            FileName = fi.Name,
                            SizeBytes = fi.Length,
                            LastModified = fi.LastWriteTime,
                            Category = Category,
                            ModuleName = Name,
                            IsSelected = true
                        };

                        discovered.Add(item);
                        totalBytes += fi.Length;
                        AddDiscoveredItem(item);

                        progress?.Report(new ScanProgressReport(
                            fi.FullName,
                            discovered.Count,
                            totalBytes,
                            Name));
                    }
                }
            }, ct);
        }
        catch (OperationCanceledException) { }
        finally
        {
            IsScanning = false;
            sw.Stop();
        }

        SkippedSizeBytes = skippedBytes;
        SkippedCount = skipped;
        SkippedReasonSummary = "Protected recent cache files (<24h old)";

        return new ModuleScanResult(Id, discovered, totalBytes, skippedBytes, skipped, sw.Elapsed);
    }

    public override async Task<ModuleCleanResult> CleanAsync(EngineOptions options, IProgress<CleanProgressReport> progress, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        int cleanedCount = 0;
        int skippedLocked = 0;
        int failedCount = 0;
        long cleanedBytes = 0;

        var targetItems = DiscoveredItems.Where(i => i.IsSelected).ToList();
        int totalTargets = targetItems.Count;

        try
        {
            await Task.Run(() =>
            {
                for (int i = 0; i < targetItems.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var item = targetItems[i];

                    var (success, reason) = SafeDeleteFile(item.FullPath, options.DryRun);
                    if (success)
                    {
                        item.Status = CleanItemStatus.Cleaned;
                        cleanedCount++;
                        cleanedBytes += item.SizeBytes;
                    }
                    else
                    {
                        failedCount++;
                        if (reason != null && reason.Contains("locked", StringComparison.OrdinalIgnoreCase))
                        {
                            item.Status = CleanItemStatus.SkippedLocked;
                            skippedLocked++;
                        }
                        else
                        {
                            item.Status = CleanItemStatus.Failed;
                        }
                    }

                    progress?.Report(new CleanProgressReport(
                        item.FullPath,
                        cleanedBytes,
                        0,
                        i + 1,
                        totalTargets,
                        (double)(i + 1) / totalTargets * 100.0,
                        Name));
                }
            }, ct);
        }
        catch (OperationCanceledException) { }
        finally
        {
            sw.Stop();
        }

        return new ModuleCleanResult(Id, cleanedBytes, cleanedCount, skippedLocked, failedCount, sw.Elapsed);
    }
}
