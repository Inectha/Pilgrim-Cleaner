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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LuminaCleaner.Models;
using LuminaCleaner.Modules;

namespace LuminaCleaner.Services;

public class EngineService : IEngineService
{
    private readonly List<ICleanerModule> _modules;

    public IReadOnlyList<ICleanerModule> Modules => _modules;

    public event Action<LogMessageModel>? LogAppended;

    public EngineService()
    {
        _modules =
        [
            new TempCleanerModule(),
            new CrashDumpsCleanerModule(),
            new BrowserCacheCleanerModule(),
            new RecycleBinCleanerModule(),
            new DeliveryOptimizationCleanerModule(),
            new DeveloperCacheCleanerModule(),
            new ServerLogsCleanerModule(),
            new DismComponentCleanerModule()
        ];
    }

    public void ApplyProfile(EngineOptions options)
    {
        switch (options.Profile)
        {
            case CleanupProfile.ServerSafe:
                SetEnabledModules(["system_temp", "crash_dumps", "delivery_optimization", "server_logs"]);
                break;

            case CleanupProfile.Rds:
                options.AllProfiles = true;
                SetEnabledModules(["system_temp", "crash_dumps", "browser_cache", "recycle_bin", "delivery_optimization", "server_logs"]);
                break;

            case CleanupProfile.Workstation:
                SetEnabledModules(["system_temp", "crash_dumps", "browser_cache", "recycle_bin", "delivery_optimization", "developer_caches"]);
                break;
        }
    }

    public void SetEnabledModules(IEnumerable<string> moduleIds)
    {
        var set = new HashSet<string>(moduleIds, StringComparer.OrdinalIgnoreCase);
        foreach (var m in _modules)
        {
            m.IsEnabled = set.Contains(m.Id);
        }
    }

    public void Log(LogLevel level, string tag, string message, string? details = null)
    {
        var log = new LogMessageModel
        {
            Timestamp = DateTime.Now,
            Level = level,
            Tag = tag,
            Message = message,
            Details = details
        };
        LogAppended?.Invoke(log);
    }

    public void ResetAll()
    {
        foreach (var m in _modules)
        {
            m.ClearResults();
        }
    }

    public async Task<List<ModuleScanResult>> ScanAllAsync(
        EngineOptions options,
        IProgress<ScanProgressReport> progress,
        CancellationToken ct)
    {
        var results = new List<ModuleScanResult>();
        var totalSw = Stopwatch.StartNew();

        int enabledCount = _modules.Count(m => m.IsEnabled);
        Log(LogLevel.Info, "[AUDIT - START]", $"Starting storage audit across {enabledCount} active modules...");

        long runningTotalBytes = 0;
        int runningTotalItems = 0;
        long runningSkippedBytes = 0;
        int runningSkippedCount = 0;
        int moduleIndex = 0;

        foreach (var module in _modules)
        {
            if (ct.IsCancellationRequested)
            {
                Log(LogLevel.Warning, "[AUDIT - CANCELLED]", "Audit operation was cancelled by the user.");
                break;
            }

            if (!module.IsEnabled)
            {
                Log(LogLevel.Info, "[MODULE - DISABLED]", $"Skipping disabled module: {module.Name}");
                continue;
            }

            moduleIndex++;
            double moduleBasePct = (double)(moduleIndex - 1) / Math.Max(1, enabledCount) * 100.0;
            double moduleSpanPct = 100.0 / Math.Max(1, enabledCount);

            if (module.RequiresElevation && !module.IsElevationAvailable)
            {
                Log(LogLevel.Warning, "[ELEVATION REQ]", $"Module '{module.Name}' requires Administrator privileges. Gracefully bypassing.");
                continue;
            }

            Log(LogLevel.Info, "[AUDITING]", $"Inspecting {module.Name}...");

            var throttler = new ThrottledProgress<ScanProgressReport>(
                TimeSpan.FromMilliseconds(40),
                p =>
                {
                    double currentProgress = Math.Min(99.0, moduleBasePct + (moduleSpanPct * 0.5));
                    progress.Report(new ScanProgressReport(
                        p.CurrentPath,
                        runningTotalItems + p.ItemsFound,
                        runningTotalBytes + p.TotalBytesFound,
                        module.Name,
                        currentProgress));
                });

            var result = await module.ScanAsync(options, throttler, ct);
            throttler.Flush();

            runningTotalBytes += result.TotalBytes;
            runningTotalItems += result.Items.Count(i => i.Status == CleanItemStatus.Scanned);
            runningSkippedBytes += result.SkippedBytes;
            runningSkippedCount += result.SkippedCount;
            results.Add(result);

            double moduleEndPct = (double)moduleIndex / Math.Max(1, enabledCount) * 100.0;
            progress.Report(new ScanProgressReport(
                $"Completed {module.Name}",
                runningTotalItems,
                runningTotalBytes,
                module.Name,
                moduleEndPct));

            Log(LogLevel.Success, "[AUDIT - MODULE]",
                $"{module.Name}: Discovered {result.Items.Count(i => i.Status == CleanItemStatus.Scanned):N0} recoverable files ({CleanItemModel.FormatBytes(result.TotalBytes)}). {result.SkippedCount:N0} protected items ({CleanItemModel.FormatBytes(result.SkippedBytes)}).");
        }

        totalSw.Stop();

        // Send guaranteed 100% completion report
        progress.Report(new ScanProgressReport(
            "Audit complete",
            runningTotalItems,
            runningTotalBytes,
            "All Modules Audited",
            100.0));

        Log(LogLevel.Success, "[AUDIT - COMPLETE]",
            $"Audit finished (100%). Total recoverable: {runningTotalItems:N0} items ({CleanItemModel.FormatBytes(runningTotalBytes)}). {runningSkippedCount:N0} protected files ({CleanItemModel.FormatBytes(runningSkippedBytes)}) preserved in {totalSw.Elapsed.TotalSeconds:N1}s.");

        return results;
    }

