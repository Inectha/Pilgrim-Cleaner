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
using CommunityToolkit.Mvvm.ComponentModel;
using Wpf.Ui.Controls;

namespace LuminaCleaner.Models;

public partial class PrivacyTraceItemModel : ObservableObject
{
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public long SizeBytes { get; init; }
    public DateTime? LastAccessed { get; init; }

    public string FormattedSize => CleanItemModel.FormatBytes(SizeBytes);
    public string FormattedTime => LastAccessed.HasValue ? LastAccessed.Value.ToLocalTime().ToString("g") : "Recent";

    [ObservableProperty]
    private bool _isSelected = true;
}

public partial class PrivacyTargetModel : ObservableObject
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public required SymbolRegular IconSymbol { get; init; }

    [ObservableProperty]
    private int _itemCount;

    [ObservableProperty]
    private long _sizeBytes;

    [ObservableProperty]
    private bool _isSelected = true;

    [ObservableProperty]
    private bool _isCleaned;

    [ObservableProperty]
    private bool _isExpanded;

    public ObservableCollection<PrivacyTraceItemModel> DiscoveredTraces { get; } = [];

    public string FormattedSize => FormatBytes(SizeBytes);

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
