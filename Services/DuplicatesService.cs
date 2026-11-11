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
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using LuminaCleaner.Interop;
using LuminaCleaner.Models;

namespace LuminaCleaner.Services;

public class DuplicatesService : IDuplicatesService
{
    public async Task<IReadOnlyList<DuplicateGroupModel>> FindDuplicatesAsync(
        IEnumerable<string> rootPaths,
        IProgress<int>? progress,
        CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            var filesBySize = new Dictionary<long, List<FileInfo>>();
            int filesScanned = 0;

            // The same file must never be counted twice: with overlapping roots (e.g. "D:\Data" and
            // "D:\Data\Photos", or Pictures redirected into Documents) a file would otherwise become a
            // "duplicate" of itself and its only copy would be recycled.
            var seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var root in NormalizeRoots(rootPaths))
            {
                ct.ThrowIfCancellationRequested();
                if (!Directory.Exists(root)) continue;

                var dirs = new Stack<string>();
                dirs.Push(root);

                while (dirs.Count > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    string current = dirs.Pop();

                    try
                    {
                        var dirInfo = new DirectoryInfo(current);
                        if (dirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                            continue;

                        foreach (var sub in dirInfo.EnumerateDirectories())
                        {
                            try
                            {
                                if (sub.Attributes.HasFlag(FileAttributes.ReparsePoint) || sub.Name.StartsWith('.'))
                                    continue;

                                string subName = sub.Name.ToLowerInvariant();
                                if (subName is "node_modules" or ".git" or "bin" or "obj" or ".vs" or ".nuget" or ".cache" or "appdata")
                                    continue;

                                dirs.Push(sub.FullName);
                            }
                            catch { }
                        }

                        foreach (var file in dirInfo.EnumerateFiles())
                        {
                            ct.ThrowIfCancellationRequested();
                            try
                            {
                                if (file.Length > 1024 && seenFiles.Add(file.FullName)) // Focus on files > 1 KB, each path once
                                {
                                    if (!filesBySize.TryGetValue(file.Length, out var list))
                                    {
                                        list = [];
                                        filesBySize[file.Length] = list;
                                    }
                                    list.Add(file);

                                    filesScanned++;
                                    if (filesScanned % 20 == 0)
                                    {
                                        progress?.Report(filesScanned);
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
            }

            // Phase 2: Compute SHA256 with 2-tier fast prefix screening
            var duplicateGroups = new List<DuplicateGroupModel>();
            var candidateGroups = filesBySize.Where(kvp => kvp.Value.Count >= 2).ToList();

            using var sha256 = SHA256.Create();

            foreach (var candidate in candidateGroups)
            {
                ct.ThrowIfCancellationRequested();

                // If candidate files are larger than 64 KB, pre-screen by 4KB header hash
                IEnumerable<List<FileInfo>> clusters;
                if (candidate.Key > 64 * 1024)
                {
                    var prefixMap = new Dictionary<string, List<FileInfo>>();
                    foreach (var fi in candidate.Value)
                    {
                        try
                        {
                            string prefixHash = ComputeHeaderHash(fi, sha256);
                            if (!prefixMap.TryGetValue(prefixHash, out var pList))
                            {
                                pList = [];
                                prefixMap[prefixHash] = pList;
                            }
                            pList.Add(fi);
                        }
                        catch { }
                    }
                    clusters = prefixMap.Values.Where(v => v.Count >= 2);
                }
                else
                {
                    clusters = [candidate.Value];
                }

                foreach (var cluster in clusters)
                {
                    var hashMap = new Dictionary<string, List<FileInfo>>();

                    foreach (var fi in cluster)
                    {
                        try
                        {
                            using var stream = new FileStream(fi.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
                            byte[] hashBytes = sha256.ComputeHash(stream);
                            string hashStr = Convert.ToHexString(hashBytes);

                            if (!hashMap.TryGetValue(hashStr, out var hashList))
                            {
                                hashList = [];
                                hashMap[hashStr] = hashList;
                            }
                            hashList.Add(fi);
                        }
                        catch { }
                    }

                    foreach (var (hash, matchingFiles) in hashMap)
                    {
                        if (matchingFiles.Count >= 2)
                        {
                            var group = new DuplicateGroupModel
                            {
                                FileHash = hash,
                                SizeBytes = candidate.Key
                            };

                            // Sort copies by LastModified (oldest is original)
                            var ordered = matchingFiles.OrderBy(f => f.LastWriteTime).ToList();
                            for (int i = 0; i < ordered.Count; i++)
                            {
                                var f = ordered[i];
                                group.Copies.Add(new DuplicateFileItemModel
                                {
                                    FullPath = f.FullName,
                                    FileName = f.Name,
                                    DirectoryPath = f.DirectoryName ?? string.Empty,
                                    SizeBytes = f.Length,
                                    LastModified = f.LastWriteTime,
                                    IsOriginal = (i == 0),
                                    IsSelected = (i > 0) // Pre-select copies for deletion
                                });
                            }

                            duplicateGroups.Add(group);
                        }
                    }
                }
            }

            return (IReadOnlyList<DuplicateGroupModel>)duplicateGroups.OrderByDescending(g => g.WastedBytes).ToList();
        }, ct);
    }

    /// <summary>
    /// Returns full, de-duplicated root paths with nested roots removed
    /// ("D:\Data" and "D:\Data\Photos" -> "D:\Data").
    /// </summary>
    public static List<string> NormalizeRoots(IEnumerable<string> rootPaths)
    {
        var normalized = new List<string>();
        foreach (var root in rootPaths)
        {
            if (string.IsNullOrWhiteSpace(root)) continue;
            try
            {
                normalized.Add(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)));
            }
            catch
            {
                // Ignore invalid paths
            }
        }

        var result = new List<string>();
        foreach (var root in normalized.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(r => r.Length))
        {
            bool nested = result.Any(parent =>
                root.StartsWith(Path.EndsInDirectorySeparator(parent) ? parent : parent + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase));
            if (!nested)
                result.Add(root);
        }
        return result;
    }

    private static string ComputeHeaderHash(FileInfo fi, SHA256 sha256)
    {
        using var stream = new FileStream(fi.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
        byte[] buffer = new byte[Math.Min(4096, (int)stream.Length)];
        int read = stream.Read(buffer, 0, buffer.Length);
        byte[] hash = sha256.ComputeHash(buffer, 0, read);
        return Convert.ToHexString(hash);
    }

    public bool DeleteFileToRecycleBin(string path, bool interactive = false)
    {
        return NativeMethods.SendToRecycleBin(path, warnIfPermanent: interactive);
    }

    public bool RecycleDuplicate(string copyPath, string keptPath, bool interactive = false)
    {
        // Last line of defence: only remove a copy when the file we keep is a different,
        // existing file of the same size.
        try
        {
            string copy = Path.GetFullPath(copyPath);
            string kept = Path.GetFullPath(keptPath);
            if (string.Equals(copy, kept, StringComparison.OrdinalIgnoreCase))
                return false;

            var keptInfo = new FileInfo(kept);
            var copyInfo = new FileInfo(copy);
            if (!keptInfo.Exists || !copyInfo.Exists || keptInfo.Length != copyInfo.Length)
                return false;
        }
        catch
        {
            return false;
        }

        return DeleteFileToRecycleBin(copyPath, interactive);
    }
}
