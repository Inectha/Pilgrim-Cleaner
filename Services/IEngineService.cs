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
using System.Threading;
using System.Threading.Tasks;
using LuminaCleaner.Models;
using LuminaCleaner.Modules;

namespace LuminaCleaner.Services;

public interface IEngineService
{
    IReadOnlyList<ICleanerModule> Modules { get; }

    event Action<LogMessageModel>? LogAppended;

    void Log(LogLevel level, string tag, string message, string? details = null);

    Task<List<ModuleScanResult>> ScanAllAsync(
        EngineOptions options,
        IProgress<ScanProgressReport> progress,
        CancellationToken ct);

    Task<List<ModuleCleanResult>> CleanSelectedAsync(
        EngineOptions options,
        IProgress<CleanProgressReport> progress,
        CancellationToken ct);

    void ResetAll();
}
