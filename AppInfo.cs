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

using System.Reflection;

namespace LuminaCleaner;

/// <summary>
/// Version information read from the assembly, so the version only has to be set in
/// Directory.Build.props (or by the release workflow from the git tag).
/// </summary>
public static class AppInfo
{
    private static readonly System.Version AssemblyVersion =
        typeof(AppInfo).Assembly.GetName().Version ?? new System.Version(0, 0, 0);

    /// <summary>"1.2.0"</summary>
    public static string Version { get; } = AssemblyVersion.ToString(3);

    /// <summary>"v1.2"</summary>
    public static string ShortVersion { get; } = $"v{AssemblyVersion.Major}.{AssemblyVersion.Minor}";

    /// <summary>"Version 1.2.0"</summary>
    public static string VersionLabel { get; } = $"Version {Version}";
}
