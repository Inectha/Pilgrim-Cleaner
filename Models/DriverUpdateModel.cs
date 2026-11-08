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

public partial class DriverUpdateModel : ObservableObject
{
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _driverModel = string.Empty;

    [ObservableProperty]
    private string _driverProvider = "Microsoft / OEM";

    [ObservableProperty]
    private string _driverVerDate = string.Empty;

    [ObservableProperty]
    private string _driverVersion = string.Empty;

    [ObservableProperty]
    private bool _isWhqlCertified = true;

    [ObservableProperty]
    private string _status = "WHQL Verified";

    [ObservableProperty]
    private string _updateId = string.Empty;

    [ObservableProperty]
    private bool _isInstalling;

    [ObservableProperty]
    private bool _isInstalled;

    [ObservableProperty]
    private string _installStatus = "Ready to Install";

    [ObservableProperty]
    private object? _rawUpdateObject;
}
