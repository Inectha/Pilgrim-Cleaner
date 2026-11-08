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
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LuminaCleaner.Models;
using LuminaCleaner.Services;

namespace LuminaCleaner.ViewModels;

public partial class SystemTelemetryViewModel : ObservableObject
{
    private readonly ISystemTelemetryService _telemetryService;
    private readonly DispatcherTimer _timer;

    [ObservableProperty]
    private HardwareTelemetryModel _telemetry = new();

    [ObservableProperty]
    private bool _isLiveMonitoring = true;

    [ObservableProperty]
    private string _statusMessage = "Live hardware telemetry active.";

    public SystemTelemetryViewModel(ISystemTelemetryService? telemetryService = null)
    {
        _telemetryService = telemetryService ?? new SystemTelemetryService();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2.0)
        };
        _timer.Tick += async (s, e) => await SampleAsync();
        _timer.Start();

        // Initial sample
        _ = SampleAsync();
    }

    [RelayCommand]
    public async Task RefreshTelemetryAsync()
    {
        await SampleAsync();
    }

    [RelayCommand]
    public void ToggleMonitoring()
    {
        IsLiveMonitoring = !IsLiveMonitoring;
        if (IsLiveMonitoring)
        {
            _timer.Start();
            StatusMessage = "Live monitoring resumed.";
        }
        else
        {
            _timer.Stop();
            StatusMessage = "Live monitoring paused.";
        }
    }

    private async Task SampleAsync()
    {
        try
        {
            var result = await _telemetryService.SampleTelemetryAsync();
            Telemetry = result;
        }
        catch { }
    }
}
