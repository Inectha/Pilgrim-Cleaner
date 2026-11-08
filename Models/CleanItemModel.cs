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
using CommunityToolkit.Mvvm.ComponentModel;

namespace LuminaCleaner.Models;

public partial class CleanItemModel : ObservableObject
{
    public required string FullPath { get; init; }
    public required string FileName { get; init; }
    public required long SizeBytes { get; init; }
    public required DateTime LastModified { get; init; }
    public required CleanerCategory Category { get; init; }
    public required string ModuleName { get; init; }
    public bool IsFolder { get; init; }

    [ObservableProperty]
    private bool _isSelected = true;

    [ObservableProperty]
    private CleanItemStatus _status = CleanItemStatus.Scanned;

    [ObservableProperty]
    private string? _skipReason;

    public string FormattedSize => FormatBytes(SizeBytes);

    public string FormattedAge
    {
        get
        {
            var diff = DateTime.Now - LastModified;
            if (diff.TotalHours < 1)
                return $"{Math.Max(1, (int)diff.TotalMinutes)}m ago";
            if (diff.TotalDays < 1)
                return $"{(int)diff.TotalHours}h ago";
            if (diff.TotalDays < 30)
                return $"{(int)diff.TotalDays}d ago";
            return LastModified.ToString("yyyy-MM-dd");
        }
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
            if (counter >= suffixes.Length - 1) break;
        }
        return $"{number:n2} {suffixes[counter]}";
    }
}
