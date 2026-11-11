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
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LuminaCleaner.Models;
using LuminaCleaner.Services;

namespace LuminaCleaner.ViewModels;

public partial class SchedulerViewModel : ObservableObject
{
    private readonly ISchedulerService _schedulerService;

    [ObservableProperty]
    private bool _isScheduleActive;

    [ObservableProperty]
    private string _selectedFrequency = "Weekly"; // "Weekly" or "Daily"

    [ObservableProperty]
    private string _selectedDay = "Sunday"; // "Sunday", "Monday", "Wednesday", "Friday", "Saturday"

    [ObservableProperty]
    private string _selectedTime = "02:00"; // "02:00", "08:00", "12:00", "18:00", "22:00"

    [ObservableProperty]
    private double _notificationThresholdGB = 1.0;

    [ObservableProperty]
    private string _statusMessage = "Automatic maintenance is currently inactive.";

    [ObservableProperty]
    private ScheduledTaskStatusModel _taskInfo = new();

    public SchedulerViewModel(ISchedulerService? schedulerService = null)
    {
        _schedulerService = schedulerService ?? new SchedulerService();
        RefreshStatus();
    }

    [RelayCommand]
    public void RefreshStatus()
    {
        IsScheduleActive = _schedulerService.IsTaskScheduled();
        TaskInfo = _schedulerService.GetTaskStatusInfo();

        if (IsScheduleActive)
        {
            StatusMessage = $"Windows Task Active: Next run on {TaskInfo.NextRunTime}";
        }
        else
        {
            StatusMessage = "Automatic maintenance is currently inactive.";
        }
    }

    [RelayCommand]
    public void ToggleSchedule()
    {
        string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;

        if (IsScheduleActive)
        {
            // Turn OFF
            bool removed = _schedulerService.RemoveMaintenanceTask();
            if (removed || !_schedulerService.IsTaskScheduled())
            {
                IsScheduleActive = false;
                StatusMessage = "Automatic maintenance task successfully disabled in Windows.";
                TaskInfo = _schedulerService.GetTaskStatusInfo();
            }
            else
            {
                StatusMessage = "Could not disable task. Administrator privileges may be required.";
            }
        }
        else
        {
            // Turn ON with selected settings
            string dayCode = MapDayToCode(SelectedDay);
            if (_schedulerService.RegisterCustomMaintenanceTask(exePath, SelectedFrequency, dayCode, SelectedTime))
            {
                IsScheduleActive = true;
                StatusMessage = $"Scheduled {SelectedFrequency} maintenance ({SelectedDay} at {SelectedTime}).";
                TaskInfo = _schedulerService.GetTaskStatusInfo();
            }
            else
            {
                StatusMessage = "Task registration requires administrator rights or valid execution path.";
            }
        }
    }

    [RelayCommand]
    public void ApplyScheduleChanges()
    {
        string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
        string dayCode = MapDayToCode(SelectedDay);

        if (_schedulerService.RegisterCustomMaintenanceTask(exePath, SelectedFrequency, dayCode, SelectedTime))
        {
            IsScheduleActive = true;
            StatusMessage = $"Updated Windows Schedule: {SelectedFrequency} on {SelectedDay} at {SelectedTime}.";
            TaskInfo = _schedulerService.GetTaskStatusInfo();
        }
        else
        {
            StatusMessage = "Could not update task settings. Administrator rights may be required.";
        }
    }

    [RelayCommand]
    public void RunTaskNow()
    {
        if (!_schedulerService.IsTaskScheduled())
        {
            StatusMessage = "Task is not currently scheduled. Activate automation first.";
            return;
        }

        if (_schedulerService.RunTaskImmediately())
        {
            StatusMessage = "Dispatched task in background via Windows Task Scheduler. Inspecting storage...";
        }
        else
        {
            StatusMessage = "Could not trigger task execution.";
        }
    }

    [RelayCommand]
    public void OpenTaskScheduler()
    {
        _schedulerService.OpenWindowsTaskScheduler();
        StatusMessage = "Opened Windows Task Scheduler (taskschd.msc).";
    }

    [RelayCommand]
    public void SendTestToast()
    {
        _schedulerService.ShowWindowsNotification(
            "Pilgrim Cleaner - System Storage Alert",
            $"Automatic audit detected 2.84 GB of safe recoverable cache files (> {NotificationThresholdGB:N1} GB threshold).");
        StatusMessage = "Dispatched native Windows 11 Toast Notification.";
    }

    private static string MapDayToCode(string day)
    {
        return day.ToUpperInvariant() switch
        {
            "SUNDAY" => "SUN",
            "MONDAY" => "MON",
            "TUESDAY" => "TUE",
            "WEDNESDAY" => "WED",
            "THURSDAY" => "THU",
            "FRIDAY" => "FRI",
            "SATURDAY" => "SAT",
            _ => "SUN"
        };
    }
}
