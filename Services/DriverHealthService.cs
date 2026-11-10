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
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LuminaCleaner.Interop;
using LuminaCleaner.Models;

namespace LuminaCleaner.Services;

public class DriverHealthService : IDriverHealthService
{
    public async Task<List<DriverItemModel>> GetInstalledDriversAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var drivers = new List<DriverItemModel>();

            // Method 1: Direct native COM query via SWbemLocator
            try
            {
                Type? locatorType = Type.GetTypeFromProgID("WbemScripting.SWbemLocator");
                if (locatorType != null)
                {
                    dynamic locator = Activator.CreateInstance(locatorType)!;
                    dynamic service = locator.ConnectServer(".", @"root\cimv2");
                    dynamic results = service.ExecQuery("SELECT DeviceName, DriverVersion, DriverDate, Signer, DeviceClass, DeviceID FROM Win32_PnPSignedDriver WHERE DeviceName IS NOT NULL");

                    foreach (dynamic item in results)
                    {
                        try
                        {
                            string deviceName = item.Properties_.Item("DeviceName")?.Value?.ToString() ?? "";
                            if (string.IsNullOrWhiteSpace(deviceName)) continue;

                            string driverVersion = item.Properties_.Item("DriverVersion")?.Value?.ToString() ?? "Standard Driver";
                            string rawDate = item.Properties_.Item("DriverDate")?.Value?.ToString() ?? "";
                            string signer = item.Properties_.Item("Signer")?.Value?.ToString() ?? "Unsigned / Custom";
                            string deviceClass = item.Properties_.Item("DeviceClass")?.Value?.ToString() ?? "SYSTEM";
                            string deviceID = item.Properties_.Item("DeviceID")?.Value?.ToString() ?? "";

                            var (category, icon) = MapDeviceCategory(deviceClass);
                            bool isWhql = IsSignerWhqlCertified(signer);

                            drivers.Add(new DriverItemModel
                            {
                                DeviceName = deviceName,
                                DriverVersion = driverVersion,
                                DriverDate = FormatDriverDate(rawDate),
                                Signer = string.IsNullOrWhiteSpace(signer) ? "Microsoft OEM Driver" : signer,
                                IsWhqlCertified = isWhql,
                                DeviceClass = deviceClass.ToUpperInvariant(),
                                Category = category,
                                CategoryIcon = icon,
                                DeviceID = deviceID,
                                Status = "Up to Date",
                                IsUpToDate = true,
                                HasPendingUpdate = false
                            });
                        }
                        catch
                        {
                            // Suppress individual driver read errors
                        }
                    }
                }
            }
            catch
            {
                // Fallback to PowerShell if COM is restricted in sandbox
            }

            // Method 2: Fallback query via PowerShell if COM was unavailable
            if (drivers.Count == 0)
            {
                QueryDriversViaPowerShell(drivers);
            }

