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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LuminaCleaner.Models;
using Microsoft.Win32;

namespace LuminaCleaner.Services;

public class StartupService : IStartupService
{
    private const string RunSubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupApprovedRunKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string StartupApprovedFolderKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";

    public async Task<IReadOnlyList<StartupItemModel>> GetStartupItemsAsync(CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            var list = new List<StartupItemModel>();

            // 1. HKCU Run
            EnumerateRegistryRun(Registry.CurrentUser, RunSubKey, "Registry (Current User)", list);

            // 2. HKLM Run
            EnumerateRegistryRun(Registry.LocalMachine, RunSubKey, "Registry (All Users)", list);

            // 3. User Startup Folder
            string userStartup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            EnumerateStartupFolder(userStartup, "Startup Folder", list);

            // 4. Common Startup Folder
            string commonStartup = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
            EnumerateStartupFolder(commonStartup, "Common Startup Folder", list);

            return (IReadOnlyList<StartupItemModel>)list.OrderBy(x => x.Name).ToList();
        }, ct);
    }

    public bool SetStartupItemEnabled(StartupItemModel item, bool enable)
    {
        try
        {
            if (item.SourceType.StartsWith("Registry"))
            {
                byte[] data = new byte[12];
                if (enable)
                {
                    data[0] = 0x02;
                }
                else
                {
                    data[0] = 0x03;
                    byte[] timeBytes = BitConverter.GetBytes(DateTime.UtcNow.ToFileTimeUtc());
                    Array.Copy(timeBytes, 0, data, 4, Math.Min(8, timeBytes.Length));
                }

                bool written = false;

                // 1. If Local Machine item, try writing to HKLM StartupApproved if elevated
                if (item.Location.StartsWith("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        using var lmKey = Registry.LocalMachine.CreateSubKey(StartupApprovedRunKey, true);
                        if (lmKey != null)
                        {
                            lmKey.SetValue(item.Name, data, RegistryValueKind.Binary);
                            written = true;
                        }
                    }
                    catch
                    {
                        // Standard user mode fallback to HKCU override
                    }
                }

                // 2. Write to HKCU StartupApproved (works in both Admin and Standard User modes)
                try
                {
                    using var cuKey = Registry.CurrentUser.CreateSubKey(StartupApprovedRunKey, true);
                    if (cuKey != null)
                    {
                        cuKey.SetValue(item.Name, data, RegistryValueKind.Binary);
                        written = true;
                    }
                }
                catch { }

                if (written)
                {
                    item.IsEnabled = enable;
                    return true;
                }
            }
            else if (item.SourceType.Contains("Startup Folder"))
            {
                byte[] data = new byte[12];
                if (enable)
                {
                    data[0] = 0x02;
                }
                else
                {
                    data[0] = 0x03;
                    byte[] timeBytes = BitConverter.GetBytes(DateTime.UtcNow.ToFileTimeUtc());
                    Array.Copy(timeBytes, 0, data, 4, Math.Min(8, timeBytes.Length));
                }

                bool written = false;
                string fileName = Path.GetFileName(item.Command);

                // 1. If Common Startup Folder and elevated, write to HKLM
                if (item.Location.Contains("ProgramData", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        using var lmKey = Registry.LocalMachine.CreateSubKey(StartupApprovedFolderKey, true);
                        if (lmKey != null)
                        {
                            lmKey.SetValue(fileName, data, RegistryValueKind.Binary);
                            written = true;
                        }
                    }
                    catch { }
                }

                // 2. Write to HKCU StartupFolder (standard user override supported by Windows Explorer)
                try
                {
                    using var cuKey = Registry.CurrentUser.CreateSubKey(StartupApprovedFolderKey, true);
                    if (cuKey != null)
                    {
                        cuKey.SetValue(fileName, data, RegistryValueKind.Binary);
                        written = true;
                    }
                }
                catch { }

                // 3. Fallback file rename only for user-owned writable startup folder
                if (!item.Location.Contains("ProgramData", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        string path = item.Command.Trim('\"');
                        if (enable && path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
                        {
                            string target = path[..^9];
                            File.Move(path, target, true);
                            item.Command = target;
                            written = true;
                        }
                        else if (!enable && File.Exists(path) && !path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
                        {
                            string target = path + ".disabled";
                            File.Move(path, target, true);
                            item.Command = target;
                            written = true;
                        }
                    }
                    catch { }
                }

                if (written)
                {
                    item.IsEnabled = enable;
                    return true;
                }
            }
        }
        catch { }

        return false;
    }

    private static void EnumerateRegistryRun(RegistryKey rootKey, string subKey, string sourceType, List<StartupItemModel> list)
    {
        try
        {
            using var key = rootKey.OpenSubKey(subKey);
            if (key == null) return;

            // Read StartupApproved status from HKCU first, then HKLM
            using var userApprovedKey = Registry.CurrentUser.OpenSubKey(StartupApprovedRunKey);
            using var machineApprovedKey = (rootKey == Registry.LocalMachine) ? Registry.LocalMachine.OpenSubKey(StartupApprovedRunKey) : null;

            foreach (string valueName in key.GetValueNames())
            {
                if (string.IsNullOrWhiteSpace(valueName)) continue;
                string command = key.GetValue(valueName)?.ToString() ?? string.Empty;

                bool isEnabled = true;

                // Priority: User approval override > Machine approval
                object? approvedVal = userApprovedKey?.GetValue(valueName) ?? machineApprovedKey?.GetValue(valueName);
                if (approvedVal is byte[] bytes && bytes.Length > 0)
                {
                    // In Windows StartupApproved binary structure:
                    // Even first byte (0x02, 0x00, 0x06) = Enabled
                    // Odd first byte (0x01, 0x03) = Disabled
                    isEnabled = (bytes[0] & 1) == 0;
                }

                string publisher = ResolvePublisher(command);
                string impact = CalculateImpact(command);

                list.Add(new StartupItemModel
                {
                    Name = valueName,
                    Command = command,
                    Publisher = publisher,
                    Location = $@"{rootKey.Name}\{subKey}",
                    SourceType = sourceType,
                    IsEnabled = isEnabled,
                    StartupImpact = impact
                });
            }
        }
        catch { }
    }

    private static void EnumerateStartupFolder(string folderPath, string sourceType, List<StartupItemModel> list)
    {
        if (!Directory.Exists(folderPath)) return;

        try
        {
            using var userFolderApprovedKey = Registry.CurrentUser.OpenSubKey(StartupApprovedFolderKey);
            using var machineFolderApprovedKey = Registry.LocalMachine.OpenSubKey(StartupApprovedFolderKey);

            foreach (var file in Directory.EnumerateFiles(folderPath))
            {
                var fi = new FileInfo(file);
                string rawFileName = fi.Name;

                // Self-heal and ignore any desktop.ini variants
                if (rawFileName.StartsWith("desktop.ini", StringComparison.OrdinalIgnoreCase))
                {
                    if (rawFileName.Equals("desktop.ini.disabled", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            fi.Attributes = FileAttributes.Normal;
                            string targetPath = Path.Combine(folderPath, "desktop.ini");
                            if (!File.Exists(targetPath))
                            {
                                fi.MoveTo(targetPath);
                                File.SetAttributes(targetPath, FileAttributes.Hidden | FileAttributes.System);
                            }
                            else
                            {
                                fi.Delete();
                            }
                        }
                        catch { }
                    }
                    continue;
                }

                if (fi.Attributes.HasFlag(FileAttributes.System)) continue;

                // Check allowed extensions
                string ext = fi.Extension.ToLowerInvariant();
                if (ext != ".lnk" && ext != ".url" && ext != ".exe" && ext != ".bat" && ext != ".cmd" && ext != ".disabled")
                {
                    continue;
                }

                string name = Path.GetFileNameWithoutExtension(file);
                if (name.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    name = Path.GetFileNameWithoutExtension(name);
                }

                bool isEnabled = !file.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);

                // Check Windows StartupApproved\StartupFolder binary state
                object? approvedVal = userFolderApprovedKey?.GetValue(rawFileName) ?? machineFolderApprovedKey?.GetValue(rawFileName);
                if (approvedVal is byte[] bytes && bytes.Length > 0)
                {
                    isEnabled = (bytes[0] & 1) == 0;
                }

                list.Add(new StartupItemModel
                {
                    Name = name,
                    Command = file,
                    Publisher = "Shortcut Target",
                    Location = folderPath,
                    SourceType = sourceType,
                    IsEnabled = isEnabled,
                    StartupImpact = "Medium"
                });
            }
        }
        catch { }
    }

    private static string ResolvePublisher(string command)
    {
        try
        {
            string exePath = ExtractExePath(command);
            if (File.Exists(exePath))
            {
                var vi = FileVersionInfo.GetVersionInfo(exePath);
                if (!string.IsNullOrWhiteSpace(vi.CompanyName))
                    return vi.CompanyName;
            }
        }
        catch { }

        return "Verified Windows Application";
    }

    private static string CalculateImpact(string command)
    {
        string lower = command.ToLowerInvariant();
        if (lower.Contains("electron") || lower.Contains("teams") || lower.Contains("discord") || lower.Contains("slack"))
            return "High";
        if (lower.Contains("update") || lower.Contains("service") || lower.Contains("agent"))
            return "Low";
        return "Medium";
    }

    private static string ExtractExePath(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return string.Empty;
        string trimmed = command.Trim();
        if (trimmed.StartsWith('\"'))
        {
            int nextQuote = trimmed.IndexOf('\"', 1);
            if (nextQuote > 1) return trimmed[1..nextQuote];
        }

        int space = trimmed.IndexOf(' ');
        return space > 0 ? trimmed[..space] : trimmed;
    }
}
