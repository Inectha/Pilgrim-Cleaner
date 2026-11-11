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
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LuminaCleaner.Models;
using LuminaCleaner.Services;

namespace LuminaCleaner.ViewModels;

public partial class PrivacyViewModel : ObservableObject
{
    private readonly IPrivacyService _privacyService;
    private CancellationTokenSource? _cts;

    public ObservableCollection<PrivacyTargetModel> Targets { get; } = [];

    [ObservableProperty]
    private bool _isAuditing;

    [ObservableProperty]
    private bool _isPurging;

    [ObservableProperty]
    private int _totalTraceItems;

    [ObservableProperty]
    private string _totalTraceVolumeText = "0.00 B";

    [ObservableProperty]
    private string _statusMessage = "Ready to audit Windows privacy traces.";

    public PrivacyViewModel(IPrivacyService? privacyService = null)
    {
        _privacyService = privacyService ?? new PrivacyService();
    }

    [RelayCommand]
    public async Task AuditPrivacyAsync()
    {
        if (IsAuditing || IsPurging) return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        IsAuditing = true;
        StatusMessage = "Auditing Windows recent items, taskbar jump lists, and clipboard buffer...";

        Targets.Clear();

        try
        {
            var results = await _privacyService.AuditPrivacyAsync(_cts.Token);
            foreach (var target in results)
            {
                Targets.Add(target);
            }

            RecalculateMetrics();
            StatusMessage = $"Privacy audit complete: {TotalTraceItems} privacy traces detected.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Privacy audit canceled.";
        }
        finally
        {
            IsAuditing = false;
        }
    }

    [RelayCommand]
    public async Task PurgePrivacyAsync()
    {
        if (IsAuditing || IsPurging) return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        IsPurging = true;
        StatusMessage = "Purging selected privacy traces...";

        try
        {
            int purged = await _privacyService.PurgePrivacyTargetsAsync(Targets, _cts.Token);
            foreach (var target in Targets)
            {
                if (target.IsSelected)
                {
                    target.IsCleaned = true;
                    target.ItemCount = 0;
                    target.SizeBytes = 0;
                    target.DiscoveredTraces.Clear();
                }
            }

            RecalculateMetrics();
            StatusMessage = $"Privacy shield active: {purged} traces successfully cleared.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Privacy purge canceled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Privacy purge failed: {ex.Message}";
        }
        finally
        {
            IsPurging = false;
        }
    }

    [RelayCommand]
    public void SelectAll()
    {
        foreach (var t in Targets) t.IsSelected = true;
    }

    [RelayCommand]
    public void DeselectAll()
    {
        foreach (var t in Targets) t.IsSelected = false;
    }

    [RelayCommand]
    public void ToggleExpand(PrivacyTargetModel? target)
    {
        if (target != null)
        {
            target.IsExpanded = !target.IsExpanded;
        }
    }

    private void RecalculateMetrics()
    {
        TotalTraceItems = Targets.Sum(t => t.ItemCount);
        long bytes = Targets.Sum(t => t.SizeBytes);
        TotalTraceVolumeText = FormatBytes(bytes);
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