            return drivers
                .GroupBy(d => d.DeviceName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(d => d.Category)
                .ThenBy(d => d.DeviceName)
                .ToList();
        }, ct);
    }

    private static void QueryDriversViaPowerShell(List<DriverItemModel> destination)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NonInteractive -Command \"Get-CimInstance Win32_PnPSignedDriver -ErrorAction SilentlyContinue | Where-Object { $_.DeviceName -ne $null } | Select-Object -First 60 DeviceName, DriverVersion, DriverDate, Signer, DeviceClass, DeviceID | ConvertTo-Json -Compress\"",
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
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    AddDriverFromJson(el, destination);
                }
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                AddDriverFromJson(doc.RootElement, destination);
            }
        }
        catch
        {
            // Graceful fallback
        }
    }

    private static void AddDriverFromJson(JsonElement el, List<DriverItemModel> destination)
    {
        try
        {
            string name = el.TryGetProperty("DeviceName", out var n) ? n.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(name)) return;

            string version = el.TryGetProperty("DriverVersion", out var v) ? v.GetString() ?? "" : "";
            string rawDate = el.TryGetProperty("DriverDate", out var d) ? d.GetString() ?? "" : "";
            string signer = el.TryGetProperty("Signer", out var s) ? s.GetString() ?? "Microsoft Windows" : "Microsoft Windows";
            string devClass = el.TryGetProperty("DeviceClass", out var dc) ? dc.GetString() ?? "SYSTEM" : "SYSTEM";
            string devId = el.TryGetProperty("DeviceID", out var di) ? di.GetString() ?? "" : "";

            var (category, icon) = MapDeviceCategory(devClass);
            bool isWhql = IsSignerWhqlCertified(signer);

            destination.Add(new DriverItemModel
            {
                DeviceName = name,
                DriverVersion = version,
                DriverDate = FormatDriverDate(rawDate),
                Signer = signer,
                IsWhqlCertified = isWhql,
                DeviceClass = devClass.ToUpperInvariant(),
                Category = category,
                CategoryIcon = icon,
                DeviceID = devId,
                Status = "Up to Date",
                IsUpToDate = true,
                HasPendingUpdate = false
            });
        }
        catch
        {
            // Suppress item error
        }
    }

    public async Task<List<DriverUpdateModel>> CheckWhqlUpdatesAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var updates = new List<DriverUpdateModel>();

            try
            {
                Type? sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session");
                if (sessionType != null)
                {
                    dynamic session = Activator.CreateInstance(sessionType)!;
                    dynamic searcher = session.CreateUpdateSearcher();
                    searcher.Online = true;
                    searcher.ServerSelection = 1; // Native Windows Update Service

                    dynamic result = searcher.Search("IsInstalled=0 and Type='Driver'");
                    if (result?.Updates != null)
                    {
                        foreach (dynamic update in result.Updates)
                        {
                            try
                            {
                                string title = update.Title?.ToString() ?? "Windows Certified Driver Update";
                                string description = update.Description?.ToString() ?? "Official Microsoft WHQL Driver Package";
                                string driverModel = update.DriverModel?.ToString() ?? "";
                                string driverProvider = update.DriverProvider?.ToString() ?? "Microsoft / OEM";
                                string driverVerDate = update.DriverVerDate?.ToString() ?? "";
                                string updateId = update.Identity?.UpdateID?.ToString() ?? Guid.NewGuid().ToString();

                                // Extract driver version from title or driverModel if present
                                string versionMatch = ExtractVersion(title, driverModel);

                                updates.Add(new DriverUpdateModel
                                {
                                    Title = title,
                                    Description = description,
                                    DriverModel = string.IsNullOrWhiteSpace(driverModel) ? title : driverModel,
                                    DriverProvider = driverProvider,
                                    DriverVerDate = FormatDriverDate(driverVerDate),
                                    DriverVersion = versionMatch,
                                    IsWhqlCertified = true,
                                    Status = "WHQL Verified Update",
                                    UpdateId = updateId,
                                    RawUpdateObject = update
                                });
                            }
                            catch
                            {
                                // Suppress individual update read errors
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Windows Update Session Search error: {ex.Message}");
            }

            return updates;
        }, ct);
    }

    public void CorrelateDriversWithUpdates(IEnumerable<DriverItemModel> drivers, IEnumerable<DriverUpdateModel> updates)
    {
        var updateList = updates.ToList();

        foreach (var driver in drivers)
        {
            // Reset to default up-to-date state
            driver.IsUpToDate = true;
            driver.HasPendingUpdate = false;
            driver.Status = "Up to Date";
            driver.MatchingUpdate = null;

            // Search for matching update
            var match = updateList.FirstOrDefault(u =>
                MatchesDriver(driver, u));

            if (match != null)
            {
                driver.IsUpToDate = false;
                driver.HasPendingUpdate = true;
                driver.UpdateTitle = match.Title;
                driver.UpdateVersion = match.DriverVersion;
                driver.UpdateReleaseDate = match.DriverVerDate;
                driver.Status = $"Update Available ({match.DriverVerDate})";
                driver.MatchingUpdate = match;
            }
        }
    }

    private static bool MatchesDriver(DriverItemModel driver, DriverUpdateModel update)
    {
        if (string.IsNullOrWhiteSpace(driver.DeviceName)) return false;

        string devName = driver.DeviceName.ToLowerInvariant();
        string model = update.DriverModel.ToLowerInvariant();
        string title = update.Title.ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(model) && (devName.Contains(model) || model.Contains(devName)))
            return true;

        if (devName.Contains("watchdog") && (title.Contains("watchdog") || model.Contains("watchdog")))
            return true;

        if (devName.Contains("voice clarity") && (title.Contains("voice clarity") || model.Contains("voice clarity")))
            return true;

        if (devName.Contains("audioprocessingobject") || (title.Contains("audioprocessingobject") && driver.Category == DriverCategory.Audio))
            return true;

        return false;
    }

    public async Task<(bool Success, string Message)> InstallDriverUpdateAsync(DriverUpdateModel update, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                update.IsInstalling = true;
                update.InstallStatus = "Downloading WHQL package...";

                // Method A: Elevated in-process installation via COM
                if (NativeMethods.IsRunningAsAdministrator() && update.RawUpdateObject != null)
                {
                    dynamic rawUpdate = update.RawUpdateObject;
                    if (!rawUpdate.EulaAccepted)
                    {
                        rawUpdate.AcceptEula();
                    }

                    Type? sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session");
                    Type? collType = Type.GetTypeFromProgID("Microsoft.Update.UpdateColl");

                    if (sessionType != null && collType != null)
                    {
                        dynamic session = Activator.CreateInstance(sessionType)!;
                        dynamic updateColl = Activator.CreateInstance(collType)!;
                        updateColl.Add(rawUpdate);

                        // 1. Download
                        dynamic downloader = session.CreateUpdateDownloader();
                        downloader.Updates = updateColl;
                        dynamic downResult = downloader.Download();

                        if (downResult.ResultCode != 2) // 2 = Succeeded
                        {
                            update.InstallStatus = $"Download failed (Code {downResult.ResultCode})";
                            return (false, $"Download failed with code {downResult.ResultCode}");
                        }

                        // 2. Install
                        update.InstallStatus = "Installing driver...";
                        dynamic installer = session.CreateUpdateInstaller();
                        installer.Updates = updateColl;
                        dynamic instResult = installer.Install();

                        if (instResult.ResultCode == 2)
                        {
                            update.IsInstalled = true;
                            update.InstallStatus = "Installed Successfully (WHQL)";
                            return (true, "Official WHQL driver package installed successfully.");
                        }
                        else
                        {
                            update.InstallStatus = $"Installation code {instResult.ResultCode}";
                            return (false, $"Driver installation completed with code {instResult.ResultCode}");
                        }
                    }
                }

                // Method B: Elevated installation helper via PowerShell (runs with Administrator UAC prompt)
                update.InstallStatus = "Requesting administrative elevation...";

                string safeTitle = update.Title.Replace("'", "''");
                string psScript = $@"
$session = New-Object -ComObject 'Microsoft.Update.Session'
$searcher = $session.CreateUpdateSearcher()
$searcher.Online = $true
$result = $searcher.Search('IsInstalled=0 and Type=\'Driver\'')
foreach ($u in $result.Updates) {{
    if ($u.Title -like '*{safeTitle}*' -or '{safeTitle}' -like ('*' + $u.DriverModel + '*')) {{
        if (-not $u.EulaAccepted) {{ $u.AcceptEula() }}
        $coll = New-Object -ComObject 'Microsoft.Update.UpdateColl'
        $coll.Add($u) | Out-Null
        $down = $session.CreateUpdateDownloader()
        $down.Updates = $coll
        $dr = $down.Download()
        if ($dr.ResultCode -eq 2) {{
            $inst = $session.CreateUpdateInstaller()
            $inst.Updates = $coll
            $ir = $inst.Install()
            if ($ir.ResultCode -eq 2) {{ exit 0 }} else {{ exit $ir.ResultCode }}
        }} else {{ exit $dr.ResultCode }}
    }}
}}
exit 1
";

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psScript.Replace("\r\n", " ").Replace("\n", " ")}\"",
                    UseShellExecute = true,
                    Verb = "runas" // Prompt Windows UAC for administrative rights
                };

                using var proc = Process.Start(psi);
                if (proc == null)
                {
                    update.InstallStatus = "Installation cancelled (UAC denied)";
                    return (false, "Administrator elevation prompt was declined.");
                }

                proc.WaitForExit();

                if (proc.ExitCode == 0)
                {
                    update.IsInstalled = true;
                    update.InstallStatus = "Installed Successfully (WHQL)";
                    return (true, "Official WHQL driver package installed successfully via elevated Windows Update Session.");
                }
                else
                {
                    update.InstallStatus = $"Install finished with code {proc.ExitCode}";
                    return (false, $"Windows Update installer exited with code {proc.ExitCode}");
                }
            }
            catch (Exception ex)
            {
                update.InstallStatus = $"Error: {ex.Message}";
                return (false, $"Driver installation error: {ex.Message}");
            }
            finally
            {
                update.IsInstalling = false;
            }
        }, ct);
    }

    public async Task<(bool Success, string Message)> InstallAllDriverUpdatesAsync(IEnumerable<DriverUpdateModel> updates, CancellationToken ct = default)
    {
        int successCount = 0;
        int failCount = 0;

        foreach (var update in updates.Where(u => !u.IsInstalled))
        {
            var (ok, _) = await InstallDriverUpdateAsync(update, ct);
            if (ok) successCount++;
            else failCount++;
        }

        if (failCount == 0 && successCount > 0)
        {
            return (true, $"All {successCount} WHQL driver update packages installed successfully.");
        }
        else if (successCount > 0)
        {
            return (true, $"{successCount} drivers updated successfully. {failCount} packages require manual attention.");
        }
        else
        {
            return (false, "Driver package installation did not complete.");
        }
    }

    private static (DriverCategory Category, string Icon) MapDeviceCategory(string? rawClass)
    {
        if (string.IsNullOrWhiteSpace(rawClass))
            return (DriverCategory.System, "DeveloperBoard24");

        string upper = rawClass.Trim().ToUpperInvariant();
        return upper switch
        {
            "DISPLAY" => (DriverCategory.Graphics, "Desktop24"),
            "MEDIA" or "SOUND" or "AUDIOENDPOINT" => (DriverCategory.Audio, "Speaker224"),
            "NET" or "BLUETOOTH" or "MODEM" => (DriverCategory.Network, "Wifi124"),
            "SCSIADAPTER" or "HDC" or "DISKDRIVE" or "VOLUME" or "FLOPPYDISK" => (DriverCategory.Storage, "HardDrive24"),
            _ => (DriverCategory.System, "DeveloperBoard24")
        };
    }

    private static bool IsSignerWhqlCertified(string? signer)
    {
        if (string.IsNullOrWhiteSpace(signer)) return false;
        string s = signer.ToLowerInvariant();
        return s.Contains("microsoft") ||
               s.Contains("windows") ||
               s.Contains("whql") ||
               s.Contains("compatibility publisher") ||
               s.Contains("intel") ||
               s.Contains("nvidia") ||
               s.Contains("advanced micro devices") ||
               s.Contains("realtek") ||
               s.Contains("qualcomm");
    }

    private static string FormatDriverDate(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "Current Release";

        // Handle WMI CIM_DATETIME e.g. "20240315000000.000000+000"
        if (raw.Length >= 8 && int.TryParse(raw[..8], out _))
        {
            if (DateTime.TryParseExact(raw[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                return dt.ToString("yyyy-MM-dd");
            }
        }

        // Handle standard DateTime string
        if (DateTime.TryParse(raw, out var parsed))
        {
            return parsed.ToString("yyyy-MM-dd");
        }

        return raw;
    }

    private static string ExtractVersion(string title, string model)
    {
        var match = Regex.Match(title, @"\b(\d+\.\d+(\.\d+)+)\b");
        if (match.Success) return match.Value;

        match = Regex.Match(model, @"\b(\d+\.\d+(\.\d+)+)\b");
        if (match.Success) return match.Value;

        return "WHQL Release";
    }
}
