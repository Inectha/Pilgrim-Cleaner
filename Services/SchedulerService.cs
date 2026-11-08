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
using LuminaCleaner.Models;

namespace LuminaCleaner.Services;

public class SchedulerService : ISchedulerService
{
    public bool IsTaskScheduled(string taskName = "PilgrimCleanerAutoMaintenance")
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
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
                FileName = "schtasks.exe",
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
                FileName = "schtasks.exe",
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
                FileName = "powershell.exe",
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
        var model = new ScheduledTaskStatusModel();
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
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
                FileName = "schtasks.exe",
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
                FileName = "taskschd.msc",
                UseShellExecute = true
            });
        }
        catch { }
    }

    public void ShowWindowsNotification(string title, string message)
    {
        try
        {
            // Windows 11 native toast notification via powershell script
            string escapedTitle = title.Replace("\"", "\\\"");
            string escapedMsg = message.Replace("\"", "\\\"");
            string script = $"[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] > $null; " +
                            $"$template = [Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent([Windows.UI.Notifications.ToastTemplateType]::ToastText02); " +
                            $"$textNodes = $template.GetElementsByTagName('text'); " +
                            $"$textNodes.Item(0).AppendChild($template.CreateTextNode('{escapedTitle}')) > $null; " +
                            $"$textNodes.Item(1).AppendChild($template.CreateTextNode('{escapedMsg}')) > $null; " +
                            $"$toast = [Windows.UI.Notifications.ToastNotification]::new($template); " +
                            $"[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('Pilgrim Cleaner').Show($toast);";

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{script}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process.Start(psi);
        }
        catch { }
    }
}
