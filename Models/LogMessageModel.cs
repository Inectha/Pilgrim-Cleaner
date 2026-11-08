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

namespace LuminaCleaner.Models;

public class LogMessageModel
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public LogLevel Level { get; init; } = LogLevel.Info;
    public required string Tag { get; init; }
    public required string Message { get; init; }
    public string? Details { get; init; }

    public string FormattedTimestamp
    {
        get => Timestamp.ToString("HH:mm:ss.fff");
        set { }
    }

    public string DisplayText => string.IsNullOrWhiteSpace(Details)
        ? $"[{FormattedTimestamp}] {Tag,-18} {Message}"
        : $"[{FormattedTimestamp}] {Tag,-18} {Message} - {Details}";

    public string AccentColor => Level switch
    {
        LogLevel.Success => "#00FF9D", // Radiant Emerald
        LogLevel.Warning => "#FFB800", // Amber Warning
        LogLevel.Error   => "#FF4D4D", // Ruby Crimson
        LogLevel.Skipped => "#00E5FF", // Pilgrim Cyan
        _                => "#94A3B8"  // Slate White
    };
}
