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

namespace LuminaCleaner.Models;

public class EngineOptions
{
    /// <summary>
    /// Guardrail: Files modified or created within this buffer are skipped in temp directories.
    /// Default is 24 hours to prevent disrupting active installers or background processes.
    /// </summary>
    public TimeSpan AgeSafetyBuffer { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// When true, simulates deletion without removing files.
    /// </summary>
    public bool DryRun { get; set; } = false;

    /// <summary>
    /// Maximum parallel tasks during folder exploration.
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;
}
