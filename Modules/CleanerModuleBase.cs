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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using LuminaCleaner.Interop;
using LuminaCleaner.Models;
using Wpf.Ui.Controls;

namespace LuminaCleaner.Modules;

public abstract partial class CleanerModuleBase : ObservableObject, ICleanerModule
{
    private readonly object _itemsLock = new();

    // Scan roots that passed the junction/symlink check, mapped to their real location.
    // Only files below one of these roots can be deleted (see SafeDeleteFile).
    private readonly ConcurrentDictionary<string, string> _verifiedRoots = new(StringComparer.OrdinalIgnoreCase);

    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract string SecurityNotice { get; }
    public abstract CleanerCategory Category { get; }
    public abstract SymbolRegular IconSymbol { get; }

    [ObservableProperty]
    private bool _isEnabled = true;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private long _totalSizeBytes;

    [ObservableProperty]
    private int _itemCount;

    [ObservableProperty]
    private long _selectedSizeBytes;

    [ObservableProperty]
    private int _selectedCount;

    [ObservableProperty]
    private long _skippedSizeBytes;

    [ObservableProperty]
    private int _skippedCount;

    [ObservableProperty]
    private string _skippedReasonSummary = "No skipped files";

    public virtual bool RequiresElevation => false;
    public virtual bool IsElevationAvailable => !RequiresElevation || NativeMethods.IsRunningAsAdministrator();

    public string FormattedTotalSize => CleanItemModel.FormatBytes(TotalSizeBytes);
    public string FormattedSelectedSize => CleanItemModel.FormatBytes(SelectedSizeBytes);
    public string FormattedSkippedSize => CleanItemModel.FormatBytes(SkippedSizeBytes);

    public ObservableCollection<CleanItemModel> DiscoveredItems { get; } = [];

    public CleanerModuleBase()
    {
        try
        {
            if (System.Windows.Application.Current != null)
            {
                System.Windows.Data.BindingOperations.EnableCollectionSynchronization(DiscoveredItems, _itemsLock);
            }
        }
        catch
        {
            // Unit test runner without active WPF Dispatcher
        }
    }

    public abstract Task<ModuleScanResult> ScanAsync(EngineOptions options, IProgress<ScanProgressReport> progress, CancellationToken ct);
    public abstract Task<ModuleCleanResult> CleanAsync(EngineOptions options, IProgress<CleanProgressReport> progress, CancellationToken ct);

