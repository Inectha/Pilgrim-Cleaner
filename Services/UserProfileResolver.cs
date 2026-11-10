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
using System.IO;
using System.Linq;
using LuminaCleaner.Interop;

namespace LuminaCleaner.Services;

/// <summary>
/// Safely resolves user profile directories for both single-user workstation sessions
/// and multi-user Windows Server / RDS / Citrix environments (--all-profiles or SYSTEM context).
/// </summary>
public static class UserProfileResolver
{
    private static readonly HashSet<string> ExcludedProfileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Public",
        "Default",
        "Default User",
        "All Users",
        "desktop.ini"
    };

    /// <summary>
    /// Returns the list of user profile root paths (e.g. C:\Users\Alice) to inspect.
    /// </summary>
    public static IReadOnlyList<string> GetTargetUserProfiles(bool includeAllProfiles)
    {
        var profiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string currentProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(currentProfile) && Directory.Exists(currentProfile))
        {
            profiles.Add(currentProfile);
        }

        // Also check if running as NT AUTHORITY\SYSTEM (where UserProfile points to systemprofile)
        bool isSystemAccount = Environment.UserName.Equals("SYSTEM", StringComparison.OrdinalIgnoreCase) ||
                               currentProfile.EndsWith(@"systemprofile", StringComparison.OrdinalIgnoreCase);

        if ((includeAllProfiles || isSystemAccount) && NativeMethods.IsRunningAsAdministrator())
        {
            string usersRoot = GetUsersRootDirectory();
            if (Directory.Exists(usersRoot))
            {
                try
                {
                    foreach (var dir in Directory.GetDirectories(usersRoot))
                    {
                        try
                        {
                            var di = new DirectoryInfo(dir);
                            if (ExcludedProfileNames.Contains(di.Name))
                                continue;

                            // Never traverse NTFS junctions/symlinks (e.g. "Default User", "All Users")
                            if ((di.Attributes & FileAttributes.ReparsePoint) != 0)
                                continue;

                            // Verify it looks like a real user profile (has AppData)
                            if (Directory.Exists(Path.Combine(dir, "AppData")))
                            {
                                profiles.Add(dir);
                            }
                        }
                        catch
                        {
                            // Skip inaccessible profile directory
                        }
                    }
                }
                catch
                {
                    // Suppress root enumeration error
                }
            }
        }

        return profiles.ToList();
    }

    public static string GetUsersRootDirectory()
    {
        string? systemDrive = Path.GetPathRoot(Environment.SystemDirectory);
        if (string.IsNullOrWhiteSpace(systemDrive))
            systemDrive = @"C:\";

        return Path.Combine(systemDrive, "Users");
    }
}
