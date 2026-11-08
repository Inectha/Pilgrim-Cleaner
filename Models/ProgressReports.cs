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
using System.Collections.Generic;

namespace LuminaCleaner.Models;

public record ScanProgressReport(
    string CurrentPath,
    int ItemsFound,
    long TotalBytesFound,
    string ModuleName,
    double Percentage = 0);

public record CleanProgressReport(
    string CurrentFile,
    long CleanedBytes,
    long SkippedBytes,
    int ItemsProcessed,
    int TotalItems,
    double Percentage,
    string ModuleName);

public record ModuleScanResult(
    string ModuleId,
    List<CleanItemModel> Items,
    long TotalBytes,
    long SkippedBytes,
    int SkippedCount,
    TimeSpan Elapsed);

public record ModuleCleanResult(
    string ModuleId,
    long CleanedBytes,
    int CleanedCount,
    int SkippedCount,
    int FailedCount,
    TimeSpan Elapsed);
