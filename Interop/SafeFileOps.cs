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
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace LuminaCleaner.Interop;

/// <summary>
/// File operations that stay safe when Pilgrim Cleaner runs elevated or as SYSTEM.
///
/// Threat: a standard user can create a junction or symbolic link inside a folder
/// they own (their profile, %TEMP%, C:\Windows\Temp) that points somewhere else.
/// A privileged cleaner that follows it would delete files in the target, e.g.
/// another user's profile or a security product's folder.
///
/// Defences:
/// 1. A scan root is only accepted when no part of its path is a reparse point
///    created by a non-privileged account (<see cref="TryResolveTrustedDirectory"/>).
/// 2. A file is deleted through the same handle that was used to verify where it
///    really lives (<see cref="DeleteFileIfAt"/>), so swapping a folder for a
///    junction after the scan cannot redirect the delete.
/// </summary>
public static class SafeFileOps
{
    private const uint DELETE = 0x00010000;
    private const uint READ_CONTROL = 0x00020000;
    private const uint FILE_READ_ATTRIBUTES = 0x0080;
    private const uint FILE_WRITE_ATTRIBUTES = 0x0100;
    private const uint FILE_SHARE_READ_WRITE_DELETE = 0x00000007;
    private const uint OPEN_EXISTING = 3;
    private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
    private const uint FILE_FLAG_OPEN_REPARSE_POINT = 0x00200000;

    private const int ERROR_INVALID_FUNCTION = 1;
    private const int ERROR_FILE_NOT_FOUND = 2;
    private const int ERROR_PATH_NOT_FOUND = 3;
    private const int ERROR_ACCESS_DENIED = 5;
    private const int ERROR_SHARING_VIOLATION = 32;
    private const int ERROR_LOCK_VIOLATION = 33;
    private const int ERROR_NOT_SUPPORTED = 50;
    private const int ERROR_INVALID_PARAMETER = 87;

    private const int FileBasicInfoClass = 0;
    private const int FileDispositionInfoClass = 4;
    private const int FileDispositionInfoExClass = 21;
    private const uint FILE_DISPOSITION_FLAG_DELETE = 0x00000001;
    private const uint FILE_DISPOSITION_FLAG_IGNORE_READONLY_ATTRIBUTE = 0x00000010;
    private const uint FILE_ATTRIBUTE_READONLY = 0x00000001;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;

    private const int SE_FILE_OBJECT = 1;
    private const uint OWNER_SECURITY_INFORMATION = 0x00000001;
    private const string TrustedInstallerSid = "S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464";

    [StructLayout(LayoutKind.Sequential)]
    private struct FILE_BASIC_INFO
    {
        public long CreationTime;
        public long LastAccessTime;
        public long LastWriteTime;
        public long ChangeTime;
        public uint FileAttributes;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes,
        uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandleW(SafeFileHandle hFile, [Out] char[] lpszFilePath, uint cchFilePath, uint dwFlags);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle hFile, int fileInformationClass, ref uint lpFileInformation, uint dwBufferSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle hFile, int fileInformationClass, ref byte lpFileInformation, uint dwBufferSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle hFile, int fileInformationClass, ref FILE_BASIC_INFO lpFileInformation, uint dwBufferSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle hFile, int fileInformationClass, out FILE_BASIC_INFO lpFileInformation, uint dwBufferSize);

