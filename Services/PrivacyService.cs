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
using System.Threading;
using System.Threading.Tasks;
using LuminaCleaner.Interop;
using LuminaCleaner.Models;
using Microsoft.Win32;
using Wpf.Ui.Controls;

namespace LuminaCleaner.Services;

public class PrivacyService : IPrivacyService
{
    private static readonly string RecentFolderPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Recent");

    private static readonly string AutoDestFolderPath =
        Path.Combine(RecentFolderPath, "AutomaticDestinations");

    private static readonly string CustomDestFolderPath =
        Path.Combine(RecentFolderPath, "CustomDestinations");

    public async Task<IReadOnlyList<PrivacyTargetModel>> AuditPrivacyAsync(CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            var targets = new List<PrivacyTargetModel>();

            // 1. Windows Recent Documents / Jump Lists
            var (recentCount, recentBytes, recentTraces) = AuditFolder(RecentFolderPath, false);
            var recentTarget = new PrivacyTargetModel
            {
                Id = "recent_items",
                Title = "Windows Recent Documents History",
                Description = "Tracks recent files, documents, and media opened across File Explorer.",
                Category = "Explorer & Activity",
                IconSymbol = SymbolRegular.DocumentBulletList24,
                ItemCount = recentCount,
                SizeBytes = recentBytes
            };
            foreach (var t in recentTraces) recentTarget.DiscoveredTraces.Add(t);
            targets.Add(recentTarget);

            // 2. Taskbar Jump Lists (Automatic Destinations)
            var (autoCount, autoBytes, autoTraces) = AuditFolder(AutoDestFolderPath, true);
            var autoTarget = new PrivacyTargetModel
            {
                Id = "jump_lists",
                Title = "Taskbar Jump Lists & Frequent Folders",
                Description = "History of frequently pinned and accessed items on the Windows Taskbar.",
                Category = "Taskbar & Shell",
                IconSymbol = SymbolRegular.TaskListSquareLtr24,
                ItemCount = autoCount,
                SizeBytes = autoBytes
            };
            foreach (var t in autoTraces) autoTarget.DiscoveredTraces.Add(t);
            targets.Add(autoTarget);

            // 3. Custom Destinations
            var (customCount, customBytes, customTraces) = AuditFolder(CustomDestFolderPath, true);
            var customTarget = new PrivacyTargetModel
            {
                Id = "custom_destinations",
                Title = "Application Quick Launch Destinations",
                Description = "Per-app recent history registered by third-party Win32 software.",
                Category = "Taskbar & Shell",
                IconSymbol = SymbolRegular.AppsListDetail24,
                ItemCount = customCount,
                SizeBytes = customBytes
            };
            foreach (var t in customTraces) customTarget.DiscoveredTraces.Add(t);
            targets.Add(customTarget);

            // 4. Windows Explorer Search & Run Command History (MRU)
            var mruTraces = AuditExplorerMru();
            var mruTarget = new PrivacyTargetModel
            {
                Id = "explorer_history",
                Title = "File Explorer Search & Run History",
                Description = "Typed commands in Run dialog, File Explorer address bar, and search queries.",
                Category = "Explorer & Activity",
                IconSymbol = SymbolRegular.SearchSquare24,
                ItemCount = mruTraces.Count,
                SizeBytes = mruTraces.Sum(x => x.SizeBytes)
            };
            foreach (var t in mruTraces) mruTarget.DiscoveredTraces.Add(t);
            targets.Add(mruTarget);

            // 5. Windows Clipboard History Buffer
            var clipTarget = new PrivacyTargetModel
            {
                Id = "clipboard_buffer",
                Title = "Live Clipboard Buffer",
                Description = "Active copied text, images, or credentials in system memory.",
                Category = "System & Memory",
                IconSymbol = SymbolRegular.Clipboard24,
                ItemCount = 1,
                SizeBytes = 0
            };
            clipTarget.DiscoveredTraces.Add(new PrivacyTraceItemModel
            {
                FileName = "Active Memory Clipboard Payload",
                FilePath = "RAM // System Clipboard Buffer",
                SizeBytes = 1024,
                LastAccessed = DateTime.Now
            });
            targets.Add(clipTarget);

            return (IReadOnlyList<PrivacyTargetModel>)targets;
        }, ct);
    }

    public async Task<int> PurgePrivacyTargetsAsync(IEnumerable<PrivacyTargetModel> targets, CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            int purged = 0;
            foreach (var target in targets)
            {
                ct.ThrowIfCancellationRequested();
                if (!target.IsSelected) continue;

                switch (target.Id)
                {
                    case "recent_items":
                        purged += PurgeFolder(RecentFolderPath, false);
                        break;

                    case "jump_lists":
                        purged += PurgeFolder(AutoDestFolderPath, true);
                        break;

                    case "custom_destinations":
                        purged += PurgeFolder(CustomDestFolderPath, true);
                        break;

                    case "explorer_history":
                        purged += PurgeExplorerMru();
                        break;

                    case "clipboard_buffer":
                        NativeMethods.ClearSystemClipboard();
                        purged++;
                        break;
                }
            }

            return purged;
        }, ct);
    }

    private static (int Count, long Bytes, List<PrivacyTraceItemModel> Traces) AuditFolder(string path, bool recursive)
    {
        int count = 0;
        long bytes = 0;
        var traces = new List<PrivacyTraceItemModel>();

        if (!Directory.Exists(path)) return (0, 0, traces);

        try
        {
            var opt = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            foreach (var file in Directory.EnumerateFiles(path, "*.*", opt))
            {
                try
                {
                    var fi = new FileInfo(file);
                    count++;
                    bytes += fi.Length;

                    // Keep top 250 traces for snappy UI rendering
                    if (traces.Count < 250)
                    {
                        traces.Add(new PrivacyTraceItemModel
                        {
                            FileName = fi.Name,
                            FilePath = fi.FullName,
                            SizeBytes = fi.Length,
                            LastAccessed = fi.LastWriteTime
                        });
                    }
                }
                catch { }
            }
        }
        catch { }

        return (count, bytes, traces);
    }

    private static List<PrivacyTraceItemModel> AuditExplorerMru()
    {
        var traces = new List<PrivacyTraceItemModel>();
        try
        {
            // Run dialog MRU
            using var runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\RunMRU");
            if (runKey != null)
            {
                foreach (var name in runKey.GetValueNames())
                {
                    if (name.Length == 1 && char.IsLetter(name[0]))
                    {
                        string val = runKey.GetValue(name)?.ToString() ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(val))
                        {
                            traces.Add(new PrivacyTraceItemModel
                            {
                                FileName = $"Run command: {val.TrimEnd('\\', '1')}",
                                FilePath = $@"HKCU\...\RunMRU\{name}",
                                SizeBytes = 128,
                                LastAccessed = DateTime.Now
                            });
                        }
                    }
                }
            }

            // Typed Paths in Explorer address bar
            using var pathsKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\TypedPaths");
            if (pathsKey != null)
            {
                foreach (var name in pathsKey.GetValueNames())
                {
                    string pathVal = pathsKey.GetValue(name)?.ToString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(pathVal))
                    {
                        traces.Add(new PrivacyTraceItemModel
                        {
                            FileName = $"Address bar: {pathVal}",
                            FilePath = $@"HKCU\...\TypedPaths\{name}",
                            SizeBytes = 256,
                            LastAccessed = DateTime.Now
                        });
                    }
                }
            }
        }
        catch { }

        return traces;
    }

    private static int PurgeExplorerMru()
    {
        int count = 0;
        try
        {
            using var runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\RunMRU", true);
            if (runKey != null)
            {
                foreach (var name in runKey.GetValueNames())
                {
                    runKey.DeleteValue(name, false);
                    count++;
                }
            }

            using var pathsKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\TypedPaths", true);
            if (pathsKey != null)
            {
                foreach (var name in pathsKey.GetValueNames())
                {
                    pathsKey.DeleteValue(name, false);
                    count++;
                }
            }
        }
        catch { }

        return count;
    }

    private static int PurgeFolder(string path, bool recursive)
    {
        int purged = 0;
        if (!Directory.Exists(path)) return 0;

        try
        {
            var opt = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            foreach (var file in Directory.EnumerateFiles(path, "*.*", opt))
            {
                try
                {
                    File.Delete(file);
                    purged++;
                }
                catch { }
            }
        }
        catch { }

        return purged;
    }
}
