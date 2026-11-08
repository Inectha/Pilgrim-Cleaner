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

public partial class HelpArticleModel : ObservableObject
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Category { get; init; }
    public required string Summary { get; init; }
    public required string Content { get; init; }
    public required SymbolRegular IconSymbol { get; init; }
    public required string[] Keywords { get; init; }
    public string? NavigationTargetView { get; init; }

    [ObservableProperty]
    private bool _isSelected;
}
