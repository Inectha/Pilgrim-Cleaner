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
using Wpf.Ui.Controls;

namespace LuminaCleaner.Models;

public partial class StartupItemModel : ObservableObject
{
    public required string Name { get; init; }
    public required string Command { get; set; }
    public required string Publisher { get; init; }
    public required string Location { get; init; }
    public required string SourceType { get; init; } // "Registry (User)", "Registry (Machine)", "Startup Folder", "Task Scheduler"

    [ObservableProperty]
    private bool _isEnabled;

    public required string StartupImpact { get; init; } // "High", "Medium", "Low", "Not Measured"

    public SymbolRegular SourceIcon => SourceType switch
    {
        "Startup Folder" => SymbolRegular.Folder24,
        "Task Scheduler" => SymbolRegular.Timer24,
        _ => SymbolRegular.WindowConsole20
    };

    public string ImpactBadgeColor => StartupImpact switch
    {
        "High" => "#FF4D4D",
        "Medium" => "#FFB800",
        "Low" => "#00FF9D",
        _ => "#94A3B8"
    };
}
