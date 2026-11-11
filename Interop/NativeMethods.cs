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
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace LuminaCleaner.Interop;

public static class NativeMethods
{
    // Default packing: 24 bytes on x64 as Windows expects. Pack = 4 made it 20 bytes and
    // SHQueryRecycleBin returned E_INVALIDARG, so the Recycle Bin was always reported as empty.
    [StructLayout(LayoutKind.Sequential)]
    public struct SHQUERYRBINFO
    {
        public int cbSize;
        public long i64Size;
        public long i64NumItems;
    }

    public const uint SHERB_NOCONFIRMATION = 0x00000001;
    public const uint SHERB_NOPROGRESSUI   = 0x00000002;
    public const uint SHERB_NOSOUND        = 0x00000004;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public class MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;

        public MEMORYSTATUSEX()
        {
            dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetSystemTimes(
        out System.Runtime.InteropServices.ComTypes.FILETIME lpIdleTime,
        out System.Runtime.InteropServices.ComTypes.FILETIME lpKernelTime,
        out System.Runtime.InteropServices.ComTypes.FILETIME lpUserTime);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string? lpszProgressTitle;
    }

    public const uint FO_DELETE = 0x0003;
    public const ushort FOF_ALLOWUNDO = 0x0040;
    public const ushort FOF_NOCONFIRMATION = 0x0010;
    public const ushort FOF_SILENT = 0x0004;
    public const ushort FOF_NOERRORUI = 0x0400;
    public const ushort FOF_WANTNUKEWARNING = 0x4000;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

    [DllImport("user32.dll")]
    public static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll")]
    public static extern bool EmptyClipboard();

    [DllImport("user32.dll")]
    public static extern bool CloseClipboard();

    /// <summary>
    /// Queries the total size and item count of the Recycle Bin across all fixed drives.
    /// </summary>
    public static bool QueryRecycleBinTotal(out long totalSizeBytes, out long totalItemCount)
    {
        totalSizeBytes = 0;
        totalItemCount = 0;

        try
        {
            DriveInfo[] drives = DriveInfo.GetDrives();
            foreach (var drive in drives)
            {
                if (drive.DriveType != DriveType.Fixed || !drive.IsReady)
                    continue;

                var info = new SHQUERYRBINFO { cbSize = Marshal.SizeOf<SHQUERYRBINFO>() };
                int hr = SHQueryRecycleBin(drive.Name, ref info);
                if (hr == 0) // S_OK
                {
                    totalSizeBytes += info.i64Size;
                    totalItemCount += info.i64NumItems;
                }
            }
            return true;
        }
        catch
        {
            // Fallback to global query with null root path
            try
            {
                var info = new SHQUERYRBINFO { cbSize = Marshal.SizeOf<SHQUERYRBINFO>() };
                int hr = SHQueryRecycleBin(null, ref info);
                if (hr == 0)
                {
                    totalSizeBytes = info.i64Size;
                    totalItemCount = info.i64NumItems;
                    return true;
                }
            }
            catch
            {
                // Suppress interop exception
            }
        }

        return false;
    }

    /// <summary>
    /// Safely empties the Recycle Bin using the native Shell32 API without UI popups.
    /// </summary>
    public static bool EmptyRecycleBinSafely(IntPtr hwnd = default)
    {
        try
        {
            uint flags = SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND;
            int hr = SHEmptyRecycleBin(hwnd, null, flags);
            return hr == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks whether the current process is executing with elevated Administrator rights.
    /// </summary>
    public static bool IsRunningAsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Tests whether a file is currently open and locked by another process.
    /// Suppresses exceptions and returns true if locked.
    /// </summary>
    public static bool IsFileLocked(FileInfo file)
    {
        if (!file.Exists) return false;

        FileStream? stream = null;
        try
        {
            stream = file.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
        catch
        {
            return true;
        }
        finally
        {
            stream?.Dispose();
        }
    }

    /// <summary>
    /// Moves a file or directory to the Windows Recycle Bin (with undo support).
    /// Network shares and removable drives have no Recycle Bin: the Shell would delete
    /// permanently without asking, so those paths are refused. With
    /// <paramref name="warnIfPermanent"/> (interactive use only) Windows asks before
    /// permanently deleting an item that does not fit in the Recycle Bin.
    /// </summary>
    public static bool SendToRecycleBin(string path, bool warnIfPermanent = false)
    {
        if (string.IsNullOrWhiteSpace(path) || (!File.Exists(path) && !Directory.Exists(path)))
            return false;

        if (!IsOnFixedLocalDrive(path))
            return false;

        try
        {
            ushort flags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI;
            if (warnIfPermanent)
                flags |= FOF_WANTNUKEWARNING;

            var fileOp = new SHFILEOPSTRUCT
            {
                wFunc = FO_DELETE,
                pFrom = Path.GetFullPath(path) + '\0' + '\0', // Must be double-null terminated
                fFlags = flags
            };
            int result = SHFileOperation(ref fileOp);
            return result == 0 && !fileOp.fAnyOperationsAborted;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsOnFixedLocalDrive(string path)
    {
        try
        {
            string full = Path.GetFullPath(path);
            if (full.StartsWith(@"\\", StringComparison.Ordinal))
                return false; // UNC path: network share
            string? root = Path.GetPathRoot(full);
            return root != null && new DriveInfo(root).DriveType == DriveType.Fixed;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Queries the current system memory status safely using GlobalMemoryStatusEx.
    /// </summary>
    public static bool GetMemoryStatus(out ulong totalBytes, out ulong availableBytes, out uint loadPercent)
    {
        totalBytes = 0;
        availableBytes = 0;
        loadPercent = 0;

        try
        {
            var memStatus = new MEMORYSTATUSEX();
            if (GlobalMemoryStatusEx(memStatus))
            {
                totalBytes = memStatus.ullTotalPhys;
                availableBytes = memStatus.ullAvailPhys;
                loadPercent = memStatus.dwMemoryLoad;
                return true;
            }
        }
        catch { }
        return false;
    }

    /// <summary>
    /// Clears the Windows clipboard buffer safely.
    /// </summary>
    public static bool ClearSystemClipboard()
    {
        try
        {
            if (OpenClipboard(IntPtr.Zero))
            {
                EmptyClipboard();
                CloseClipboard();
                return true;
            }
        }
        catch { }
        return false;
    }
}
