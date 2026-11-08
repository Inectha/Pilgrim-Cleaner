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
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LuminaCleaner.Models;
using LuminaCleaner.Services;

namespace LuminaCleaner.ViewModels;

public partial class StartupViewModel : ObservableObject
{
    private readonly IStartupService _startupService;
    private readonly List<StartupItemModel> _allStartupItems = [];
    private CancellationTokenSource? _cts;

    public ObservableCollection<StartupItemModel> FilteredItems { get; } = [];

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedFilter = "All"; // "All", "Enabled", "Disabled", "High Impact"

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _enabledCount;

    [ObservableProperty]
    private int _highImpactCount;

    [ObservableProperty]
    private string _statusMessage = "Ready to audit startup entries.";

    public StartupViewModel(IStartupService? startupService = null)
    {
        _startupService = startupService ?? new StartupService();
    }

    [RelayCommand]
    public async Task RefreshStartupItemsAsync()
    {
        if (IsLoading) return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        IsLoading = true;
        StatusMessage = "Auditing Windows startup entries and Task Scheduler...";

        _allStartupItems.Clear();
        FilteredItems.Clear();

        try
        {
            var items = await _startupService.GetStartupItemsAsync(_cts.Token);
            _allStartupItems.AddRange(items);

            ApplyFilters();
            StatusMessage = $"Cataloged {_allStartupItems.Count} startup programs ({EnabledCount} active).";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Startup audit canceled.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void ToggleItem(StartupItemModel? item)
    {
        if (item == null) return;

        bool targetState = !item.IsEnabled;
        bool success = _startupService.SetStartupItemEnabled(item, targetState);
        if (success)
        {
            item.IsEnabled = targetState;
            StatusMessage = $"{item.Name} is now {(targetState ? "enabled" : "disabled")} for Windows boot.";
            ApplyFilters();
        }
        else
        {
            StatusMessage = $"Could not modify startup entry '{item.Name}'. Administrator privileges may be required.";
        }
    }

    partial void OnSearchQueryChanged(string value) => ApplyFilters();
    partial void OnSelectedFilterChanged(string value) => ApplyFilters();

    private void ApplyFilters()
    {
        var query = _allStartupItems.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            query = query.Where(i => i.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                                     i.Publisher.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                                     i.Command.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase));
        }

        if (SelectedFilter == "Enabled")
        {
            query = query.Where(i => i.IsEnabled);
        }
        else if (SelectedFilter == "Disabled")
        {
            query = query.Where(i => !i.IsEnabled);
        }
        else if (SelectedFilter == "High Impact")
        {
            query = query.Where(i => i.StartupImpact == "High");
        }

        FilteredItems.Clear();
        foreach (var item in query)
        {
            FilteredItems.Add(item);
        }

        TotalCount = _allStartupItems.Count;
        EnabledCount = _allStartupItems.Count(i => i.IsEnabled);
        HighImpactCount = _allStartupItems.Count(i => i.StartupImpact == "High" && i.IsEnabled);
    }
}
