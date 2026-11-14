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
using System.Diagnostics;
using System.IO;
using System.Text;
using LuminaCleaner.Interop;
using LuminaCleaner.Models;
using Microsoft.Win32;

namespace LuminaCleaner.Services;

public class SchedulerService : ISchedulerService
{
    private const string LegacyTaskName = "LuminaSuiteAutoMaintenance";

    private static void RemoveLegacyTaskIfPresent()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = SystemTools.Schtasks,
                Arguments = $"/delete /tn \"{LegacyTaskName}\" /f",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(2000);
        }
        catch { }
    }

    public bool IsTaskScheduled(string taskName = "PilgrimCleanerAutoMaintenance")
    {
        try
        {
            RemoveLegacyTaskIfPresent();
            var psi = new ProcessStartInfo
            {
                FileName = SystemTools.Schtasks,
                Arguments = $"/query /tn \"{taskName}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(3000);
            return proc?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public bool RegisterWeeklyMaintenanceTask(string exePath, string taskName = "PilgrimCleanerAutoMaintenance")
    {
        return RegisterCustomMaintenanceTask(exePath, "WEEKLY", "SUN", "02:00", taskName);
    }

    public bool RegisterCustomMaintenanceTask(
        string exePath,
        string scheduleType,
        string dayOfWeek,
        string time,
        string taskName = "PilgrimCleanerAutoMaintenance")
    {
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            return false;

        try
        {
            string sched = scheduleType.ToUpperInvariant().Trim();
            string day = dayOfWeek.ToUpperInvariant().Trim();
            if (day.Length > 3) day = day[..3]; // e.g. "Sunday" -> "SUN"

            string arguments;
            if (sched == "DAILY")
            {
                arguments = $"/create /tn \"{taskName}\" /tr \"\\\"{exePath}\\\" --silent-audit\" /sc daily /st {time} /f";
            }
            else
            {
                arguments = $"/create /tn \"{taskName}\" /tr \"\\\"{exePath}\\\" --silent-audit\" /sc weekly /d {day} /st {time} /f";
            }

            var psi = new ProcessStartInfo
            {
                FileName = SystemTools.Schtasks,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(5000);
            return proc?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public bool RemoveMaintenanceTask(string taskName = "PilgrimCleanerAutoMaintenance")
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = SystemTools.Schtasks,
                Arguments = $"/delete /tn \"{taskName}\" /f",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(5000);
            if (proc?.ExitCode == 0 || !IsTaskScheduled(taskName))
            {
                return true;
            }

            // Fallback via PowerShell
            var psPsi = new ProcessStartInfo
            {
                FileName = SystemTools.PowerShell,
                Arguments = $"-NoProfile -Command \"Unregister-ScheduledTask -TaskName '{taskName}' -Confirm:$false -ErrorAction SilentlyContinue\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var psProc = Process.Start(psPsi);
            psProc?.WaitForExit(5000);

            return !IsTaskScheduled(taskName);
        }
        catch
        {
            return false;
        }
    }

    public ScheduledTaskStatusModel GetTaskStatusInfo(string taskName = "PilgrimCleanerAutoMaintenance")
    {
        var model = new ScheduledTaskStatusModel
        {
            TaskPath = $"\\{taskName}"
        };
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = SystemTools.Schtasks,
                Arguments = $"/query /tn \"{taskName}\" /v /fo list",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var proc = Process.Start(psi);
            if (proc != null)
            {
                string output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(3000);

                if (proc.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    model.IsScheduled = true;
                    string day = "SUN";
                    string schedType = "Weekly";
                    string startTime = "02:00:00";

                    using var reader = new StringReader(output);
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        int colon = line.IndexOf(':');
                        if (colon <= 0) continue;

                        string key = line[..colon].Trim();
                        string val = line[(colon + 1)..].Trim();

                        if (key.Equals("Next Run Time", StringComparison.OrdinalIgnoreCase))
                        {
                            model.NextRunTime = val;
                        }
                        else if (key.Equals("Last Run Time", StringComparison.OrdinalIgnoreCase))
                        {
                            model.LastRunTime = (val.Contains("1999") || val.Contains("N/A")) ? "Never (Awaiting first execution)" : val;
                        }
                        else if (key.Equals("Status", StringComparison.OrdinalIgnoreCase))
                        {
                            model.Status = val;
                        }
                        else if (key.Equals("Last Result", StringComparison.OrdinalIgnoreCase))
                        {
                            model.LastResult = (val == "0") ? "0 (Successful)" : val;
                        }
                        else if (key.Equals("Schedule Type", StringComparison.OrdinalIgnoreCase))
                        {
                            schedType = val;
                        }
                        else if (key.Equals("Days", StringComparison.OrdinalIgnoreCase))
                        {
                            day = val;
                        }
                        else if (key.Equals("Start Time", StringComparison.OrdinalIgnoreCase))
                        {
                            startTime = val;
                        }
                    }

                    model.ScheduleDetails = $"{schedType} on {day} at {startTime}";
                }
            }
        }
        catch { }

        return model;
    }

    public bool RunTaskImmediately(string taskName = "PilgrimCleanerAutoMaintenance")
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = SystemTools.Schtasks,
                Arguments = $"/run /tn \"{taskName}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(3000);
            return proc?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public void OpenWindowsTaskScheduler()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = SystemTools.TaskScheduler,
                UseShellExecute = true
            });
        }
        catch { }
    }

    public const string ToastAppUserModelId = "PilgrimSoftware.PilgrimCleaner";

    public void ShowWindowsNotification(string title, string message)
    {
        try
        {
            EnsureToastAppUserModelIdRegistered();

            string script = BuildToastNotificationScript(title, message, ToastAppUserModelId);
            var psi = new ProcessStartInfo
            {
                FileName = SystemTools.PowerShell,
                Arguments = "-NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -EncodedCommand " +
                            Convert.ToBase64String(Encoding.Unicode.GetBytes(script)),
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Toast notification dispatch failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Registers an AppUserModelId under HKCU\Software\Classes\AppUserModelId so Windows 10/11
    /// Action Center displays native toast notifications for an unpackaged Win32/WPF app.
    /// </summary>
    private static void EnsureToastAppUserModelIdRegistered()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.CreateSubKey(
                $@"Software\Classes\AppUserModelId\{ToastAppUserModelId}");
            if (key is null) return;

            key.SetValue("DisplayName", "Pilgrim Cleaner", RegistryValueKind.String);

            string iconPng = Path.Combine(AppContext.BaseDirectory, "Assets", "PilgrimIconP.png");
            if (File.Exists(iconPng))
            {
                key.SetValue("IconUri", iconPng, RegistryValueKind.String);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to register AppUserModelId for toast notifications: {ex.Message}");
        }
    }

    /// <summary>
    /// Builds an injection-safe PowerShell script for displaying a Windows 10/11 toast notification.
    /// External strings are passed exclusively as Base64 literals.
    /// </summary>
    public static string BuildToastNotificationScript(string title, string message, string appUserModelId)
    {
        string titleB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(title ?? string.Empty));
        string msgB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(message ?? string.Empty));
        string aumidB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(appUserModelId ?? ToastAppUserModelId));

        return $@"
$title = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String('{titleB64}'))
$msg = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String('{msgB64}'))
$aumid = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String('{aumidB64}'))

[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null
[Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime] | Out-Null

$template = [Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent([Windows.UI.Notifications.ToastTemplateType]::ToastText02)
$textNodes = $template.GetElementsByTagName('text')
$textNodes.Item(0).AppendChild($template.CreateTextNode($title)) | Out-Null
$textNodes.Item(1).AppendChild($template.CreateTextNode($msg)) | Out-Null

$toast = [Windows.UI.Notifications.ToastNotification]::new($template)
[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier($aumid).Show($toast)
";
    }
}
