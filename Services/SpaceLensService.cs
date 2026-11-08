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
using System.Threading;
using System.Threading.Tasks;
using LuminaCleaner.Interop;
using LuminaCleaner.Models;

namespace LuminaCleaner.Services;

public class SpaceLensService : ISpaceLensService
{
    public async Task<IReadOnlyList<SpaceLensItemModel>> ScanLibrariesAsync(
        IEnumerable<string> rootPaths,
        long minSizeBytes,
        IProgress<int>? progress,
        CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            var results = new List<SpaceLensItemModel>();
            int count = 0;

            foreach (var root in rootPaths)
            {
                ct.ThrowIfCancellationRequested();
                if (!Directory.Exists(root)) continue;

                var dirsToVisit = new Stack<string>();
                dirsToVisit.Push(root);

                while (dirsToVisit.Count > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    string currentDir = dirsToVisit.Pop();

                    try
                    {
                        var dirInfo = new DirectoryInfo(currentDir);
                        if (dirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                            continue; // Skip symlinks/junctions

                        foreach (var subDir in dirInfo.EnumerateDirectories())
                        {
                            try
                            {
                                if (!subDir.Attributes.HasFlag(FileAttributes.ReparsePoint) &&
                                    !subDir.Name.StartsWith('.'))
                                {
                                    dirsToVisit.Push(subDir.FullName);
                                }
                            }
                            catch { }
                        }

                        foreach (var file in dirInfo.EnumerateFiles())
                        {
                            ct.ThrowIfCancellationRequested();
                            try
                            {
                                if (file.Length >= minSizeBytes)
                                {
                                    results.Add(new SpaceLensItemModel
                                    {
                                        FullPath = file.FullName,
                                        FileName = file.Name,
                                        DirectoryName = currentDir,
                                        SizeBytes = file.Length,
                                        LastModified = file.LastWriteTime
                                    });
                                    count++;
                                    if (count % 25 == 0)
                                    {
                                        progress?.Report(count);
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
            }

            progress?.Report(count);
            return (IReadOnlyList<SpaceLensItemModel>)results;
        }, ct);
    }

    public bool RevealInExplorer(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
            return false;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{fullPath}\"",
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool DeleteToRecycleBin(string fullPath)
    {
        return NativeMethods.SendToRecycleBin(fullPath);
    }
}
