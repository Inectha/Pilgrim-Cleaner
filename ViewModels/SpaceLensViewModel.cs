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

public partial class SpaceLensViewModel : ObservableObject
{
    private readonly ISpaceLensService _spaceLensService;
    private readonly List<SpaceLensItemModel> _allDiscovered = [];
    private CancellationTokenSource? _cts;

    public ObservableCollection<SpaceLensItemModel> FilteredItems { get; } = [];

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedSizeFilter = "All Sizes";

    [ObservableProperty]
    private string _selectedAgeFilter = "All Ages";

    [ObservableProperty]
    private string _selectedCategoryFilter = "All Types";

    [ObservableProperty]
    private int _totalFilesFound;

    [ObservableProperty]
    private string _totalVolumeText = "0.00 B";

    [ObservableProperty]
    private long _totalVolumeBytes;

    [ObservableProperty]
    private int _selectedFilesCount;

    [ObservableProperty]
    private string _selectedVolumeText = "0.00 B";

    [ObservableProperty]
    private string _statusMessage = "Ready to scan for large and ancient files.";

    public SpaceLensViewModel(ISpaceLensService? spaceLensService = null)
    {
        _spaceLensService = spaceLensService ?? new SpaceLensService();
    }

    [RelayCommand]
    public async Task StartScanAsync()
    {
        if (IsScanning) return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        IsScanning = true;
        StatusMessage = "Analyzing user folders (Downloads, Documents, Videos, Desktop)...";

        _allDiscovered.Clear();
        FilteredItems.Clear();

        var targetFolders = new List<string>();
        AddIfDirectoryExists(targetFolders, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        AddIfDirectoryExists(targetFolders, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        AddIfDirectoryExists(targetFolders, Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));
        AddIfDirectoryExists(targetFolders, Environment.GetFolderPath(Environment.SpecialFolder.Desktop));

        var progress = new Progress<int>(count =>
        {
            StatusMessage = $"Discovered {count:N0} candidate files...";
        });

        try
        {
            // Default 50 MB threshold for large/old files
            var items = await _spaceLensService.ScanLibrariesAsync(targetFolders, 50L * 1024 * 1024, progress, _cts.Token);
            _allDiscovered.AddRange(items.OrderByDescending(i => i.SizeBytes));

            ApplyFilters();
            StatusMessage = $"Audit complete: {_allDiscovered.Count:N0} large files cataloged ({TotalVolumeText}).";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Space Lens audit canceled.";
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
    public void RevealInExplorer(SpaceLensItemModel? item)
    {
        if (item != null)
        {
            _spaceLensService.RevealInExplorer(item.FullPath);
        }
    }

    [RelayCommand]
    public void DeleteSelectedToRecycleBin()
    {
        var targets = _allDiscovered.Where(i => i.IsSelected).ToList();
        if (targets.Count == 0) return;

        int deleted = 0;
        long bytesDeleted = 0;

        foreach (var item in targets)
        {
            if (_spaceLensService.DeleteToRecycleBin(item.FullPath))
            {
                deleted++;
                bytesDeleted += item.SizeBytes;
                _allDiscovered.Remove(item);
            }
        }

        ApplyFilters();
        StatusMessage = $"Moved {deleted} files ({FormatBytes(bytesDeleted)}) to the Windows Recycle Bin.";
    }

    [RelayCommand]
    public void SelectAllFiltered()
    {
        foreach (var item in FilteredItems) item.IsSelected = true;
        UpdateSelectionMetrics();
    }

    [RelayCommand]
    public void DeselectAll()
    {
        foreach (var item in _allDiscovered) item.IsSelected = false;
        UpdateSelectionMetrics();
    }

    partial void OnSearchQueryChanged(string value) => ApplyFilters();
    partial void OnSelectedSizeFilterChanged(string value) => ApplyFilters();
    partial void OnSelectedAgeFilterChanged(string value) => ApplyFilters();
    partial void OnSelectedCategoryFilterChanged(string value) => ApplyFilters();

    private void ApplyFilters()
    {
        var query = _allDiscovered.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            query = query.Where(i => i.FileName.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase));
        }

        if (SelectedSizeFilter != "All Sizes")
        {
            query = query.Where(i => i.SizeTier.Equals(SelectedSizeFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (SelectedAgeFilter != "All Ages")
        {
            query = query.Where(i => i.AgeCategory.Equals(SelectedAgeFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (SelectedCategoryFilter != "All Types")
        {
            query = query.Where(i => i.FileCategory.Equals(SelectedCategoryFilter, StringComparison.OrdinalIgnoreCase));
        }

        FilteredItems.Clear();
        foreach (var item in query)
        {
            FilteredItems.Add(item);
        }

        TotalFilesFound = _allDiscovered.Count;
        TotalVolumeBytes = _allDiscovered.Sum(i => i.SizeBytes);
        TotalVolumeText = FormatBytes(TotalVolumeBytes);

        UpdateSelectionMetrics();
    }

    public void UpdateSelectionMetrics()
    {
        var selected = _allDiscovered.Where(i => i.IsSelected).ToList();
        SelectedFilesCount = selected.Count;
        long bytes = selected.Sum(i => i.SizeBytes);
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