    public virtual void ClearResults()
    {
        _verifiedRoots.Clear();

        void InternalClear()
        {
            lock (_itemsLock)
            {
                DiscoveredItems.Clear();
                TotalSizeBytes = 0;
                ItemCount = 0;
                SelectedSizeBytes = 0;
                SelectedCount = 0;
                SkippedSizeBytes = 0;
                SkippedCount = 0;
                SkippedReasonSummary = "No skipped files";
                OnPropertyChanged(nameof(FormattedTotalSize));
                OnPropertyChanged(nameof(FormattedSelectedSize));
                OnPropertyChanged(nameof(FormattedSkippedSize));
            }
        }

        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(InternalClear);
        }
        else
        {
            InternalClear();
        }
    }

    public void SelectAll(bool select)
    {
        void InternalSelect()
        {
            lock (_itemsLock)
            {
                foreach (var item in DiscoveredItems)
                {
                    // Only toggle items that are actually recoverable (not skipped/protected)
                    if (item.Status == CleanItemStatus.Scanned)
                    {
                        item.IsSelected = select;
                    }
                }
            }
            UpdateSelectionMetrics();
        }

        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(InternalSelect);
        }
        else
        {
            InternalSelect();
        }
    }

    public void UpdateSelectionMetrics()
    {
        void InternalUpdate()
        {
            long selBytes = 0;
            int selCount = 0;
            lock (_itemsLock)
            {
                foreach (var item in DiscoveredItems)
                {
                    if (item.IsSelected && item.Status == CleanItemStatus.Scanned)
                    {
                        selBytes += item.SizeBytes;
                        selCount++;
                    }
                }
            }
            SelectedSizeBytes = selBytes;
            SelectedCount = selCount;
            OnPropertyChanged(nameof(FormattedSelectedSize));
        }

        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(InternalUpdate);
        }
        else
        {
            InternalUpdate();
        }
    }

    protected void AddDiscoveredItem(CleanItemModel item)
    {
        bool hasGuiDispatcher = System.Windows.Application.Current != null;

        if (hasGuiDispatcher)
        {
            item.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(CleanItemModel.IsSelected))
                {
                    UpdateSelectionMetrics();
                }
            };
        }

        void InternalAdd()
        {
            lock (_itemsLock)
            {
                DiscoveredItems.Add(item);

                if (item.Status == CleanItemStatus.Scanned)
                {
                    TotalSizeBytes += item.SizeBytes;
                    ItemCount++;
                    if (item.IsSelected)
                    {
                        SelectedSizeBytes += item.SizeBytes;
                        SelectedCount++;
                    }
                }
                else
                {
                    SkippedSizeBytes += item.SizeBytes;
                    SkippedCount++;
                }

                if (hasGuiDispatcher)
                {
                    OnPropertyChanged(nameof(FormattedTotalSize));
                    OnPropertyChanged(nameof(FormattedSelectedSize));
                    OnPropertyChanged(nameof(FormattedSkippedSize));
                }
            }
        }

        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(InternalAdd);
        }
        else
        {
            InternalAdd();
        }
    }

    protected IEnumerable<FileInfo> SafeEnumerateFiles(string rootPath, string searchPattern = "*", int maxDepth = 10)
    {
        if (!Directory.Exists(rootPath)) yield break;

        // Refuse roots that are (or sit below) a junction/symlink planted by a non-privileged
        // account; an elevated or SYSTEM run would otherwise clean the link's target.
        string rootKey = Path.TrimEndingDirectorySeparator(rootPath);
        if (!SafeFileOps.TryResolveTrustedDirectory(rootKey, out string? finalRoot, out string? reason))
        {
            Debug.WriteLine($"[{Id}] Skipping scan root '{rootKey}': {reason}");
            yield break;
        }
        _verifiedRoots[rootKey] = finalRoot!;

        var dirQueue = new Queue<(string Path, int Depth)>();
        dirQueue.Enqueue((rootPath, 0));

        while (dirQueue.Count > 0)
        {
            var (currentDir, depth) = dirQueue.Dequeue();

            string[] files = [];
            try
            {
                files = Directory.GetFiles(currentDir, searchPattern);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                continue;
            }

            foreach (var filePath in files)
            {
                FileInfo? fi = null;
                try
                {
                    fi = new FileInfo(filePath);
                }
                catch
                {
                    continue;
                }

                if (fi != null && fi.Exists)
                {
                    yield return fi;
                }
            }

            if (depth < maxDepth)
            {
                string[] subDirs = [];
                try
                {
                    subDirs = Directory.GetDirectories(currentDir);
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    continue;
                }

                foreach (var sub in subDirs)
                {
                    try
                    {
                        var di = new DirectoryInfo(sub);
                        if ((di.Attributes & FileAttributes.ReparsePoint) != 0)
                            continue;

                        dirQueue.Enqueue((sub, depth + 1));
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
        }
    }

    protected (bool Success, string? SkipReason) SafeDeleteFile(string fullPath, bool dryRun)
    {
        try
        {
            var fi = new FileInfo(fullPath);
            if (!fi.Exists)
                return (true, null);

            if (dryRun)
            {
                return (true, null);
            }

            // Only delete files found below a root that was verified during the scan, and only if the
            // file still really lives there. The delete goes through the handle that was verified, so
            // a folder swapped for a junction after the scan cannot redirect it.
            string? expectedFinalPath = GetExpectedFinalPath(fullPath);
            if (expectedFinalPath == null)
            {
                return (false, "Skipped: file is not inside a folder that was verified during the scan.");
            }

            return SafeFileOps.DeleteFileIfAt(fullPath, expectedFinalPath);
        }
        catch (Exception ex)
        {
            return (false, $"Suppressed: {ex.Message}");
        }
    }

    private string? GetExpectedFinalPath(string fullPath)
    {
        string? bestRoot = null;
        foreach (var root in _verifiedRoots.Keys)
        {
            string prefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
            if (fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                (bestRoot == null || root.Length > bestRoot.Length))
            {
                bestRoot = root;
            }
        }

        if (bestRoot == null || !_verifiedRoots.TryGetValue(bestRoot, out var finalRoot))
            return null;

        string relative = fullPath[bestRoot.Length..].TrimStart(Path.DirectorySeparatorChar);
        return finalRoot + Path.DirectorySeparatorChar + relative;
    }

    protected void SafeDeleteEmptyDirectories(string rootPath)
    {
        try
        {
            if (!Directory.Exists(rootPath)) return;

            // Only below roots verified during the scan, and only if the root still resolves to the same place.
            string rootKey = Path.TrimEndingDirectorySeparator(rootPath);
            if (!_verifiedRoots.TryGetValue(rootKey, out var verifiedFinalRoot) ||
                !SafeFileOps.TryResolveTrustedDirectory(rootKey, out var currentFinalRoot, out _) ||
                !string.Equals(verifiedFinalRoot, currentFinalRoot, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Collect subdirectories without following junctions or symbolic links
            // (SearchOption.AllDirectories would walk into them).
            var subDirectories = new List<string>();
            var pending = new Stack<string>();
            pending.Push(rootPath);
            while (pending.Count > 0)
            {
                string current = pending.Pop();
                string[] children;
                try
                {
                    children = Directory.GetDirectories(current);
                }
                catch
                {
                    continue;
                }

                foreach (var child in children)
                {
                    if (SafeFileOps.IsReparsePoint(child))
                        continue;
                    subDirectories.Add(child);
                    pending.Push(child);
                }
            }

            foreach (var subDir in subDirectories.OrderByDescending(d => d.Length))
            {
                try
                {
                    if (Directory.Exists(subDir) && !Directory.EnumerateFileSystemEntries(subDir).Any())
                    {
                        Directory.Delete(subDir, false);
                    }
                }
                catch
                {
                }
            }
        }
        catch
        {
        }
    }
}
