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
using System.Threading;
using System.Threading.Tasks;
using LuminaCleaner.Models;

namespace LuminaCleaner.Services;

public interface IDuplicatesService
{
    Task<IReadOnlyList<DuplicateGroupModel>> FindDuplicatesAsync(IEnumerable<string> rootPaths, IProgress<int>? progress, CancellationToken ct);
    bool DeleteFileToRecycleBin(string path, bool interactive = false);

    /// <summary>Recycles <paramref name="copyPath"/> only if <paramref name="keptPath"/> is a different, existing file of the same size.</summary>
    bool RecycleDuplicate(string copyPath, string keptPath, bool interactive = false);
}
