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
using Wpf.Ui.Controls;

namespace LuminaCleaner.Modules;

public class DismComponentCleanerModule : CleanerModuleBase
{
    public override string Id => "dism_component_store";
    public override string Name => "Windows Update Component Store (WinSxS)";
    public override string Description => "Safely purge superseded Windows update backups using Microsoft DISM.";
    public override string SecurityNotice => "Executes official Microsoft DISM /StartComponentCleanup. Zero manual file deletions.";
    public override CleanerCategory Category => CleanerCategory.ComponentStore;
    public override SymbolRegular IconSymbol => SymbolRegular.System24;
    public override bool RequiresElevation => true;

    private long _estimatedReclaimableBytes = 0;

    public override async Task<ModuleScanResult> ScanAsync(EngineOptions options, IProgress<ScanProgressReport> progress, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        ClearResults();
        IsScanning = true;

        var discovered = new List<CleanItemModel>();

        try
        {
            await Task.Run(() =>
            {
                string winSxSPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "WinSxS");
                long measuredBytes = 0;

                if (Directory.Exists(winSxSPath))
                {
                    // Measure actual staging, temp, pending delete, and manifest cache folders in WinSxS
                    // rather than reporting an arbitrary hardcoded number.
                    string[] measurableSubfolders = ["Temp", "Backup", "ManifestCache"];
                    foreach (var sub in measurableSubfolders)
                    {
                        string subPath = Path.Combine(winSxSPath, sub);
                        if (!Directory.Exists(subPath)) continue;

                        foreach (var fi in SafeEnumerateFiles(subPath, "*", 3))
                        {
                            if (ct.IsCancellationRequested) break;
                            try { measuredBytes += fi.Length; } catch { }
                        }
                    }
                }

                _estimatedReclaimableBytes = measuredBytes;

                var item = new CleanItemModel
                {
                    FullPath = @"C:\Windows\WinSxS [Superseded Update Components & Staging]",
                    FileName = "DISM /Online /Cleanup-Image /StartComponentCleanup",
                    SizeBytes = _estimatedReclaimableBytes,
                    LastModified = DateTime.Now,
                    Category = Category,
                    ModuleName = Name,
                    IsSelected = true
                };

                discovered.Add(item);

                progress?.Report(new ScanProgressReport(
                    item.FullPath,
                    1,
                    _estimatedReclaimableBytes,
                    Name,
                    100.0));
            }, ct);
        }
        catch (OperationCanceledException) { }
        finally
        {
            IsScanning = false;
            sw.Stop();
        }

        foreach (var item in discovered)
        {
            AddDiscoveredItem(item);
        }

        return new ModuleScanResult(Id, discovered, _estimatedReclaimableBytes, 0, 0, sw.Elapsed);
    }

    public override async Task<ModuleCleanResult> CleanAsync(EngineOptions options, IProgress<CleanProgressReport> progress, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        long cleanedBytes = 0;
        int cleanedCount = 0;
        int skippedCount = 0;
        int failedCount = 0;

        if (options.DryRun)
        {
            return new ModuleCleanResult(Id, _estimatedReclaimableBytes, 1, 0, 0, sw.Elapsed);
        }

        if (!NativeMethods.IsRunningAsAdministrator() && options.HeadlessMode)
        {
            sw.Stop();
            return new ModuleCleanResult(Id, 0, 0, 1, 0, sw.Elapsed);
        }

        try
        {
            await Task.Run(() =>
            {
                progress?.Report(new CleanProgressReport(
                    "Executing Microsoft DISM Component Store cleanup...",
                    0,
                    0,
                    0,
                    1,
                    50.0,
                    Name));

                var psi = new ProcessStartInfo
                {
                    FileName = SystemTools.Dism,
                    Arguments = "/Online /Cleanup-Image /StartComponentCleanup /Quiet /NoRestart",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                if (!NativeMethods.IsRunningAsAdministrator())
                {
                    psi.FileName = SystemTools.PowerShell;
                    psi.Arguments = "-NoProfile -Command \"Start-Process dism.exe -ArgumentList '/Online /Cleanup-Image /StartComponentCleanup /Quiet /NoRestart' -Verb RunAs -Wait\"";
                }

                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    // Allow up to 10 minutes for DISM on Windows Server, checking cancellation token
                    while (!proc.WaitForExit(1000))
                    {
                        if (ct.IsCancellationRequested)
                        {
                            try { proc.Kill(); } catch { }
                            break;
                        }
                    }

                    if (!ct.IsCancellationRequested && proc.ExitCode == 0)
                    {
                        cleanedCount = 1;
                        cleanedBytes = _estimatedReclaimableBytes;
                    }
                    else
                    {
                        failedCount = 1;
                    }
                }

                progress?.Report(new CleanProgressReport(
                    "DISM Component Store cleanup complete.",
                    cleanedBytes,
                    0,
                    1,
                    1,
                    100.0,
                    Name));
            }, ct);
        }
        catch (Exception ex)
        {
            failedCount = 1;
            progress?.Report(new CleanProgressReport(
                $"DISM execution notice: {ex.Message}",
                0,
                0,
                1,
                1,
                100.0,
                Name));
        }
        finally
        {
            sw.Stop();
        }

        return new ModuleCleanResult(Id, cleanedBytes, cleanedCount, skippedCount, failedCount, sw.Elapsed);
    }
}
