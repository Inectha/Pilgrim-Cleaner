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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using LuminaCleaner.Interop;
using LuminaCleaner.Models;

namespace LuminaCleaner.Services;

public class AppManagerService : IAppManagerService
{
    private static readonly HashSet<string> ProtectedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft Windows",
        "Windows Operating System",
        "Windows Defender",
        "Windows Security",
        "Internet Explorer"
    };

    // Folder names that are shared by many programs or hold Windows/user data.
    // They are never offered as "leftovers", whatever an uninstall entry claims.
    private static readonly HashSet<string> SharedFolderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft", "Microsoft Corporation", "Microsoft Office", "Windows", "WindowsApps",
        "ModifiableWindowsApps", "Common Files", "Internet Explorer", "Packages", "Programs",
        "Temp", "Package Cache", "Application Data", "dotnet", "PowerShell", "Reference Assemblies",
        "MSBuild", "Google", "Mozilla", "Desktop", "Documents", "Downloads", "Local", "LocalLow", "Roaming"
    };

    // Exit codes that mean "uninstalled": success, and MSI success with (initiated) reboot.
    private static readonly HashSet<int> SuccessExitCodes = [0, 3010, 1641];

    // Inventory from the last scan, used to avoid deleting folders that other installed apps still use.
    private List<AppItemModel> _lastInventory = [];

    public async Task<List<AppItemModel>> GetInstalledApplicationsAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var apps = new Dictionary<string, AppItemModel>(StringComparer.OrdinalIgnoreCase);

            // 1. Scan 64-bit Local Machine
            ScanRegistryUninstallKey(RegistryHive.LocalMachine, RegistryView.Registry64, apps);

            // 2. Scan 32-bit Local Machine (WOW6432Node)
            ScanRegistryUninstallKey(RegistryHive.LocalMachine, RegistryView.Registry32, apps);

            // 3. Scan Current User
            ScanRegistryUninstallKey(RegistryHive.CurrentUser, RegistryView.Default, apps);

            // 4. Enumerate Packaged Windows Store Apps
            ScanPackagedWindowsApps(apps);

            var inventory = apps.Values
                .OrderBy(a => a.DisplayName)
                .ToList();
            _lastInventory = inventory;
            return inventory;
        }, ct);
    }

    private static void ScanRegistryUninstallKey(RegistryHive hive, RegistryView view, Dictionary<string, AppItemModel> destination)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var uninstallKey = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            if (uninstallKey == null) return;

            foreach (var subKeyName in uninstallKey.GetSubKeyNames())
            {
                try
                {
                    using var appKey = uninstallKey.OpenSubKey(subKeyName);
                    if (appKey == null) continue;

                    string? displayName = appKey.GetValue("DisplayName")?.ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(displayName)) continue;

                    // Exclude System Components and security hotfixes
                    int systemComponent = Convert.ToInt32(appKey.GetValue("SystemComponent") ?? 0);
                    if (systemComponent == 1) continue;

                    string? parentKeyName = appKey.GetValue("ParentKeyName")?.ToString();
                    if (!string.IsNullOrWhiteSpace(parentKeyName)) continue;

                    if (displayName.StartsWith("KB", StringComparison.OrdinalIgnoreCase) &&
                        displayName.Length > 2 && char.IsDigit(displayName[2]))
                    {
                        continue;
                    }

                    if (ProtectedNames.Contains(displayName)) continue;

                    string displayVersion = appKey.GetValue("DisplayVersion")?.ToString()?.Trim() ?? "";
                    string publisher = appKey.GetValue("Publisher")?.ToString()?.Trim() ?? "Independent Software";
                    string uninstallString = appKey.GetValue("UninstallString")?.ToString()?.Trim() ?? "";
                    string quietUninstallString = appKey.GetValue("QuietUninstallString")?.ToString()?.Trim() ?? "";
                    string installLocation = appKey.GetValue("InstallLocation")?.ToString()?.Trim() ?? "";
                    string displayIcon = appKey.GetValue("DisplayIcon")?.ToString()?.Trim() ?? "";

                    // Parse size in KB (DWORD)
                    long estimatedSizeBytes = 0;
                    object? sizeObj = appKey.GetValue("EstimatedSize");
                    if (sizeObj != null && long.TryParse(sizeObj.ToString(), out long sizeKb))
                    {
                        estimatedSizeBytes = sizeKb * 1024;
                    }
                    else if (!string.IsNullOrWhiteSpace(installLocation) && Directory.Exists(installLocation))
                    {
                        estimatedSizeBytes = CalculateDirectorySize(installLocation, 2);
                    }

                    // Parse install date
                    string installDate = ParseInstallDate(appKey.GetValue("InstallDate")?.ToString());

                    string dedupeKey = $"{displayName}::{displayVersion}";
                    if (!destination.ContainsKey(dedupeKey))
                    {
                        destination[dedupeKey] = new AppItemModel
                        {
                            DisplayName = displayName,
                            DisplayVersion = displayVersion,
                            Publisher = publisher,
                            InstallDate = installDate,
                            EstimatedSizeBytes = estimatedSizeBytes,
                            FormattedSize = FormatBytes(estimatedSizeBytes),
                            DisplayIcon = CleanIconPath(displayIcon),
                            UninstallString = uninstallString,
                            QuietUninstallString = quietUninstallString,
                            InstallLocation = installLocation,
                            IsPackagedApp = false,
                            Category = "Win32 Desktop",
                            IsPerUserEntry = hive == RegistryHive.CurrentUser,
                            RegistryHive = hive,
                            RegistryView = view,
                            RegistryKeyPath = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{subKeyName}"
                        };
                    }
                }
                catch
                {
                    // Suppress individual subkey access exceptions
                }
            }
        }
        catch
        {
            // Suppress base key access exceptions
        }
    }

    private static void ScanPackagedWindowsApps(Dictionary<string, AppItemModel> destination)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NonInteractive -Command \"Get-AppxPackage -ErrorAction SilentlyContinue | Where-Object { -not $_.IsFramework -and -not $_.NonRemovable } | Select-Object Name, Publisher, Version, InstallLocation, PackageFullName | ConvertTo-Json -Compress\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return;

            string json = process.StandardOutput.ReadToEnd();
            process.WaitForExit(4000);

            if (string.IsNullOrWhiteSpace(json)) return;

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    AddPackagedAppFromJson(element, destination);
                }
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                AddPackagedAppFromJson(doc.RootElement, destination);
            }
        }
        catch
        {
            // Graceful fallback if PowerShell or AppX query is restricted
        }
    }

    private static void AddPackagedAppFromJson(JsonElement element, Dictionary<string, AppItemModel> destination)
    {
        try
        {
            string name = element.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(name)) return;

            string publisher = element.TryGetProperty("Publisher", out var p) ? p.GetString() ?? "Microsoft Store" : "Microsoft Store";
            string version = element.TryGetProperty("Version", out var v) ? v.GetString() ?? "" : "";
            string installLocation = element.TryGetProperty("InstallLocation", out var il) ? il.GetString() ?? "" : "";
            string packageFullName = element.TryGetProperty("PackageFullName", out var pfn) ? pfn.GetString() ?? "" : "";

            // Friendly display name
            string friendlyName = name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase)
                ? name[10..]
                : name;

            string dedupeKey = $"{friendlyName}::{version}";
            if (!destination.ContainsKey(dedupeKey))
            {
                long sizeBytes = 0;
                if (!string.IsNullOrWhiteSpace(installLocation) && Directory.Exists(installLocation))
                {
                    sizeBytes = CalculateDirectorySize(installLocation, 1);
                }

                destination[dedupeKey] = new AppItemModel
                {
                    DisplayName = friendlyName,
                    DisplayVersion = version,
                    Publisher = CleanPublisher(publisher),
                    InstallDate = "Store Managed",
                    EstimatedSizeBytes = sizeBytes,
                    FormattedSize = FormatBytes(sizeBytes),
                    InstallLocation = installLocation,
                    IsPackagedApp = true,
                    PackageFullName = packageFullName,
                    UninstallString = $"powershell.exe -Command \"Remove-AppxPackage -Package '{packageFullName}'\"",
                    Category = "Windows Store App"
                };
            }
        }
        catch
        {
            // Suppress item parse errors
        }
    }

    public async Task<bool> UninstallApplicationAsync(AppItemModel app, bool quiet = false, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                app.IsBusy = true;
                app.Status = "Uninstalling...";

                if (app.IsPackagedApp && !string.IsNullOrWhiteSpace(app.PackageFullName))
                {
                    // Store apps are removed per user and need no elevation. Running this "as administrator"
                    // would remove the package for the admin account instead of the signed-in user.
                    var psi = new ProcessStartInfo
                    {
                        FileName = Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"),
                        Arguments = $"-NoProfile -NonInteractive -Command \"Remove-AppxPackage -Package '{app.PackageFullName}'\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var proc = Process.Start(psi);
                    if (proc == null)
                    {
                        app.Status = "Failed: could not start PowerShell";
                        return false;
                    }
                    proc.WaitForExit();
                    if (proc.ExitCode != 0)
                    {
                        app.Status = $"Failed: Remove-AppxPackage exited with code {proc.ExitCode}";
                        return false;
                    }
                    app.Status = "Uninstalled";
                    return true;
                }

                if (app.IsPerUserEntry && NativeMethods.IsRunningAsAdministrator())
                {
                    // HKCU uninstall commands can be written by any program the user runs. Starting one from an
                    // elevated Pilgrim Cleaner would hand that program administrator rights without a UAC prompt.
                    app.Status = "Skipped: per-user app. Uninstall it from a non-elevated Pilgrim Cleaner or Windows Settings.";
                    return false;
                }

                string targetCommand = quiet && !string.IsNullOrWhiteSpace(app.QuietUninstallString)
                    ? app.QuietUninstallString
                    : app.UninstallString;

                if (string.IsNullOrWhiteSpace(targetCommand))
                {
                    app.Status = "No uninstall string found";
                    return false;
                }

                // If quiet requested and no dedicated quiet string, enhance msiexec
                if (quiet && string.IsNullOrWhiteSpace(app.QuietUninstallString))
                {
                    if (targetCommand.Contains("msiexec", StringComparison.OrdinalIgnoreCase))
                    {
                        targetCommand = Regex.Replace(targetCommand, @"/I(?=\{)", "/X", RegexOptions.IgnoreCase);
                        if (!targetCommand.Contains("/qn", StringComparison.OrdinalIgnoreCase))
                        {
                            targetCommand += " /qn /norestart";
                        }
                    }
                    else if (targetCommand.EndsWith(".exe\"", StringComparison.OrdinalIgnoreCase) || targetCommand.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        targetCommand += " /S /VERYSILENT";
                    }
                }

                // Parse executable and arguments
                string fileName;
                string arguments = "";

                targetCommand = targetCommand.Trim();
                if (targetCommand.StartsWith("\""))
                {
                    int endQuote = targetCommand.IndexOf('"', 1);
                    if (endQuote > 0)
                    {
                        fileName = targetCommand.Substring(1, endQuote - 1);
                        arguments = targetCommand[(endQuote + 1)..].Trim();
                    }
                    else
                    {
                        fileName = targetCommand.Trim('"');
                    }
                }
                else
                {
                    int firstSpace = targetCommand.IndexOf(' ');
                    if (firstSpace > 0)
                    {
                        fileName = targetCommand[..firstSpace];
                        arguments = targetCommand[(firstSpace + 1)..].Trim();
                    }
                    else
                    {
                        fileName = targetCommand;
                    }
                }

                var procInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = true,
                    // Per-machine (HKLM) entries can only be written by administrators, so they may elevate.
                    // Per-user (HKCU) entries run as the user, like Windows Settings does.
                    Verb = app.IsPerUserEntry ? string.Empty : "runas"
                };

                using var uninstallerProcess = Process.Start(procInfo);
                if (uninstallerProcess == null)
                {
                    app.Status = "Failed: uninstaller did not start";
                    return false;
                }
                uninstallerProcess.WaitForExit();

                int exitCode = uninstallerProcess.ExitCode;
                if (!SuccessExitCodes.Contains(exitCode))
                {
                    app.Status = exitCode is 1602 or 1223
                        ? "Cancelled by user"
                        : $"Failed: uninstaller exited with code {exitCode}";
                    return false;
                }

                // Some uninstallers hand off to a copy of themselves and exit immediately. Only report success
                // once the uninstall entry is really gone, so leftovers are never offered for a live install.
                if (!WaitUntilUnregistered(app, TimeSpan.FromSeconds(10)))
                {
                    app.Status = "Uninstaller finished, but the app is still registered (it may still be running).";
                    return false;
                }

                app.Status = "Uninstalled";
                return true;
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                app.Status = "Cancelled: administrator prompt was declined";
                return false;
            }
            catch (Exception ex)
            {
                app.Status = $"Failed: {ex.Message}";
                return false;
            }
            finally
            {
                app.IsBusy = false;
            }
        }, ct);
    }

    public async Task<List<LeftoverFolderModel>> ScanLeftoversAsync(AppItemModel app, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var leftovers = new List<LeftoverFolderModel>();

            // DisplayName and Publisher come from the registry (HKCU is writable by any user program).
            // Only plain folder names are used, so values like "..\..\Windows" or "C:\Data" cannot
            // steer the candidate paths.
            if (!IsSafeFolderName(app.DisplayName)) return leftovers;

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

            var candidatePaths = new List<string>
            {
                Path.Combine(localAppData, app.DisplayName),
                Path.Combine(appData, app.DisplayName),
                Path.Combine(programData, app.DisplayName)
            };

            // Microsoft's own folders hold Windows data (credentials, DPAPI keys, ...); never look inside them.
            if (IsSafeFolderName(app.Publisher) &&
                !app.Publisher.StartsWith("Unknown", StringComparison.OrdinalIgnoreCase) &&
                !app.Publisher.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase))
            {
                candidatePaths.Add(Path.Combine(localAppData, app.Publisher, app.DisplayName));
                candidatePaths.Add(Path.Combine(appData, app.Publisher, app.DisplayName));
                candidatePaths.Add(Path.Combine(programData, app.Publisher, app.DisplayName));
            }

            bool stillRegistered = IsStillRegistered(app);

            // The install folder is only a leftover once the app is really gone. Store apps are managed by Windows,
            // and per-user entries may only point inside the user's own AppData, not at machine-wide folders.
            if (!stillRegistered && !app.IsPackagedApp &&
                !string.IsNullOrWhiteSpace(app.InstallLocation) && Directory.Exists(app.InstallLocation) &&
                (!app.IsPerUserEntry || IsInsideUserAppData(app.InstallLocation)))
            {
                candidatePaths.Add(app.InstallLocation);
            }

            foreach (var path in candidatePaths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (IsPathSafeForLeftoverTarget(path) && Directory.Exists(path) &&
                    !SafeFileOps.IsReparsePoint(path) && !IsUsedByAnotherInstalledApp(path, app))
                {
                    long size = CalculateDirectorySize(path, 10);
                    int fileCount = CountDirectoryFiles(path);

                    leftovers.Add(new LeftoverFolderModel
                    {
                        FolderPath = path,
                        SizeBytes = size,
                        FormattedSize = FormatBytes(size),
                        FileCount = fileCount,
                        // Never pre-select anything while the app is still installed: these folders are live data.
                        IsSelected = !stillRegistered,
                        TargetCategory = GetFolderCategoryName(path)
                    });
                }
            }

            return leftovers;
        }, ct);
    }

    public async Task<bool> DeleteLeftoversAsync(IEnumerable<LeftoverFolderModel> leftovers, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            bool anyFailed = false;

            foreach (var item in leftovers.Where(l => l.IsSelected))
            {
                if (!IsPathSafeForLeftoverTarget(item.FolderPath) || SafeFileOps.IsReparsePoint(item.FolderPath)) continue;

                try
                {
                    if (Directory.Exists(item.FolderPath))
                    {
                        Directory.Delete(item.FolderPath, true);
                    }
                }
                catch
                {
                    anyFailed = true;
                }
            }

            return !anyFailed;
        }, ct);
    }

    private static bool IsPathSafeForLeftoverTarget(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        string normalized = Path.GetFullPath(path).TrimEnd('\\');
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData).TrimEnd('\\');
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData).TrimEnd('\\');
        string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData).TrimEnd('\\');
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles).TrimEnd('\\');
        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86).TrimEnd('\\');
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\');

        // Never allow root paths or system directories themselves
        if (normalized.Equals(localAppData, StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals(appData, StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals(programData, StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals(programFiles, StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals(programFilesX86, StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals(windows, StringComparison.OrdinalIgnoreCase) ||
            normalized.Length <= 3)
        {
            return false;
        }

        // Must be a child directory of AppData, LocalAppData, ProgramData, or Program Files,
        // and the first folder below that root must not be a shared one (Common Files, Microsoft, ...).
        foreach (var root in new[] { localAppData, appData, programData, programFiles, programFilesX86 })
        {
            if (string.IsNullOrEmpty(root) || !normalized.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase))
                continue;

            string firstFolder = normalized[(root.Length + 1)..].Split('\\')[0];
            return !SharedFolderNames.Contains(firstFolder) &&
                   !firstFolder.StartsWith("Windows", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool IsSafeFolderName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        string trimmed = name.Trim();
        return trimmed == name &&
               trimmed is not "." and not ".." &&
               trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
               !SharedFolderNames.Contains(trimmed);
    }

    private static bool IsInsideUserAppData(string path)
    {
        try
        {
            string normalized = Path.GetFullPath(path).TrimEnd('\\');
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData).TrimEnd('\\');
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData).TrimEnd('\\');
            return normalized.StartsWith(localAppData + "\\", StringComparison.OrdinalIgnoreCase) ||
                   normalized.StartsWith(appData + "\\", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private bool IsUsedByAnotherInstalledApp(string path, AppItemModel app)
    {
        string normalized;
        try { normalized = Path.GetFullPath(path).TrimEnd('\\'); } catch { return true; }

        foreach (var other in _lastInventory)
        {
            if (ReferenceEquals(other, app) || string.IsNullOrWhiteSpace(other.InstallLocation)) continue;
            if (other.DisplayName == app.DisplayName && other.DisplayVersion == app.DisplayVersion) continue;

            string otherLocation;
            try { otherLocation = Path.GetFullPath(other.InstallLocation).TrimEnd('\\'); } catch { continue; }

            // Same folder, or one contains the other (e.g. several Office components share one folder).
            if (otherLocation.Equals(normalized, StringComparison.OrdinalIgnoreCase) ||
                otherLocation.StartsWith(normalized + "\\", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith(otherLocation + "\\", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsStillRegistered(AppItemModel app)
    {
        if (app.RegistryHive is not { } hive || string.IsNullOrWhiteSpace(app.RegistryKeyPath))
            return false; // no registry entry to check (Store apps, or entries created in tests)

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, app.RegistryView);
            using var key = baseKey.OpenSubKey(app.RegistryKeyPath);
            return key != null;
        }
        catch
        {
            return true; // cannot verify: assume it is still installed
        }
    }

    private static bool WaitUntilUnregistered(AppItemModel app, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (IsStillRegistered(app))
        {
            if (sw.Elapsed >= timeout) return false;
            Thread.Sleep(500);
        }
        return true;
    }

    private static string GetFolderCategoryName(string path)
    {
        if (path.Contains("Local", StringComparison.OrdinalIgnoreCase)) return "Local AppData Cache";
        if (path.Contains("Roaming", StringComparison.OrdinalIgnoreCase)) return "Roaming User Profile";
        if (path.Contains("ProgramData", StringComparison.OrdinalIgnoreCase)) return "Shared Machine Data";
        return "Installation Residue";
    }

    private static long CalculateDirectorySize(string path, int maxDepth)
    {
        long total = 0;
        try
        {
            var dirInfo = new DirectoryInfo(path);
            foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                try { total += file.Length; } catch { }
            }
        }
        catch { }
        return total;
    }

    private static int CountDirectoryFiles(string path)
    {
        try
        {
            return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Count();
        }
        catch
        {
            return 0;
        }
    }

    private static string CleanPublisher(string raw)
    {
        if (raw.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
        {
            var match = Regex.Match(raw, @"CN=([^,]+)");
            if (match.Success) return match.Groups[1].Value.Trim();
        }
        return raw;
    }

    private static string CleanIconPath(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        string clean = raw.Trim('\"');
        int commaIndex = clean.IndexOf(',');
        if (commaIndex > 0)
        {
            clean = clean[..commaIndex];
        }
        return File.Exists(clean) ? clean : "";
    }

    private static string ParseInstallDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "Unknown";
        if (raw.Length == 8 && int.TryParse(raw, out _))
        {
            if (DateTime.TryParseExact(raw, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                return dt.ToString("yyyy-MM-dd");
            }
        }
        return raw;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0.00 B";
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        int index = 0;
        double size = bytes;
        while (size >= 1024 && index < suffixes.Length - 1)
        {
            size /= 1024;
            index++;
        }
        return $"{size:F2} {suffixes[index]}";
    }
}
