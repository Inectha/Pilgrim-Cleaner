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
using System.IO;

namespace LuminaCleaner.Interop;

/// <summary>
/// Absolute paths of the Windows tools Pilgrim Cleaner starts.
///
/// Starting "powershell.exe" by name makes Windows search the application folder and the
/// current directory before System32. When the CLI runs as SYSTEM from a folder a user can
/// write to (e.g. an RMM job in C:\Windows\Temp), a planted powershell.exe would run as SYSTEM.
/// </summary>
public static class SystemTools
{
    public static string PowerShell => Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe");
    public static string Schtasks => Path.Combine(Environment.SystemDirectory, "schtasks.exe");
    public static string Dism => Path.Combine(Environment.SystemDirectory, "dism.exe");
    public static string TaskScheduler => Path.Combine(Environment.SystemDirectory, "taskschd.msc");
    public static string Explorer => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
}
