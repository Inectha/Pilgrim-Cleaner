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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LuminaCleaner.Models;
using LuminaCleaner.Services;

namespace LuminaCleaner.ViewModels;

public partial class AppManagerViewModel : ObservableObject
{
    private readonly IAppManagerService _appManagerService;
    private readonly List<AppItemModel> _allApplications = [];
    private CancellationTokenSource? _cts;

    public ObservableCollection<AppItemModel> FilteredApplications { get; } = [];
    public ObservableCollection<LeftoverFolderModel> CurrentLeftovers { get; } = [];

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedCategoryFilter = "All"; // "All", "Win32 Desktop", "Windows Store App"

    [ObservableProperty]
    private string _selectedSortOption = "Name"; // "Name", "Size", "Date"

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusText = "Ready to audit installed software.";

    [ObservableProperty]
    private int _totalAppsCount;

    [ObservableProperty]
    private string _totalSizeText = "0.00 B";

    [ObservableProperty]
    private int _packagedAppsCount;

    [ObservableProperty]
    private AppItemModel? _selectedApp;

    [ObservableProperty]
    private bool _isLeftoverDrawerOpen;

    [ObservableProperty]
    private AppItemModel? _currentLeftoverApp;

    [ObservableProperty]
    private string _formattedLeftoverSize = "0.00 B";

    [ObservableProperty]
    private bool _hasLoadedOnce;

    public AppManagerViewModel() : this(new AppManagerService())
    {
    }

    public AppManagerViewModel(IAppManagerService appManagerService)
    {
        _appManagerService = appManagerService;
    }

    partial void OnSearchQueryChanged(string value) => ApplyFilterAndSort();
    partial void OnSelectedCategoryFilterChanged(string value) => ApplyFilterAndSort();
    partial void OnSelectedSortOptionChanged(string value) => ApplyFilterAndSort();

    [RelayCommand]
    public async Task RefreshApplicationsAsync()
    {
        if (IsLoading) return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        try
        {
            IsLoading = true;
            StatusText = "Scanning 64-bit/32-bit registry and Windows Store packages...";

            var result = await _appManagerService.GetInstalledApplicationsAsync(_cts.Token);

            _allApplications.Clear();
            _allApplications.AddRange(result);

            TotalAppsCount = _allApplications.Count;
            PackagedAppsCount = _allApplications.Count(a => a.IsPackagedApp);

            long totalBytes = _allApplications.Sum(a => a.EstimatedSizeBytes);
            TotalSizeText = FormatBytes(totalBytes);

            ApplyFilterAndSort();

            HasLoadedOnce = true;
            StatusText = $"Audit complete: {_allApplications.Count} applications cataloged ({TotalSizeText}).";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Software scan cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Scan error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task UninstallApplicationAsync(AppItemModel? app)
    {
        if (app == null) return;
        await ExecuteUninstallInternal(app, quiet: false);
    }

    [RelayCommand]
    public async Task QuietUninstallApplicationAsync(AppItemModel? app)
    {
        if (app == null) return;
        await ExecuteUninstallInternal(app, quiet: true);
    }

    private async Task ExecuteUninstallInternal(AppItemModel app, bool quiet)
    {
        StatusText = $"Launching uninstaller for {app.DisplayName}...";

        bool success = await _appManagerService.UninstallApplicationAsync(app, quiet);
        if (success)
        {
            StatusText = $"Uninstalled {app.DisplayName}. Scanning for leftover residue...";

            // Automatically scan for leftover residue
            await ScanLeftoversForAppAsync(app);
        }
        else
        {
            StatusText = $"Uninstallation cancelled or failed for {app.DisplayName}.";
        }
    }

    [RelayCommand]
    public async Task ScanLeftoversForAppAsync(AppItemModel? app)
    {
        if (app == null) return;

        CurrentLeftovers.Clear();
        CurrentLeftoverApp = app;

        var leftovers = await _appManagerService.ScanLeftoversAsync(app);
        foreach (var l in leftovers)
        {
            CurrentLeftovers.Add(l);
        }

        if (CurrentLeftovers.Count > 0)
        {
            long totalLeftoverBytes = CurrentLeftovers.Sum(l => l.SizeBytes);
            FormattedLeftoverSize = FormatBytes(totalLeftoverBytes);
            IsLeftoverDrawerOpen = true;
            StatusText = $"Found {CurrentLeftovers.Count} leftover folders ({FormattedLeftoverSize}) for {app.DisplayName}.";
        }
        else
        {
            IsLeftoverDrawerOpen = false;
            StatusText = $"Clean uninstall: Zero leftover residue detected for {app.DisplayName}.";

            // Remove uninstalled app from view
            _allApplications.Remove(app);
            ApplyFilterAndSort();
        }
    }

    [RelayCommand]
    public async Task PurgeSelectedLeftoversAsync()
    {
        if (CurrentLeftovers.Count == 0) return;

        StatusText = "Purging leftover folders...";
        bool ok = await _appManagerService.DeleteLeftoversAsync(CurrentLeftovers);

        if (ok)
        {
            StatusText = "All selected leftover residue successfully purged.";
        }
        else
        {
            StatusText = "Leftover purge complete. Some locked files were preserved.";
        }

        IsLeftoverDrawerOpen = false;
        if (CurrentLeftoverApp != null)
        {
            _allApplications.Remove(CurrentLeftoverApp);
            CurrentLeftoverApp = null;
        }

        ApplyFilterAndSort();
    }

    [RelayCommand]
    public void DismissLeftovers()
    {
        IsLeftoverDrawerOpen = false;
        if (CurrentLeftoverApp != null)
        {
            _allApplications.Remove(CurrentLeftoverApp);
            CurrentLeftoverApp = null;
        }
        ApplyFilterAndSort();
        StatusText = "Leftovers dismissed.";
    }

    private void ApplyFilterAndSort()
    {
        IEnumerable<AppItemModel> query = _allApplications;

        // 1. Search Query Filter
        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            string q = SearchQuery.Trim();
            query = query.Where(a =>
                a.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                a.Publisher.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        // 2. Category Filter
        if (SelectedCategoryFilter == "Win32 Desktop")
        {
            query = query.Where(a => !a.IsPackagedApp);
        }
        else if (SelectedCategoryFilter == "Windows Store App")
        {
            query = query.Where(a => a.IsPackagedApp);
        }

        // 3. Sorting
        query = SelectedSortOption switch
        {
            "Size" => query.OrderByDescending(a => a.EstimatedSizeBytes).ThenBy(a => a.DisplayName),
            "Date" => query.OrderByDescending(a => a.InstallDate).ThenBy(a => a.DisplayName),
            _ => query.OrderBy(a => a.DisplayName)
        };

        FilteredApplications.Clear();
        foreach (var app in query)
        {
            FilteredApplications.Add(app);
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0.00 B";
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        int index = 0;
        double size = bytes;
        while (size >= 1024 && index < suffixes.Length - 1)
        {
            size /= 1024;
            index++;
        }
        return $"{size:F2} {suffixes[index]}";
    }
}
