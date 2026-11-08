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
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LuminaCleaner.Models;

public partial class SpaceLensItemModel : ObservableObject
{
    public required string FullPath { get; init; }
    public required string FileName { get; init; }
    public required string DirectoryName { get; init; }
    public required long SizeBytes { get; init; }
    public required DateTime LastModified { get; init; }

    [ObservableProperty]
    private bool _isSelected;

    public int AgeDays => (int)Math.Max(0, (DateTime.Now - LastModified).TotalDays);

    public string Extension => Path.GetExtension(FileName).ToLowerInvariant();

    public string SizeTier => SizeBytes switch
    {
        >= 2L * 1024 * 1024 * 1024 => "Huge (> 2 GB)",
        >= 500L * 1024 * 1024 => "Very Large (500 MB - 2 GB)",
        >= 100L * 1024 * 1024 => "Large (100 MB - 500 MB)",
        _ => "Standard (< 100 MB)"
    };

    public string AgeCategory => AgeDays switch
    {
        >= 365 => "Ancient (> 1 Year)",
        >= 180 => "Old (6 - 12 Months)",
        >= 30 => "Recent (1 - 6 Months)",
        _ => "Active (< 30 Days)"
    };

    public string FileCategory => Extension switch
    {
        ".mp4" or ".mkv" or ".avi" or ".mov" or ".wmv" or ".flv" => "Media & Video",
        ".zip" or ".rar" or ".7z" or ".tar" or ".gz" or ".bz2" => "Archives",
        ".iso" or ".vhd" or ".vhdx" or ".img" or ".bin" => "Virtual Disk Images",
        ".exe" or ".msi" or ".appx" or ".msix" => "Installers",
        _ => "Other Files"
    };

    public Wpf.Ui.Controls.SymbolRegular CategoryIcon => FileCategory switch
    {
        "Media & Video" => Wpf.Ui.Controls.SymbolRegular.Video24,
        "Archives" => Wpf.Ui.Controls.SymbolRegular.FolderZip24,
        "Virtual Disk Images" => Wpf.Ui.Controls.SymbolRegular.HardDrive24,
        "Installers" => Wpf.Ui.Controls.SymbolRegular.Box24,
        _ => Wpf.Ui.Controls.SymbolRegular.Document24
    };

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
