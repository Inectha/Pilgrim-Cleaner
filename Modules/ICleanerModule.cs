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
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using LuminaCleaner.Models;
using Wpf.Ui.Controls;

namespace LuminaCleaner.Modules;

public interface ICleanerModule : INotifyPropertyChanged
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    string SecurityNotice { get; }
    CleanerCategory Category { get; }
    SymbolRegular IconSymbol { get; }

    bool IsEnabled { get; set; }
    bool IsScanning { get; set; }
    bool RequiresElevation { get; }
    bool IsElevationAvailable { get; }

    long TotalSizeBytes { get; }
    string FormattedTotalSize { get; }
    int ItemCount { get; }
    int SelectedCount { get; }
    long SelectedSizeBytes { get; }
    string FormattedSelectedSize { get; }

    long SkippedSizeBytes { get; }
    string FormattedSkippedSize { get; }
    int SkippedCount { get; }
    string SkippedReasonSummary { get; }

    ObservableCollection<CleanItemModel> DiscoveredItems { get; }

    Task<ModuleScanResult> ScanAsync(EngineOptions options, IProgress<ScanProgressReport> progress, CancellationToken ct);
    Task<ModuleCleanResult> CleanAsync(EngineOptions options, IProgress<CleanProgressReport> progress, CancellationToken ct);

    void ClearResults();
    void SelectAll(bool select);
    void UpdateSelectionMetrics();
}
