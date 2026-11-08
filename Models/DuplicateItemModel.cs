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
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LuminaCleaner.Models;

public partial class DuplicateFileItemModel : ObservableObject
{
    public required string FullPath { get; init; }
    public required string FileName { get; init; }
    public required string DirectoryPath { get; init; }
    public required long SizeBytes { get; init; }
    public required DateTime LastModified { get; init; }

    [ObservableProperty]
    private bool _isOriginal;

    [ObservableProperty]
    private bool _isSelected;

    public string FormattedDate => LastModified.ToString("yyyy-MM-dd HH:mm");
    public string FormattedSize => CleanItemModel.FormatBytes(SizeBytes);
}

public partial class DuplicateGroupModel : ObservableObject
{
    public required string FileHash { get; init; }
    public required long SizeBytes { get; init; }
    public ObservableCollection<DuplicateFileItemModel> Copies { get; } = [];

    public int TotalCopies => Copies.Count;
    public int RedundantCopies => Math.Max(0, Copies.Count - 1);
    public long WastedBytes => RedundantCopies * SizeBytes;

    public string FormattedSize => FormatBytes(SizeBytes);
    public string FormattedWastedBytes => FormatBytes(WastedBytes);
    public string ShortHash => FileHash.Length > 12 ? FileHash[..12] : FileHash;

    public string SampleFileName => Copies.FirstOrDefault()?.FileName ?? "Unknown";

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
