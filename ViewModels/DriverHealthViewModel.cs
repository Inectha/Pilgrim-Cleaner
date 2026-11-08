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

public partial class DriverHealthViewModel : ObservableObject
{
    private readonly IDriverHealthService _driverHealthService;
    private readonly List<DriverItemModel> _allDrivers = [];
    private CancellationTokenSource? _cts;

    public ObservableCollection<DriverItemModel> FilteredDrivers { get; } = [];
    public ObservableCollection<DriverUpdateModel> AvailableUpdates { get; } = [];

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedCategoryFilter = "All"; // "All", "Graphics", "Audio", "Network", "Storage", "System"

    [ObservableProperty]
    private bool _isScanningDrivers;

    [ObservableProperty]
    private bool _isCheckingUpdates;

    [ObservableProperty]
    private bool _isInstallingAnyUpdate;

    [ObservableProperty]
    private string _statusText = "Ready to audit hardware driver health.";

    [ObservableProperty]
    private int _totalDriversCount;

    [ObservableProperty]
    private int _whqlCertifiedCount;

    [ObservableProperty]
    private double _whqlComplianceRate = 100.0;

    [ObservableProperty]
    private int _upToDateCount;

    [ObservableProperty]
    private int _outdatedCount;

    [ObservableProperty]
    private int _graphicsCount;

    [ObservableProperty]
    private int _audioCount;

    [ObservableProperty]
    private int _networkCount;

    [ObservableProperty]
    private int _storageCount;

    [ObservableProperty]
    private int _systemCount;

    [ObservableProperty]
    private bool _hasScannedOnce;

    [ObservableProperty]
    private bool _hasCheckedUpdatesOnce;

    [ObservableProperty]
    private bool _hasAvailableUpdates;

    [ObservableProperty]
    private string _updateSummaryText = "Check native Windows Update for certified driver packages.";

    public DriverHealthViewModel() : this(new DriverHealthService())
    {
    }

    public DriverHealthViewModel(IDriverHealthService driverHealthService)
    {
        _driverHealthService = driverHealthService;
    }

    partial void OnSearchQueryChanged(string value) => ApplyFilter();
    partial void OnSelectedCategoryFilterChanged(string value) => ApplyFilter();

    [RelayCommand]
    public async Task ScanDriversAsync()
    {
        if (IsScanningDrivers) return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        try
        {
            IsScanningDrivers = true;
            StatusText = "Auditing hardware devices and digital signatures via WMI...";

            var results = await _driverHealthService.GetInstalledDriversAsync(_cts.Token);

            _allDrivers.Clear();
            _allDrivers.AddRange(results);

            TotalDriversCount = _allDrivers.Count;
            WhqlCertifiedCount = _allDrivers.Count(d => d.IsWhqlCertified);
            WhqlComplianceRate = TotalDriversCount > 0
                ? Math.Round((double)WhqlCertifiedCount / TotalDriversCount * 100.0, 1)
                : 100.0;

            GraphicsCount = _allDrivers.Count(d => d.Category == DriverCategory.Graphics);
            AudioCount = _allDrivers.Count(d => d.Category == DriverCategory.Audio);
            NetworkCount = _allDrivers.Count(d => d.Category == DriverCategory.Network);
            StorageCount = _allDrivers.Count(d => d.Category == DriverCategory.Storage);
            SystemCount = _allDrivers.Count(d => d.Category == DriverCategory.System);

            // Correlate with any already checked updates
            if (AvailableUpdates.Count > 0)
            {
                _driverHealthService.CorrelateDriversWithUpdates(_allDrivers, AvailableUpdates);
            }

            UpToDateCount = _allDrivers.Count(d => d.IsUpToDate);
            OutdatedCount = _allDrivers.Count(d => d.HasPendingUpdate);

            ApplyFilter();

            HasScannedOnce = true;
            StatusText = $"Audit complete: {TotalDriversCount} drivers inspected ({WhqlComplianceRate}% WHQL compliance).";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Driver audit cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Driver audit error: {ex.Message}";
        }
        finally
        {
            IsScanningDrivers = false;
        }
    }