    [DllImport("advapi32.dll")]
    private static extern uint GetSecurityInfo(
        SafeFileHandle handle, int objectType, uint securityInfo,
        out IntPtr ppsidOwner, IntPtr ppsidGroup, IntPtr ppDacl, IntPtr ppSacl, out IntPtr ppSecurityDescriptor);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);

    /// <summary>
    /// Checks that <paramref name="directory"/> can be traversed safely and returns its real
    /// (fully resolved) location. Fails when the directory itself, or any parent of it, is a
    /// junction or symbolic link that is not owned by SYSTEM, Administrators or TrustedInstaller.
    /// Reparse points created by those accounts (for example a relocated C:\Users) are allowed.
    /// </summary>
    public static bool TryResolveTrustedDirectory(string directory, out string? finalPath, out string? reason)
    {
        finalPath = null;
        reason = null;

        string full;
        try
        {
            full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        }
        catch (Exception ex)
        {
            reason = $"Invalid path: {ex.Message}";
            return false;
        }

        if (!AllComponentsTrusted(full, out reason))
            return false;

        using (var handle = CreateFileW(ToExtendedPath(full), FILE_READ_ATTRIBUTES, FILE_SHARE_READ_WRITE_DELETE,
                   IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS, IntPtr.Zero))
        {
            if (handle.IsInvalid)
            {
                reason = $"Cannot open directory: {new Win32Exception(Marshal.GetLastWin32Error()).Message}";
                return false;
            }

            finalPath = GetFinalPath(handle);
        }

        if (finalPath == null)
        {
            reason = "Cannot resolve the real location of the directory.";
            return false;
        }

        // Check again, so a parent swapped for a junction while the handle was being opened is caught.
        if (!AllComponentsTrusted(full, out reason))
        {
            finalPath = null;
            return false;
        }

        finalPath = Path.TrimEndingDirectorySeparator(finalPath);
        return true;
    }

    /// <summary>
    /// Deletes <paramref name="fullPath"/> only if it really is the file at
    /// <paramref name="expectedFinalPath"/>. The file is opened without following a final
    /// symbolic link, its real location is read from the open handle and compared, and the
    /// delete is issued on that same handle. Nothing is deleted if the location differs.
    /// </summary>
    public static (bool Success, string? SkipReason) DeleteFileIfAt(string fullPath, string expectedFinalPath)
    {
        string extended = ToExtendedPath(fullPath);
        uint flags = FILE_FLAG_OPEN_REPARSE_POINT;

        var handle = CreateFileW(extended, DELETE | FILE_READ_ATTRIBUTES | FILE_WRITE_ATTRIBUTES,
            FILE_SHARE_READ_WRITE_DELETE, IntPtr.Zero, OPEN_EXISTING, flags, IntPtr.Zero);
        if (handle.IsInvalid && Marshal.GetLastWin32Error() == ERROR_ACCESS_DENIED)
        {
            handle.Dispose();
            // Some ACLs allow deleting but not changing attributes; try again without it.
            handle = CreateFileW(extended, DELETE | FILE_READ_ATTRIBUTES,
                FILE_SHARE_READ_WRITE_DELETE, IntPtr.Zero, OPEN_EXISTING, flags, IntPtr.Zero);
        }

        using (handle)
        {
            if (handle.IsInvalid)
            {
                int openError = Marshal.GetLastWin32Error();
                if (openError is ERROR_FILE_NOT_FOUND or ERROR_PATH_NOT_FOUND)
                    return (true, null); // already gone
                return (false, DescribeError(openError));
            }

            string? actual = GetFinalPath(handle);
            if (actual == null)
                return (false, "Suppressed: could not resolve the real location of the file.");

            if (!string.Equals(actual, expectedFinalPath, StringComparison.OrdinalIgnoreCase))
            {
                return (false, "Skipped: the path now leads to a different location than during the scan " +
                               "(junction or symbolic link redirection). Nothing was deleted.");
            }

            uint dispositionFlags = FILE_DISPOSITION_FLAG_DELETE | FILE_DISPOSITION_FLAG_IGNORE_READONLY_ATTRIBUTE;
            if (SetFileInformationByHandle(handle, FileDispositionInfoExClass, ref dispositionFlags, sizeof(uint)))
                return (true, null);

            int error = Marshal.GetLastWin32Error();
            if (error is ERROR_INVALID_PARAMETER or ERROR_NOT_SUPPORTED or ERROR_INVALID_FUNCTION)
            {
                // Windows before 10 1809 / Server 2019: no FileDispositionInfoEx.
                ClearReadOnlyAttribute(handle);
                byte deleteFile = 1;
                if (SetFileInformationByHandle(handle, FileDispositionInfoClass, ref deleteFile, 1))
                    return (true, null);
                error = Marshal.GetLastWin32Error();
            }

            return (false, DescribeError(error));
        }
    }

    /// <summary>Returns true if the item at <paramref name="path"/> is a junction or symbolic link.</summary>
    public static bool IsReparsePoint(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch
        {
            return true; // cannot inspect: treat as unsafe
        }
    }

    private static bool AllComponentsTrusted(string fullPath, out string? reason)
    {
        reason = null;
        for (string? current = fullPath; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
        {
            FileAttributes attributes;
            try
            {
                attributes = File.GetAttributes(current);
            }
            catch (Exception ex)
            {
                reason = $"Cannot inspect '{current}': {ex.Message}";
                return false;
            }

            if ((attributes & FileAttributes.ReparsePoint) != 0 && !IsOwnedByPrivilegedAccount(current))
            {
                reason = $"'{current}' is a junction or symbolic link that was not created by SYSTEM, " +
                         "Administrators or TrustedInstaller.";
                return false;
            }
        }
        return true;
    }

    private static bool IsOwnedByPrivilegedAccount(string path)
    {
        // Open the reparse point itself (not its target) to read who created it.
        using var handle = CreateFileW(ToExtendedPath(path), READ_CONTROL, FILE_SHARE_READ_WRITE_DELETE, IntPtr.Zero,
            OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT, IntPtr.Zero);
        if (handle.IsInvalid)
            return false;

        uint result = GetSecurityInfo(handle, SE_FILE_OBJECT, OWNER_SECURITY_INFORMATION,
            out IntPtr ownerSid, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, out IntPtr securityDescriptor);
        if (result != 0)
            return false;

        try
        {
            var owner = new SecurityIdentifier(ownerSid);
            return owner.IsWellKnown(WellKnownSidType.LocalSystemSid) ||
                   owner.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid) ||
                   owner.Value == TrustedInstallerSid;
        }
        finally
        {
            LocalFree(securityDescriptor);
        }
    }

    private static void ClearReadOnlyAttribute(SafeFileHandle handle)
    {
        uint size = (uint)Marshal.SizeOf<FILE_BASIC_INFO>();
        if (!GetFileInformationByHandleEx(handle, FileBasicInfoClass, out var info, size))
            return;
        if ((info.FileAttributes & FILE_ATTRIBUTE_READONLY) == 0)
            return;

        // Zero timestamps mean "leave unchanged".
        var update = new FILE_BASIC_INFO { FileAttributes = info.FileAttributes & ~FILE_ATTRIBUTE_READONLY };
        if (update.FileAttributes == 0)
            update.FileAttributes = FILE_ATTRIBUTE_NORMAL;
        SetFileInformationByHandle(handle, FileBasicInfoClass, ref update, size);
    }

    private static string? GetFinalPath(SafeFileHandle handle)
    {
        var buffer = new char[1024];
        uint length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Length, 0);
        if (length == 0)
            return null;

        if (length > buffer.Length)
        {
            buffer = new char[length];
            length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Length, 0);
            if (length == 0 || length > buffer.Length)
                return null;
        }

        return new string(buffer, 0, (int)length);
    }

    private static string ToExtendedPath(string path)
    {
        string full = Path.GetFullPath(path);
        if (full.StartsWith(@"\\?\", StringComparison.Ordinal))
            return full;
        if (full.StartsWith(@"\\", StringComparison.Ordinal))
            return @"\\?\UNC\" + full[2..];
        return @"\\?\" + full;
    }

    private static string DescribeError(int error) => error switch
    {
        ERROR_SHARING_VIOLATION or ERROR_LOCK_VIOLATION =>
            "File is currently locked by a running process or Windows service (sharing violation)",
        ERROR_ACCESS_DENIED =>
            "Unauthorized: Access denied by system security or file is locked by a protected service.",
        _ => $"Suppressed: {new Win32Exception(error).Message}"
    };
}
