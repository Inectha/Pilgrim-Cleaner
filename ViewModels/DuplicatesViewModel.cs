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
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LuminaCleaner.Models;
using LuminaCleaner.Services;

namespace LuminaCleaner.ViewModels;

public partial class DuplicatesViewModel : ObservableObject
{
    private readonly IDuplicatesService _duplicatesService;
    private CancellationTokenSource? _cts;

    public ObservableCollection<DuplicateGroupModel> DuplicateGroups { get; } = [];

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private int _totalDuplicateGroups;

    [ObservableProperty]
    private int _totalRedundantFiles;

    [ObservableProperty]
    private string _wastedVolumeText = "0.00 B";

    [ObservableProperty]
    private int _selectedCopiesCount;

    [ObservableProperty]
    private string _selectedVolumeText = "0.00 B";

    [ObservableProperty]
    private string _statusMessage = "Ready to audit duplicate and redundant files.";

    public DuplicatesViewModel(IDuplicatesService? duplicatesService = null)
    {
        _duplicatesService = duplicatesService ?? new DuplicatesService();
    }

    [RelayCommand]
    public async Task StartScanAsync()
    {
        if (IsScanning) return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        IsScanning = true;
        StatusMessage = "Analyzing folders for byte-identical duplicate files...";

        DuplicateGroups.Clear();

        var scanFolders = new List<string>();
        AddIfDirectoryExists(scanFolders, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        AddIfDirectoryExists(scanFolders, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        AddIfDirectoryExists(scanFolders, Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));

        var progress = new Progress<int>(scanned =>
        {
            StatusMessage = $"Scanned {scanned:N0} candidate files...";
        });

        try
        {
            var groups = await _duplicatesService.FindDuplicatesAsync(scanFolders, progress, _cts.Token);
            foreach (var g in groups)
            {
                DuplicateGroups.Add(g);
            }

            RecalculateMetrics();
            StatusMessage = $"Audit complete: {TotalDuplicateGroups} duplicate groups found ({WastedVolumeText} recoverable).";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Duplicate audit canceled.";
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    public void CancelScan()
    {
        _cts?.Cancel();
        IsScanning = false;
    }

    [RelayCommand]
    public void DeleteSelectedCopies()
    {
        int deleted = 0;
        long bytesRecovered = 0;
        int notRecycled = 0;

        foreach (var group in DuplicateGroups.ToList())
        {
            var original = group.Copies.FirstOrDefault(c => c.IsOriginal);
            if (original == null) continue; // never delete from a group without a kept copy

            var copiesToDelete = group.Copies.Where(c => c.IsSelected && !c.IsOriginal).ToList();
            foreach (var copy in copiesToDelete)
            {
                if (!_duplicatesService.RecycleDuplicate(copy.FullPath, original.FullPath, interactive: true))
                {
                    notRecycled++;
                    continue;
                }

                deleted++;
                bytesRecovered += copy.SizeBytes;
                group.Copies.Remove(copy);
            }

            if (group.Copies.Count <= 1)
            {
                DuplicateGroups.Remove(group);
            }
        }

        RecalculateMetrics();
        StatusMessage = notRecycled == 0
            ? $"Safely moved {deleted} duplicate files ({FormatBytes(bytesRecovered)}) to the Recycle Bin."
            : $"Moved {deleted} duplicate files ({FormatBytes(bytesRecovered)}) to the Recycle Bin. " +
              $"{notRecycled} were kept: they are on a drive without a Recycle Bin (network/USB), or the original changed.";
    }

    [RelayCommand]
    public void SelectAllRedundant()
    {
        foreach (var g in DuplicateGroups)
        {
            foreach (var c in g.Copies)
            {
                c.IsSelected = !c.IsOriginal;
            }
        }
        UpdateSelectionMetrics();
    }

    [RelayCommand]
    public void DeselectAll()
    {
        foreach (var g in DuplicateGroups)
        {
            foreach (var c in g.Copies)
            {
                c.IsSelected = false;
            }
        }
        UpdateSelectionMetrics();
    }

    public void RecalculateMetrics()
    {
        TotalDuplicateGroups = DuplicateGroups.Count;
        TotalRedundantFiles = DuplicateGroups.Sum(g => g.RedundantCopies);
        long totalWastedBytes = DuplicateGroups.Sum(g => g.WastedBytes);
        WastedVolumeText = FormatBytes(totalWastedBytes);

        UpdateSelectionMetrics();
    }

    public void UpdateSelectionMetrics()
    {
        var selected = DuplicateGroups.SelectMany(g => g.Copies).Where(c => c.IsSelected).ToList();
        SelectedCopiesCount = selected.Count;
        long bytes = selected.Sum(c => c.SizeBytes);
        SelectedVolumeText = FormatBytes(bytes);
    }

    private static void AddIfDirectoryExists(List<string> list, params string[] paths)
    {
        string combined = Path.Combine(paths);
        if (Directory.Exists(combined) && !list.Contains(combined))
        {
            list.Add(combined);
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 0) return "0.00 B";
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024m) >= 1 && counter < suffixes.Length - 1)
        {
            number /= 1024m;
            counter++;
        }
        return $"{number:n2} {suffixes[counter]}";
    }
}
