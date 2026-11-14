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

namespace LuminaCleaner.Models;

/// <summary>
/// Represents the result of querying GitHub Releases for a newer version of Pilgrim Cleaner.
/// </summary>
public sealed class UpdateCheckResult
{
    public bool IsUpdateAvailable { get; init; }
    public string CurrentVersion { get; init; } = AppInfo.Version;
    public string LatestVersion { get; init; } = AppInfo.Version;
    public string LatestTag { get; init; } = $"v{AppInfo.Version}";
    public string ReleaseNotes { get; init; } = string.Empty;
    public string ReleaseUrl { get; init; } = string.Empty;
    public string InstallerDownloadUrl { get; init; } = string.Empty;
    public string ChecksumsDownloadUrl { get; init; } = string.Empty;
    public string ExpectedSha256 { get; init; } = string.Empty;
    public long InstallerSizeBytes { get; init; }
    public string? ErrorMessage { get; init; }
}
