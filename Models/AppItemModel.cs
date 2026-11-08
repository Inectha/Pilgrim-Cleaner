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

namespace LuminaCleaner.Models;

public partial class AppItemModel : ObservableObject
{
    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _displayVersion = string.Empty;

    [ObservableProperty]
    private string _publisher = "Unknown Publisher";

    [ObservableProperty]
    private string _installDate = string.Empty;

    [ObservableProperty]
    private long _estimatedSizeBytes;

    [ObservableProperty]
    private string _formattedSize = "Unknown Size";

    [ObservableProperty]
    private string? _displayIcon;

    [ObservableProperty]
    private string _uninstallString = string.Empty;

    [ObservableProperty]
    private string _quietUninstallString = string.Empty;

    [ObservableProperty]
    private string? _installLocation;

    [ObservableProperty]
    private bool _isPackagedApp;

    [ObservableProperty]
    private string? _packageFullName;

    [ObservableProperty]
    private bool _isSystemComponent;

    [ObservableProperty]
    private bool _isSecurityUpdate;

    [ObservableProperty]
    private string _category = "Win32 Desktop";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _hasLeftovers;

    [ObservableProperty]
    private string _status = "Installed";

    public ObservableCollection<LeftoverFolderModel> Leftovers { get; } = [];

    public bool HasQuietUninstall => !string.IsNullOrWhiteSpace(QuietUninstallString) ||
                                     UninstallString.Contains("msiexec", StringComparison.OrdinalIgnoreCase) ||
                                     IsPackagedApp;
}
