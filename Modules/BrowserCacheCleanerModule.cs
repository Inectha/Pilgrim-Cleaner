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

public class BrowserCacheCleanerModule : CleanerModuleBase
{
    public override string Id => "browser_cache";
    public override string Name => "Browser Caches (Non-destructive)";
    public override string Description => "Rendered web pages, script caches, and image assets from Google Chrome and Microsoft Edge.";
    public override string SecurityNotice => "Privacy Guaranteed: Only temporary rendering and JavaScript code caches are cleaned. Passwords, session cookies, bookmarks, and browsing history are NEVER touched.";
    public override CleanerCategory Category => CleanerCategory.BrowserCache;
    public override SymbolRegular IconSymbol => SymbolRegular.Globe24;

    public bool IsChromeRunning => Process.GetProcessesByName("chrome").Length > 0;
    public bool IsEdgeRunning => Process.GetProcessesByName("msedge").Length > 0;

    public string RunningWarningMessage
    {
        get
        {
            var running = new List<string>();
            if (IsChromeRunning) running.Add("Google Chrome");
            if (IsEdgeRunning) running.Add("Microsoft Edge");

            if (running.Count > 0)
            {
                return $"Active: {string.Join(" & ", running)} is currently running. Locked cache files will be safely bypassed.";
            }
            return "Browsers closed (ready for full cache purge).";
        }
    }

    private IEnumerable<string> ResolveCacheDirectories(bool includeAllProfiles)
    {
        var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cacheFolderNames = new[] { "Cache", "Code Cache", "GPUCache", "DawnCache" };

        foreach (var profileRoot in UserProfileResolver.GetTargetUserProfiles(includeAllProfiles))
        {
            string localAppData = Path.Combine(profileRoot, "AppData", "Local");
            if (!Directory.Exists(localAppData)) continue;

            var baseDirs = new[]
            {
                Path.Combine(localAppData, @"Google\Chrome\User Data"),
                Path.Combine(localAppData, @"Microsoft\Edge\User Data")
            };

            foreach (var baseDir in baseDirs)
            {
                if (!Directory.Exists(baseDir)) continue;

                // Check Default profile
                string defaultProfile = Path.Combine(baseDir, "Default");
                if (Directory.Exists(defaultProfile))
                {
                    foreach (var cacheName in cacheFolderNames)
                    {
                        string p = Path.Combine(defaultProfile, cacheName);
                        if (Directory.Exists(p) && yielded.Add(p)) yield return p;
                    }
                }

                // Check additional profiles (Profile 1, Profile 2, etc.)
                string[] subDirs = [];
                try { subDirs = Directory.GetDirectories(baseDir, "Profile *"); } catch { }
                foreach (var profileDir in subDirs)
                {
                    foreach (var cacheName in cacheFolderNames)
                    {
                        string p = Path.Combine(profileDir, cacheName);
                        if (Directory.Exists(p) && yielded.Add(p)) yield return p;
                    }
                }
            }
        }
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
            DateTime now = DateTime.UtcNow;
            var cacheDirs = ResolveCacheDirectories(options.AllProfiles).ToList();

            foreach (var cacheDir in cacheDirs)
            {
                if (ct.IsCancellationRequested) break;

                foreach (var file in SafeEnumerateFiles(cacheDir))
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
            ? $"{skippedCount:N0} active cache files ({CleanItemModel.FormatBytes(skippedBytes)}) protected by 24h safety buffer."
            : "All inspected browser cache files are older than 24h.";

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
                foreach (var dir in ResolveCacheDirectories(options.AllProfiles))
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