    public async Task<List<ModuleCleanResult>> CleanSelectedAsync(
        EngineOptions options,
        IProgress<CleanProgressReport> progress,
        CancellationToken ct)
    {
        var results = new List<ModuleCleanResult>();
        var totalSw = Stopwatch.StartNew();

        int totalSelectedItems = _modules.Where(m => m.IsEnabled).Sum(m => m.SelectedCount);
        long totalSelectedBytes = _modules.Where(m => m.IsEnabled).Sum(m => m.SelectedSizeBytes);

        Log(LogLevel.Info, "[CLEAN - START]",
            $"Executing storage purge: {totalSelectedItems:N0} items ({CleanItemModel.FormatBytes(totalSelectedBytes)}) queued across enabled modules...");

        long runningCleanedBytes = 0;
        int runningProcessedItems = 0;

        foreach (var module in _modules)
        {
            if (ct.IsCancellationRequested)
            {
                Log(LogLevel.Warning, "[CLEAN - CANCELLED]", "Purge operation was cancelled by the user.");
                break;
            }

            if (!module.IsEnabled || module.SelectedCount == 0)
                continue;

            Log(LogLevel.Info, "[CLEANING]", $"Purging selected targets from {module.Name}...");

            var throttler = new ThrottledProgress<CleanProgressReport>(
                TimeSpan.FromMilliseconds(40),
                p =>
                {
                    int overallProcessed = runningProcessedItems + p.ItemsProcessed;
                    double overallPct = totalSelectedItems > 0 ? (double)overallProcessed / totalSelectedItems * 100.0 : 100.0;

                    progress.Report(new CleanProgressReport(
                        p.CurrentFile,
                        runningCleanedBytes + p.CleanedBytes,
                        p.SkippedBytes,
                        overallProcessed,
                        totalSelectedItems,
                        overallPct,
                        module.Name));
                });

            var result = await module.CleanAsync(options, throttler, ct);
            throttler.Flush();

            runningCleanedBytes += result.CleanedBytes;
            runningProcessedItems += (result.CleanedCount + result.SkippedCount + result.FailedCount);
            results.Add(result);

            Log(LogLevel.Success, "[PURGED - MODULE]",
                $"{module.Name}: Freed {CleanItemModel.FormatBytes(result.CleanedBytes)} ({result.CleanedCount:N0} files deleted, {result.SkippedCount:N0} safely skipped). (Elapsed: {result.Elapsed.TotalMilliseconds:N0}ms)");
        }

        totalSw.Stop();

        // Guaranteed 100% finish report
        progress.Report(new CleanProgressReport(
            "Purge complete",
            runningCleanedBytes,
            0,
            totalSelectedItems,
            totalSelectedItems,
            100.0,
            "Finished"));

        Log(LogLevel.Success, "[CLEAN - COMPLETE]",
            $"Purge finished (100%). Total storage recovered: {CleanItemModel.FormatBytes(runningCleanedBytes)} in {totalSw.Elapsed.TotalSeconds:N1}s.");

        return results;
    }
}

public class ThrottledProgress<T> : IProgress<T>
{
    private readonly TimeSpan _throttleInterval;
    private readonly Action<T> _callback;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private T? _lastValue;
    private bool _hasValue;
    private readonly object _lock = new();

    public ThrottledProgress(TimeSpan throttleInterval, Action<T> callback)
    {
        _throttleInterval = throttleInterval;
        _callback = callback;
    }

    public void Report(T value)
    {
        lock (_lock)
        {
            _lastValue = value;
            _hasValue = true;

            if (_stopwatch.Elapsed >= _throttleInterval)
            {
                _stopwatch.Restart();
                _callback(value);
                _hasValue = false;
            }
        }
    }

    public void Flush()
    {
        lock (_lock)
        {
            if (_hasValue && _lastValue != null)
            {
                _callback(_lastValue);
                _hasValue = false;
            }
        }
    }
}
