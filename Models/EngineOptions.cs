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

    /// <summary>
    /// When true and running with administrative/SYSTEM privileges, enumerates target folders
    /// across all user profiles under C:\Users\* (ideal for Windows Server / RDS / Citrix).
    /// </summary>
    public bool AllProfiles { get; set; } = false;

    /// <summary>
    /// Optimizes execution for CLI and unattended background tasks by bypassing UI dispatcher
    /// synchronization and per-item UI property change wiring.
    /// </summary>
    public bool HeadlessMode { get; set; } = false;

    /// <summary>
    /// Minimum age threshold for Windows Server logs (IIS W3SVC logs and archived CBS logs).
    /// Default is 14 days to preserve recent diagnostic and security audit trails.
    /// </summary>
    public TimeSpan ServerLogRetention { get; set; } = TimeSpan.FromDays(14);

    /// <summary>
    /// Active execution profile preset (Default, Workstation, ServerSafe, Rds).
    /// </summary>
    public CleanupProfile Profile { get; set; } = CleanupProfile.Default;

    /// <summary>
    /// Optional custom path/substring exclusions (e.g. from --exclude-path).
    /// </summary>
    public List<string> ExcludedPaths { get; set; } = [];
}
