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

using CommunityToolkit.Mvvm.ComponentModel;

namespace LuminaCleaner.Models;

public partial class DriverItemModel : ObservableObject
{
    [ObservableProperty]
    private string _deviceName = string.Empty;

    [ObservableProperty]
    private string _driverVersion = string.Empty;

    [ObservableProperty]
    private string _driverDate = string.Empty;

    [ObservableProperty]
    private string _signer = "Microsoft Windows";

    [ObservableProperty]
    private bool _isWhqlCertified = true;

    [ObservableProperty]
    private string _deviceClass = "System";

    [ObservableProperty]
    private DriverCategory _category = DriverCategory.System;

    [ObservableProperty]
    private string _deviceID = string.Empty;

    [ObservableProperty]
    private string _status = "Up to Date";

    [ObservableProperty]
    private string _categoryIcon = "DeveloperBoard24";

    [ObservableProperty]
    private bool _isUpToDate = true;

    [ObservableProperty]
    private bool _hasPendingUpdate;

    [ObservableProperty]
    private string _updateVersion = string.Empty;

    [ObservableProperty]
    private string _updateTitle = string.Empty;

    [ObservableProperty]
    private string _updateReleaseDate = string.Empty;

    [ObservableProperty]
    private bool _isInstalling;

    [ObservableProperty]
    private string _installStatus = string.Empty;

    [ObservableProperty]
    private DriverUpdateModel? _matchingUpdate;
}