    [RelayCommand]
    public async Task CheckWhqlUpdatesAsync()
    {
        if (IsCheckingUpdates) return;

        try
        {
            IsCheckingUpdates = true;
            StatusText = "Connecting exclusively to native Windows Update Session (WUApiLib)...";

            var updates = await _driverHealthService.CheckWhqlUpdatesAsync();

            AvailableUpdates.Clear();
            foreach (var u in updates)
            {
                AvailableUpdates.Add(u);
            }

            HasAvailableUpdates = AvailableUpdates.Count > 0;
            HasCheckedUpdatesOnce = true;

            // Correlate drivers with updates to flag outdated vs up-to-date
            if (_allDrivers.Count > 0)
            {
                _driverHealthService.CorrelateDriversWithUpdates(_allDrivers, AvailableUpdates);
                UpToDateCount = _allDrivers.Count(d => d.IsUpToDate);
                OutdatedCount = _allDrivers.Count(d => d.HasPendingUpdate);
                ApplyFilter();
            }

            if (AvailableUpdates.Count > 0)
            {
                UpdateSummaryText = $"{AvailableUpdates.Count} official WHQL driver updates ready to install via native Windows Update.";
                StatusText = $"{AvailableUpdates.Count} certified driver update packages discovered ({OutdatedCount} installed hardware device(s) affected).";
            }
            else
            {
                UpdateSummaryText = "All hardware drivers match current Microsoft WHQL catalog releases.";
                StatusText = "Zero outdated drivers. All connected hardware drivers are up to date.";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Windows Update query error: {ex.Message}";
        }
        finally
        {
            IsCheckingUpdates = false;
        }
    }

    [RelayCommand]
    public async Task InstallUpdateAsync(DriverUpdateModel? update)
    {
        if (update == null || update.IsInstalling) return;

        try
        {
            IsInstallingAnyUpdate = true;
            StatusText = $"Initiating installation for {update.DriverModel}...";

            var (success, message) = await _driverHealthService.InstallDriverUpdateAsync(update);

            if (success)
            {
                StatusText = $"Successfully installed WHQL package for {update.DriverModel}.";

                // Update any matching driver row in inventory
                foreach (var d in _allDrivers.Where(d => d.MatchingUpdate == update))
                {
                    d.HasPendingUpdate = false;
                    d.IsUpToDate = true;
                    d.Status = "Up to Date (WHQL Installed)";
                }

                UpToDateCount = _allDrivers.Count(d => d.IsUpToDate);
                OutdatedCount = _allDrivers.Count(d => d.HasPendingUpdate);
                ApplyFilter();
            }
            else
            {
                StatusText = $"Driver installation failed: {message}";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Installation error: {ex.Message}";
        }
        finally
        {
            IsInstallingAnyUpdate = false;
        }
    }

    [RelayCommand]
    public async Task InstallDriverUpdateFromDeviceAsync(DriverItemModel? driver)
    {
        if (driver?.MatchingUpdate == null) return;
        await InstallUpdateAsync(driver.MatchingUpdate);
    }

    [RelayCommand]
    public async Task InstallAllUpdatesAsync()
    {
        if (AvailableUpdates.Count == 0 || IsInstallingAnyUpdate) return;

        try
        {
            IsInstallingAnyUpdate = true;
            StatusText = $"Installing all {AvailableUpdates.Count(u => !u.IsInstalled)} certified WHQL driver packages...";

            var (success, message) = await _driverHealthService.InstallAllDriverUpdatesAsync(AvailableUpdates);

            StatusText = message;

            if (success)
            {
                foreach (var d in _allDrivers.Where(d => d.HasPendingUpdate))
                {
                    d.HasPendingUpdate = false;
                    d.IsUpToDate = true;
                    d.Status = "Up to Date (WHQL Installed)";
                }

                UpToDateCount = _allDrivers.Count(d => d.IsUpToDate);
                OutdatedCount = _allDrivers.Count(d => d.HasPendingUpdate);
                ApplyFilter();
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Batch installation error: {ex.Message}";
        }
        finally
        {
            IsInstallingAnyUpdate = false;
        }
    }

    [RelayCommand]
    public void SelectCategory(string category)
    {
        SelectedCategoryFilter = category;
    }

    private void ApplyFilter()
    {
        IEnumerable<DriverItemModel> query = _allDrivers;

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            string q = SearchQuery.Trim();
            query = query.Where(d =>
                d.DeviceName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                d.Signer.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                d.DeviceClass.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                d.Status.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        if (SelectedCategoryFilter != "All")
        {
            if (Enum.TryParse<DriverCategory>(SelectedCategoryFilter, true, out var cat))
            {
                query = query.Where(d => d.Category == cat);
            }
        }

        FilteredDrivers.Clear();
        foreach (var driver in query)
        {
            FilteredDrivers.Add(driver);
        }
    }
}
