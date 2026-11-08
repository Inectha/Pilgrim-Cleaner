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

public partial class DriveTelemetryModel : ObservableObject
{
    public required string DriveLetter { get; init; } // e.g. "C:\"
    public required string VolumeLabel { get; init; } // e.g. "Windows SSD"
    public required string FileSystem { get; init; }  // e.g. "NTFS"
    public required string DriveType { get; init; }   // e.g. "Fixed Storage"
    public long TotalSizeBytes { get; init; }
    public long FreeSizeBytes { get; init; }
    public long UsedSizeBytes { get; init; }
    public double UsagePercent { get; init; }

    public string FormattedTotal => CleanItemModel.FormatBytes(TotalSizeBytes);
    public string FormattedFree => CleanItemModel.FormatBytes(FreeSizeBytes);
    public string FormattedUsed => CleanItemModel.FormatBytes(UsedSizeBytes);

    public string UsageBarColor => UsagePercent > 90.0 ? "#EF4444" : (UsagePercent > 75.0 ? "#F59E0B" : "#00E5FF");
}

public partial class HardwareTelemetryModel : ObservableObject
{
    // CPU
    [ObservableProperty]
    private double _cpuUsagePercent;

    [ObservableProperty]
    private string _cpuName = "Intel / AMD Processor";

    [ObservableProperty]
    private string _cpuCoresDescription = "Active Cores & Threads";

    [ObservableProperty]
    private string _cpuArchitecture = "64-bit Architecture";

    [ObservableProperty]
    private int _processCount;

    [ObservableProperty]
    private int _threadCount;

    // RAM
    [ObservableProperty]
    private double _ramUsagePercent;

    [ObservableProperty]
    private ulong _ramUsedBytes;

    [ObservableProperty]
    private ulong _ramTotalBytes;

    [ObservableProperty]
    private ulong _ramAvailableBytes;

    [ObservableProperty]
    private ulong _pageFileTotalBytes;

    [ObservableProperty]
    private ulong _pageFileAvailBytes;

    [ObservableProperty]
    private string _formattedRamUsed = "0.00 GB";

    [ObservableProperty]
    private string _formattedRamTotal = "0.00 GB";

    [ObservableProperty]
    private string _formattedRamAvailable = "0.00 GB";

    [ObservableProperty]
    private string _formattedPageFileTotal = "0.00 GB";

    [ObservableProperty]
    private string _formattedPageFileAvail = "0.00 GB";

    // GPU
    [ObservableProperty]
    private string _gpuName = "Integrated / Dedicated Graphics";

    [ObservableProperty]
    private string _gpuDriverVersion = "WHQL Certified Driver";

    [ObservableProperty]
    private string _formattedGpuVram = "Dynamic VRAM Memory";

    // OS & Uptime
    [ObservableProperty]
    private string _osVersionText = "Windows 11 (Latest Build)";

    [ObservableProperty]
    private string _systemUptimeText = "0h 0m";

    [ObservableProperty]
    private string _machineName = "WORKSTATION";

    // Storage Volumes
    public ObservableCollection<DriveTelemetryModel> Drives { get; } = [];

    // Battery / Power
    [ObservableProperty]
    private bool _hasBattery;

    [ObservableProperty]
    private int _batteryPercent = 100;

    [ObservableProperty]
    private bool _isCharging;

    [ObservableProperty]
    private string _batteryStatus = "AC Power Connected";

    [ObservableProperty]
    private double _batteryWearLevelPercent = 100.0; // 100% = pristine health

    [ObservableProperty]
    private string _batteryHealthVerdict = "Normal (Good Condition)";
}
