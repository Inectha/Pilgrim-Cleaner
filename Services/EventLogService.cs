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

namespace LuminaCleaner.Services;

/// <summary>
/// Writes structured audit and cleanup events to the native Windows Application Event Log
/// for integration with enterprise RMM agents (NinjaOne, Datto, SCOM, Zabbix, Windows Event Forwarding).
/// </summary>
public static class EventLogService
{
    public const string SourceName = "PilgrimCleaner";
    public const string LogName = "Application";

    public const int EventIdAuditSuccess = 1000;
    public const int EventIdCleanSuccess = 1001;
    public const int EventIdThresholdWarning = 1002;
    public const int EventIdExecutionError = 1003;

    public static bool WriteEvent(int eventId, EventLogEntryType entryType, string message)
    {
        try
        {
            string sourceToUse = SourceName;
            try
            {
                if (!EventLog.SourceExists(SourceName))
                {
                    EventLog.CreateEventSource(SourceName, LogName);
                }
            }
            catch
            {
                // Standard user cannot create a new EventSource in HKLM\SYSTEM\CurrentControlSet\Services\EventLog;
                // fall back to built-in "Application" source so event logging still succeeds.
                sourceToUse = "Application";
            }

            EventLog.WriteEntry(sourceToUse, $"[Pilgrim Cleaner] {message}", entryType, eventId);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
