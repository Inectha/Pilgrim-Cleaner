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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LuminaCleaner.Interop;
using LuminaCleaner.Models;
using LuminaCleaner.Modules;
using LuminaCleaner.Services;
using LuminaCleaner.ViewModels;

namespace LuminaCleaner.Tests;

public class Program
{
    private static int _passed = 0;
    private static int _failed = 0;

    [STAThread]
    public static async Task<int> Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--generate-assets")
        {
            GenerateBrandAssets();
            return 0;
        }

        if (args.Length > 0 && args[0] == "--user-audit")
        {
            return await RunUserMeticulousAudit();
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("===============================================================");
        Console.WriteLine("   PILGRIM CLEANER (PILGRIM SOFTWARE) - ARCHITECTURE TEST SUITE ");
        Console.WriteLine("===============================================================");
        Console.ResetColor();

        await RunTest("Native Methods: Query Recycle Bin", TestRecycleBinQuery);
        await RunTest("Native Methods: Recycle Bin Struct Accepted By Windows", TestRecycleBinStructLayout);
        await RunTest("Native Methods: Elevation Detection", TestElevationCheck);
        await RunTest("Safety Policy: 24h Age Safety Buffer", TestAgeSafetyBuffer);
        await RunTest("Safety Policy: Resilient Locked File Deletion", TestLockedFileResilience);
        await RunTest("Safety Policy: Junction Scan Root Is Refused", TestJunctionRootRefused);
        await RunTest("Safety Policy: Post-Scan Junction Swap Cannot Redirect Delete", TestJunctionSwapAfterScan);
        await RunTest("Module C: Browser Non-Destructive Target Verification", TestBrowserNonDestructiveTargets);
        await RunTest("Engine Service: Throttled Progress Dispatching", TestThrottledProgress);
        await RunTest("Engine Service: Dry-Run Architecture", TestDryRunEngine);
        await RunTest("ViewModel: State Machine & Score Calculation", TestViewModelStateMachine);
        await RunTest("App Manager: Application Enumeration & Safety Exclusions", TestAppManagerEnumeration);
        await RunTest("App Manager: Post-Uninstall Residue Safety Guardrail", TestLeftoverSafetyGuardrails);
        await RunTest("App Manager: Leftovers Ignore Live, Shared & Spoofed Folders", TestLeftoverSpoofingGuardrails);
        await RunTest("App Manager: Orphan Registration & Zero Registry Policy", TestAppManagerOrphanGuardrails);
        await RunTest("Driver Health: Device Classification & WHQL Verification", TestDriverHealthClassification);
        await RunTest("Driver Health: Native Windows Update Session Integration", TestWindowsUpdateDriverSession);
        await RunTest("Driver Health: Driver & WHQL Update Correlation", TestDriverUpdateCorrelation);
        await RunTest("Driver Health: Install Script Is Injection-Safe & Parses", TestDriverInstallScriptSafety);
        await RunTest("Storage Module: Developer & Package Manager Caches", TestDeveloperCacheModule);
        await RunTest("Storage Module: Server & CBS Archived Logs (IIS / CBS)", TestServerLogsModule);
        await RunTest("Storage Module: Dism WinSxS Component Store", TestDismComponentModule);
        await RunTest("Sysadmin Engine: Execution Profiles (ServerSafe / RDS)", TestExecutionProfiles);
        await RunTest("Sysadmin Engine: Multi-Profile User Resolution", TestUserProfileResolver);
        await RunTest("Space Lens: Large & Ancient Files Library Discovery", TestSpaceLensScanner);
        await RunTest("Duplicates: Two-Tier Cryptographic SHA-256 Audit", TestDuplicatesFinder);
        await RunTest("Duplicates: Overlapping Roots Never Make A File Its Own Copy", TestDuplicatesOverlappingRoots);
        await RunTest("Startup Accelerator: Non-Destructive StartupApproved", TestStartupAccelerator);
        await RunTest("System Telemetry: Hardware & Memory Integrity", TestTelemetryService);
        await RunTest("Privacy Shield: Footprint & Activity Trace Audit", TestPrivacyService);
        await RunTest("Help & Documentation: User Manual Search & Retrieval", TestHelpService);

        Console.WriteLine();
        Console.WriteLine("---------------------------------------------------------------");
        if (_failed == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($" ALL TESTS PASSED SUCCESSFULLY! ({_passed} passed, 0 failed)");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($" TEST FAILURES DETECTED: ({_passed} passed, {_failed} failed)");
        }
        Console.ResetColor();
        Console.WriteLine("---------------------------------------------------------------");

        return _failed == 0 ? 0 : 1;
    }

    private static async Task RunTest(string name, Func<Task> test)
    {
        Console.Write($"[TEST] {name,-55} ... ");
        try
        {
            await test();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[PASS]");
            Console.ResetColor();
            _passed++;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[FAIL]: {ex.Message}");
            Console.ResetColor();
            _failed++;
        }
    }

    private static Task TestRecycleBinQuery()
    {
        bool success = NativeMethods.QueryRecycleBinTotal(out long bytes, out long items);
        Assert(success, "QueryRecycleBinTotal returned false.");
        Assert(bytes >= 0, $"Recycle bin bytes cannot be negative (got {bytes}).");
        Assert(items >= 0, $"Recycle bin items count cannot be negative (got {items}).");
        return Task.CompletedTask;
    }

    private static Task TestRecycleBinStructLayout()
    {
        // With a wrong struct size Windows rejects the call (E_INVALIDARG) and the bin always looks empty.
        var info = new NativeMethods.SHQUERYRBINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.SHQUERYRBINFO>() };
        int hr = NativeMethods.SHQueryRecycleBin(Path.GetPathRoot(Environment.SystemDirectory), ref info);
        if (hr == unchecked((int)0x80070005)) // E_ACCESSDENIED on root drive in sandbox/standard user; fallback to global user bin
        {
            hr = NativeMethods.SHQueryRecycleBin(null, ref info);
        }
        Assert(hr == 0 || hr != unchecked((int)0x80070057), $"SHQueryRecycleBin rejected the struct (HRESULT 0x{hr:X8}, size {info.cbSize}).");
        return Task.CompletedTask;
    }

    private static Task TestElevationCheck()
    {
        bool isElevated = NativeMethods.IsRunningAsAdministrator();
        // Just verify it evaluates without throwing exception
        Console.Write($"(Elevated={isElevated}) ");
        return Task.CompletedTask;
    }

    private static async Task TestAgeSafetyBuffer()
    {
        string testDir = Path.Combine(Path.GetTempPath(), "LuminaTest_AgeBuffer_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDir);

        try
        {
            string recentFile = Path.Combine(testDir, "recent.tmp");
            string oldFile = Path.Combine(testDir, "old.tmp");

            await File.WriteAllTextAsync(recentFile, "Recent temp data");
            await File.WriteAllTextAsync(oldFile, "Old temp data");

            // Set file times
            File.SetLastWriteTimeUtc(recentFile, DateTime.UtcNow.AddHours(-2)); // 2 hours old (<24h)
            File.SetLastWriteTimeUtc(oldFile, DateTime.UtcNow.AddHours(-48));    // 48 hours old (>24h)

            var module = new TestFolderCleanerModule(testDir);
            var options = new EngineOptions
            {
                AgeSafetyBuffer = TimeSpan.FromHours(24)
            };

            var progress = new Progress<ScanProgressReport>();
            var result = await module.ScanAsync(options, progress, CancellationToken.None);

            Assert(result.Items.Count == 1, $"Expected exactly 1 file discovered, got {result.Items.Count}.");
            Assert(result.Items[0].FileName == "old.tmp", $"Expected 'old.tmp' to be targeted, got '{result.Items[0].FileName}'.");
            Assert(result.SkippedCount == 1, $"Expected 1 skipped file due to age safety buffer, got {result.SkippedCount}.");
        }
        finally
        {
            if (Directory.Exists(testDir))
            {
                try { Directory.Delete(testDir, true); } catch { }
            }
        }
    }

    private static async Task TestLockedFileResilience()
    {
        string testDir = Path.Combine(Path.GetTempPath(), "LuminaTest_Locked_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDir);

        try
        {
            string lockedFile = Path.Combine(testDir, "locked_app.log");
            await File.WriteAllTextAsync(lockedFile, "Log stream in use by active process");

            // Hold open lock
            using var fileStream = new FileStream(lockedFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            var module = new TestFolderCleanerModule(testDir);
            // Deletion is only allowed below folders verified by a scan.
            await module.ScanAsync(new EngineOptions { AgeSafetyBuffer = TimeSpan.Zero }, new Progress<ScanProgressReport>(), CancellationToken.None);

            var item = new CleanItemModel
            {
                FullPath = lockedFile,
                FileName = "locked_app.log",
                SizeBytes = 100,
                LastModified = DateTime.Now.AddDays(-2),
                Category = CleanerCategory.SystemTemp,
                ModuleName = "TestModule",
                IsSelected = true
            };

            // Module clean should gracefully suppress IOException / lock and report SkippedLocked
            var options = new EngineOptions { DryRun = false };
            var progress = new Progress<CleanProgressReport>();

            var cleanResult = module.CleanSingleItemDirectly(item, options);

            Assert(!cleanResult.Success, "Expected delete to fail gracefully because file is locked.");
            Assert(cleanResult.SkipReason != null && cleanResult.SkipReason.Contains("locked"),
                $"Expected locked skip reason, got: {cleanResult.SkipReason}");
            Assert(File.Exists(lockedFile), "Locked file should still exist.");
        }
        finally
        {
            if (Directory.Exists(testDir))
            {
                try { Directory.Delete(testDir, true); } catch { }
            }
        }
    }

    private static async Task TestJunctionRootRefused()
    {
        // A standard user points a cache folder (a scan root) at someone else's data.
        string baseDir = Path.Combine(Path.GetTempPath(), "PilgrimTest_JunctionRoot_" + Guid.NewGuid().ToString("N"));
        string victimDir = Path.Combine(baseDir, "victim");
        string junctionRoot = Path.Combine(baseDir, "cache");
        Directory.CreateDirectory(victimDir);

        try
        {
            string victimFile = Path.Combine(victimDir, "important.docx");
            await File.WriteAllTextAsync(victimFile, "someone else's document");
            File.SetLastWriteTimeUtc(victimFile, DateTime.UtcNow.AddDays(-3));
            if (!TryCreateJunction(junctionRoot, victimDir, out string reason))
            {
                Console.Write($"(Junction creation not permitted in this environment, skipped: {reason}) ");
                return;
            }

            var module = new TestFolderCleanerModule(junctionRoot);
            var scan = await module.ScanAsync(new EngineOptions { AgeSafetyBuffer = TimeSpan.FromHours(24) }, new Progress<ScanProgressReport>(), CancellationToken.None);
            Assert(scan.Items.Count == 0, $"A user-created junction root must not be scanned, got {scan.Items.Count} items.");

            await module.CleanAsync(new EngineOptions { DryRun = false }, new Progress<CleanProgressReport>(), CancellationToken.None);
            Assert(File.Exists(victimFile), "File behind a junction root must never be deleted.");
        }
        finally
        {
            DeleteTestTree(baseDir, junctionRoot);
        }
    }

    private static async Task TestJunctionSwapAfterScan()
    {
        // After the scan, a scanned subfolder is replaced by a junction to another folder
        // that contains a file with the same name (time-of-check/time-of-use attack).
        string baseDir = Path.Combine(Path.GetTempPath(), "PilgrimTest_JunctionSwap_" + Guid.NewGuid().ToString("N"));
        string root = Path.Combine(baseDir, "root");
        string sub = Path.Combine(root, "sub");
        string victimDir = Path.Combine(baseDir, "victim");
        Directory.CreateDirectory(sub);
        Directory.CreateDirectory(victimDir);

        try
        {
            DateTime old = DateTime.UtcNow.AddDays(-3);
            string scannedFile = Path.Combine(sub, "old.log");
            string plainFile = Path.Combine(root, "plain.log");
            string victimFile = Path.Combine(victimDir, "old.log");
            await File.WriteAllTextAsync(scannedFile, "temp");
            await File.WriteAllTextAsync(plainFile, "temp");
            await File.WriteAllTextAsync(victimFile, "someone else's file");
            File.SetLastWriteTimeUtc(scannedFile, old);
            File.SetLastWriteTimeUtc(plainFile, old);
            File.SetLastWriteTimeUtc(victimFile, old);
            File.SetAttributes(plainFile, FileAttributes.ReadOnly); // read-only temp files must still be removed

            var module = new TestFolderCleanerModule(root);
            var scan = await module.ScanAsync(new EngineOptions { AgeSafetyBuffer = TimeSpan.FromHours(24) }, new Progress<ScanProgressReport>(), CancellationToken.None);
            Assert(scan.Items.Count == 2, $"Expected 2 scanned files, got {scan.Items.Count}.");

            Directory.Move(sub, sub + "_moved");
            if (!TryCreateJunction(sub, victimDir, out string reason))
            {
                Console.Write($"(Junction creation not permitted in this environment, skipped: {reason}) ");
                return;
            }

            await module.CleanAsync(new EngineOptions { DryRun = false }, new Progress<CleanProgressReport>(), CancellationToken.None);
            Assert(File.Exists(victimFile), "Delete must not follow a junction swapped in after the scan.");
            Assert(!File.Exists(plainFile), "A normal (read-only) file inside the verified root should still be deleted.");
        }
        finally
        {
            DeleteTestTree(baseDir, sub);
        }
    }

    private static bool TryCreateJunction(string link, string target, out string reason)
    {
        // mklink /J needs no administrator rights, which is exactly why this attack matters.
        var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"), $"/c mklink /J \"{link}\" \"{target}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var process = Process.Start(psi)!;
        process.WaitForExit(10000);
        string output = (process.StandardOutput.ReadToEnd() + " " + process.StandardError.ReadToEnd()).Trim();
        if (!Directory.Exists(link) || (File.GetAttributes(link) & FileAttributes.ReparsePoint) == 0)
        {
            reason = string.IsNullOrWhiteSpace(output) ? "Access is denied or not supported" : output;
            return false;
        }

        // When the tests run elevated (e.g. on a GitHub runner) the junction is owned by the
        // Administrators group, which Pilgrim deliberately trusts. Make the current user the owner,
        // exactly like a junction created by a standard user.
        SetOwnerToCurrentUser(link);
        reason = string.Empty;
        return true;
    }

    private static void SetOwnerToCurrentUser(string path)
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var userSid = identity.User!;
        byte[] sid = new byte[userSid.BinaryLength];
        userSid.GetBinaryForm(sid, 0);

        const uint WRITE_OWNER = 0x00080000;
        const uint FILE_SHARE_ALL = 0x7;
        const uint OPEN_EXISTING = 3;
        const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
        const uint FILE_FLAG_OPEN_REPARSE_POINT = 0x00200000;

        // Open the junction itself, not its target.
        using var handle = TestNative.CreateFileW(@"\\?\" + Path.GetFullPath(path), WRITE_OWNER, FILE_SHARE_ALL, IntPtr.Zero,
            OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT, IntPtr.Zero);
        if (handle.IsInvalid)
            throw new Exception($"Test setup failed: cannot open junction to set owner (error {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}).");

        uint result = TestNative.SetSecurityInfo(handle, 1 /* SE_FILE_OBJECT */, 1 /* OWNER_SECURITY_INFORMATION */,
            sid, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        if (result != 0)
            throw new Exception($"Test setup failed: cannot set junction owner (error {result}).");
    }

    private static class TestNative
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        public static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFileW(
            string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes,
            uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [System.Runtime.InteropServices.DllImport("advapi32.dll")]
        public static extern uint SetSecurityInfo(
            Microsoft.Win32.SafeHandles.SafeFileHandle handle, int objectType, uint securityInfo,
            byte[] psidOwner, IntPtr psidGroup, IntPtr pDacl, IntPtr pSacl);
    }

    private static void DeleteTestTree(string baseDir, string junction)
    {
        // Remove the junction itself first so cleanup can never touch its target.
        try { if (Directory.Exists(junction)) Directory.Delete(junction, false); } catch { }
        try
        {
            foreach (var file in Directory.EnumerateFiles(baseDir, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(baseDir, true);
        }
        catch { }
    }

    private static Task TestBrowserNonDestructiveTargets()
    {
        var module = new BrowserCacheCleanerModule();
        // Verify Module properties
        Assert(module.Category == CleanerCategory.BrowserCache, "Category must be BrowserCache.");
        Assert(!module.RequiresElevation, "Browser cache cleaning must not require elevation.");

        // Security notice check
        Assert(module.SecurityNotice.Contains("NEVER touched") || module.SecurityNotice.Contains("Cookies"),
            "Security notice must clearly state cookies/passwords/history are preserved.");

        // Running detection check
        bool chromeRunning = module.IsChromeRunning;
        bool edgeRunning = module.IsEdgeRunning;
        string warning = module.RunningWarningMessage;
        Assert(!string.IsNullOrEmpty(warning), "RunningWarningMessage should not be empty.");

        return Task.CompletedTask;
    }

    private static Task TestThrottledProgress()
    {
        int callbackCount = 0;
        var throttler = new ThrottledProgress<int>(TimeSpan.FromMilliseconds(50), val =>
        {
            Interlocked.Increment(ref callbackCount);
        });

        // Fire 50 rapid events in 10ms
        for (int i = 0; i < 50; i++)
        {
            throttler.Report(i);
        }

        // Without flush, callbacks should be strongly throttled (much less than 50)
        Assert(callbackCount <= 5, $"Throttled callbacks should be <= 5, was {callbackCount}.");

        // Flush must guarantee the latest value was dispatched
        throttler.Flush();
        Assert(callbackCount >= 1, "At least 1 callback should have been dispatched after flush.");
        return Task.CompletedTask;
    }

    private static async Task TestDryRunEngine()
    {
        string testDir = Path.Combine(Path.GetTempPath(), "LuminaTest_DryRun_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDir);

        try
        {
            string testFile = Path.Combine(testDir, "test_target.tmp");
            await File.WriteAllTextAsync(testFile, "Target to dry run");
            File.SetLastWriteTimeUtc(testFile, DateTime.UtcNow.AddDays(-3));

            var module = new TestFolderCleanerModule(testDir);
            var options = new EngineOptions
            {
                AgeSafetyBuffer = TimeSpan.FromHours(24),
                DryRun = true // DRY RUN
            };

            await module.ScanAsync(options, new Progress<ScanProgressReport>(), CancellationToken.None);
            var cleanResult = await module.CleanAsync(options, new Progress<CleanProgressReport>(), CancellationToken.None);

            Assert(cleanResult.CleanedCount == 1, $"Expected 1 item marked cleaned in dry run, got {cleanResult.CleanedCount}.");
            Assert(File.Exists(testFile), "File MUST NOT be deleted when DryRun is true!");
        }
        finally
        {
            if (Directory.Exists(testDir))
            {
                try { Directory.Delete(testDir, true); } catch { }
            }
        }
    }

    private static Task TestViewModelStateMachine()
    {
        var vm = new MainViewModel();

        Assert(vm.State == AppEngineState.Idle, $"Initial state should be Idle, got {vm.State}.");
        Assert(vm.CleanlinessScore >= 90, $"Initial cleanliness score should be high, got {vm.CleanlinessScore}.");
        Assert(vm.CanScan, "CanScan should be true initially.");
        Assert(!vm.CanClean, "CanClean should be false initially before scan.");
        Assert(!vm.IsBusy, "IsBusy should be false initially.");

        // Navigation check
        vm.NavigateCommand.Execute("Modules");
        Assert(vm.CurrentView == "Modules", $"CurrentView should be 'Modules', got '{vm.CurrentView}'.");

        vm.NavigateCommand.Execute("Dashboard");
        Assert(vm.CurrentView == "Dashboard", $"CurrentView should be 'Dashboard', got '{vm.CurrentView}'.");

        return Task.CompletedTask;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static async Task TestAppManagerEnumeration()
    {
        var appService = new AppManagerService();
        var apps = await appService.GetInstalledApplicationsAsync();
        Assert(apps != null, "Installed applications list cannot be null.");

        // Guardrail verification: No KB updates or System Components
        foreach (var app in apps!)
        {
            Assert(!string.IsNullOrWhiteSpace(app.DisplayName), "Discovered app must have non-empty DisplayName.");
            Assert(!app.DisplayName.StartsWith("KB", StringComparison.OrdinalIgnoreCase) || !char.IsDigit(app.DisplayName.ElementAtOrDefault(2)),
                $"Security hotfix '{app.DisplayName}' was not properly excluded from uninstaller targets.");
            Assert(!app.IsSystemComponent, $"System component '{app.DisplayName}' must not be targeted for uninstallation.");
        }
    }

    private static async Task TestLeftoverSafetyGuardrails()
    {
        var appService = new AppManagerService();
        var dummyApp = new AppItemModel
        {
            DisplayName = "TestDummySoftware_12345",
            Publisher = "PilgrimTests",
            InstallLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TestDummySoftware_12345")
        };

        // Create temporary dummy leftover folder
        string testPath = dummyApp.InstallLocation;
        try
        {
            Directory.CreateDirectory(testPath);
            File.WriteAllText(Path.Combine(testPath, "dummy.dat"), "residual telemetry data");

            var leftovers = await appService.ScanLeftoversAsync(dummyApp);
            Assert(leftovers.Count > 0, "ScanLeftoversAsync should detect the dummy leftover folder.");
            Assert(leftovers.All(l => l.FolderPath.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), StringComparison.OrdinalIgnoreCase)),
                "Leftover folder must reside strictly within user AppData hierarchy.");

            // Purge leftover safely
            bool deleted = await appService.DeleteLeftoversAsync(leftovers);
            Assert(deleted, "DeleteLeftoversAsync should successfully purge candidate leftover directories.");
            Assert(!Directory.Exists(testPath), "Target leftover directory should be erased after purge.");
        }
        finally
        {
            if (Directory.Exists(testPath))
            {
                try { Directory.Delete(testPath, true); } catch { }
            }
        }
    }

    private static async Task TestLeftoverSpoofingGuardrails()
    {
        var appService = new AppManagerService();
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        string id = Guid.NewGuid().ToString("N")[..8];

        string liveName = "PilgrimTest_Live_" + id;
        string liveDir = Path.Combine(localAppData, liveName);
        string sharedDir = Path.Combine(programData, "PilgrimTest_Shared_" + id);

        bool createdShared = false;
        try
        {
            Directory.CreateDirectory(liveDir);
            try
            {
                Directory.CreateDirectory(sharedDir);
                createdShared = true;
            }
            catch (Exception)
            {
                // In unprivileged or sandboxed test runs, writing to C:\ProgramData root is restricted
            }

            // 1. App is still registered (e.g. the uninstaller was cancelled): nothing may be pre-selected.
            var liveApp = new AppItemModel
            {
                DisplayName = liveName,
                InstallLocation = liveDir,
                RegistryHive = Microsoft.Win32.RegistryHive.CurrentUser,
                RegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion" // exists, so "still registered"
            };
            var liveLeftovers = await appService.ScanLeftoversAsync(liveApp);
            Assert(liveLeftovers.All(l => !l.IsSelected), "Folders of a still-installed app must never be pre-selected.");
            await appService.DeleteLeftoversAsync(liveLeftovers);
            Assert(Directory.Exists(liveDir), "Purging with default selection must not delete a still-installed app.");

            // 2. Registry values with path tricks must not produce candidates.
            var escapeApp = new AppItemModel { DisplayName = @"..\..\PilgrimTest_Escape" };
            Assert((await appService.ScanLeftoversAsync(escapeApp)).Count == 0, "A DisplayName with '..' must be ignored.");

            var sharedNameApp = new AppItemModel { DisplayName = "Microsoft" };
            Assert((await appService.ScanLeftoversAsync(sharedNameApp)).Count == 0, "Shared folder names must be ignored.");

            // 3. A per-user (HKCU) entry may not point its install folder at machine-wide data.
            var perUserApp = new AppItemModel
            {
                DisplayName = "PilgrimTest_PerUser_" + id,
                InstallLocation = sharedDir,
                IsPerUserEntry = true
            };
            var perUserLeftovers = await appService.ScanLeftoversAsync(perUserApp);
            Assert(perUserLeftovers.All(l => !l.FolderPath.Equals(sharedDir, StringComparison.OrdinalIgnoreCase)),
                "A per-user entry must not be able to target a ProgramData folder.");

            // 4. Shared install folders are never offered, even for per-machine entries.
            string commonFiles = Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles);
            var commonApp = new AppItemModel { DisplayName = "PilgrimTest_Common_" + id, InstallLocation = commonFiles };
            var commonLeftovers = await appService.ScanLeftoversAsync(commonApp);
            Assert(commonLeftovers.All(l => !l.FolderPath.Equals(commonFiles, StringComparison.OrdinalIgnoreCase)),
                "'Common Files' must never be offered as a leftover.");
        }
        finally
        {
            try { Directory.Delete(liveDir, true); } catch { }
            try { if (createdShared && Directory.Exists(sharedDir)) Directory.Delete(sharedDir, true); } catch { }
        }
    }

    private static async Task TestAppManagerOrphanGuardrails()
    {
        // 1. Verify ParseUninstallCommand
        var (quotedExe, quotedArgs) = AppManagerService.ParseUninstallCommand("\"C:\\Program Files\\App\\unins000.exe\" /SILENT");
        Assert(quotedExe == @"C:\Program Files\App\unins000.exe", "Quoted executable path should be parsed without quotes.");
        Assert(quotedArgs == "/SILENT", "Arguments should be parsed correctly.");

        var (bareExe, bareArgs) = AppManagerService.ParseUninstallCommand(@"C:\App\uninstall.exe -force");
        Assert(bareExe == @"C:\App\uninstall.exe", "Unquoted executable path should be parsed correctly.");
        Assert(bareArgs == "-force", "Arguments should be parsed correctly.");

        // 2. Verify IsUninstallerMissing
        var orphanApp = new AppItemModel
        {
            DisplayName = "Orphaned Game Launcher",
            UninstallString = "\"C:\\NonExistent_Fake_Dir_99999\\unins000.exe\" /SILENT",
            RegistryHive = Microsoft.Win32.RegistryHive.LocalMachine,
            RegistryView = Microsoft.Win32.RegistryView.Registry32,
            RegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\OrphanGameLauncher_is1"
        };
        bool isMissing = AppManagerService.IsUninstallerMissing(orphanApp, out string? missingExe);
        Assert(isMissing, "IsUninstallerMissing should return true when the uninstaller executable does not exist on disk.");
        Assert(missingExe == @"C:\NonExistent_Fake_Dir_99999\unins000.exe", "Missing uninstaller path should match parsed target.");

        // Packaged apps and msiexec are not flagged as missing exe
        var msiApp = new AppItemModel
        {
            DisplayName = "Msi Installer App",
            UninstallString = "MsiExec.exe /X{12345678-ABCD-1234-ABCD-1234567890AB}"
        };
        Assert(!AppManagerService.IsUninstallerMissing(msiApp, out _), "MsiExec apps should never be flagged as missing local uninstaller.");

        var storeApp = new AppItemModel
        {
            DisplayName = "Store Package App",
            IsPackagedApp = true
        };
        Assert(!AppManagerService.IsUninstallerMissing(storeApp, out _), "Packaged store apps should never be flagged as missing uninstaller.");

        // 3. Verify GetOrphanCleanupCommand generates targeted, non-destructive PowerShell command
        string cleanupCmdHklm32 = AppManagerService.GetOrphanCleanupCommand(orphanApp);
        Assert(cleanupCmdHklm32.Contains(@"HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\OrphanGameLauncher_is1"),
            "Generated command should target WOW6432Node for 32-bit HKLM entries.");
        Assert(cleanupCmdHklm32.StartsWith("Remove-Item -Path", StringComparison.OrdinalIgnoreCase),
            "Generated command must use Remove-Item.");

        var hkcuApp = new AppItemModel
        {
            DisplayName = "Per User App",
            RegistryHive = Microsoft.Win32.RegistryHive.CurrentUser,
            RegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PerUserApp1"
        };
        string cleanupCmdHkcu = AppManagerService.GetOrphanCleanupCommand(hkcuApp);
        Assert(cleanupCmdHkcu.Contains(@"HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PerUserApp1"),
            "Generated command should target HKCU for per-user apps.");

        // 4. Verify ViewModel intercepts orphan uninstalls and opens drawer without launching process
        var vm = new LuminaCleaner.ViewModels.AppManagerViewModel();
        await vm.UninstallApplicationAsync(orphanApp);
        Assert(vm.IsOrphanDrawerOpen, "Orphan drawer should open when attempting to uninstall an app with missing uninstaller.");
        Assert(vm.CurrentOrphanApp == orphanApp, "CurrentOrphanApp should be set to the targeted orphan app.");
        Assert(vm.OrphanPowerShellCommand == cleanupCmdHklm32, "OrphanPowerShellCommand should be populated in the ViewModel.");

        vm.DismissOrphanDrawerCommand.Execute(null);
        Assert(!vm.IsOrphanDrawerOpen, "Dismissing orphan drawer should set IsOrphanDrawerOpen to false.");
    }

    private static async Task TestDriverHealthClassification()
    {
        var driverService = new DriverHealthService();
        var drivers = await driverService.GetInstalledDriversAsync();
        Assert(drivers != null, "Drivers list cannot be null.");

        // Verify category mapping logic
        foreach (var d in drivers!)
        {
            Assert(!string.IsNullOrWhiteSpace(d.DeviceName), "Driver device name must not be blank.");
            Assert(Enum.IsDefined(typeof(DriverCategory), d.Category), "Driver category must be a recognized DriverCategory enum.");
        }
    }

    private static async Task TestWindowsUpdateDriverSession()
    {
        var driverService = new DriverHealthService();
        // Native Windows Update session search test
        var updates = await driverService.CheckWhqlUpdatesAsync();
        Assert(updates != null, "Windows Update driver query returned null.");
        foreach (var u in updates!)
        {
            Assert(!string.IsNullOrWhiteSpace(u.Title), "Driver update package must contain a title.");
            Assert(u.IsWhqlCertified, "Updates discovered via native Windows Update session must be WHQL certified.");
        }
    }

    private static Task TestDriverUpdateCorrelation()
    {
        var driverService = new DriverHealthService();
        var drivers = new System.Collections.Generic.List<DriverItemModel>
        {
            new() { DeviceName = "Intel(R) Watchdog Timer Driver", DeviceClass = "SYSTEM" },
            new() { DeviceName = "Standard SATA AHCI Controller", DeviceClass = "HDC" }
        };

        var updates = new System.Collections.Generic.List<DriverUpdateModel>
        {
            new()
            {
                Title = "Intel - System - 9/19/2017 - 11.7.0.1000",
                DriverModel = "Intel(R) Watchdog Timer Driver (Intel(R) WDT)",
                DriverVersion = "11.7.0.1000",
                DriverVerDate = "2017-09-19"
            }
        };

        driverService.CorrelateDriversWithUpdates(drivers, updates);

        var watchdog = drivers[0];
        Assert(watchdog.HasPendingUpdate, "Watchdog driver should have pending update detected.");
        Assert(!watchdog.IsUpToDate, "Watchdog driver should be marked as not up to date.");
        Assert(watchdog.MatchingUpdate == updates[0], "Watchdog driver should reference the matching update package.");

        var sata = drivers[1];
        Assert(!sata.HasPendingUpdate, "SATA driver without pending update should not be flagged.");
        Assert(sata.IsUpToDate, "SATA driver should be marked as up to date.");

        return Task.CompletedTask;
    }

    private static Task TestDriverInstallScriptSafety()
    {
        // A crafted update title (from Windows Update / WSUS) trying to break out with straight,
        // typographic and double quotes plus a subexpression.
        string hostileTitle = "Intel ' ‘ ’ \" ; $(New-Item C:\\pwned) ; Driver";
        string script = DriverHealthService.BuildDriverInstallScript(hostileTitle);
        Assert(!script.Contains("pwned"), "The update title must never appear in the script as code.");

        // Let PowerShell parse (not run) the script and report syntax errors.
        string scriptFile = Path.Combine(Path.GetTempPath(), "PilgrimTest_DriverScript_" + Guid.NewGuid().ToString("N") + ".ps1");
        File.WriteAllText(scriptFile, script);
        try
        {
            var psi = new ProcessStartInfo(SystemTools.PowerShell,
                $"-NoProfile -NonInteractive -Command \"$e=$null; [void][System.Management.Automation.Language.Parser]::ParseFile('{scriptFile}',[ref]$null,[ref]$e); exit $e.Count\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var process = Process.Start(psi)!;
            process.WaitForExit(30000);
            Assert(process.ExitCode == 0, $"Driver install script has {process.ExitCode} PowerShell syntax error(s).");
        }
        finally
        {
            try { File.Delete(scriptFile); } catch { }
        }
        return Task.CompletedTask;
    }

    private static async Task TestDeveloperCacheModule()
    {
        var module = new DeveloperCacheCleanerModule();
        Assert(module.Id == "developer_caches", "Expected Id 'developer_caches'.");
        Assert(module.Category == CleanerCategory.DeveloperCaches, "Expected DeveloperCaches category.");

        var options = new EngineOptions();
        var progress = new Progress<ScanProgressReport>();
        var result = await module.ScanAsync(options, progress, CancellationToken.None);

        Assert(result != null, "Developer cache scan result cannot be null.");
    }

    private static async Task TestServerLogsModule()
    {
        var module = new ServerLogsCleanerModule();
        Assert(module.Id == "server_logs", "Expected Id 'server_logs'.");
        Assert(module.Category == CleanerCategory.ServerLogs, "Expected ServerLogs category.");
        Assert(module.RequiresElevation, "Server logs module must require elevation.");

        var options = new EngineOptions { DryRun = true };
        var progress = new Progress<ScanProgressReport>();
        var result = await module.ScanAsync(options, progress, CancellationToken.None);
        Assert(result != null, "Server logs scan result cannot be null.");
    }

    private static Task TestExecutionProfiles()
    {
        var engine = new EngineService();
        Assert(engine.Modules.Count == 8, $"Expected 8 modules registered in EngineService, got {engine.Modules.Count}.");

        // Test ServerSafe profile
        var serverOpts = new EngineOptions { Profile = CleanupProfile.ServerSafe };
        engine.ApplyProfile(serverOpts);
        Assert(engine.Modules.First(m => m.Id == "system_temp").IsEnabled, "ServerSafe should enable system_temp.");
        Assert(engine.Modules.First(m => m.Id == "server_logs").IsEnabled, "ServerSafe should enable server_logs.");
        Assert(!engine.Modules.First(m => m.Id == "browser_cache").IsEnabled, "ServerSafe must disable browser_cache.");
        Assert(!engine.Modules.First(m => m.Id == "developer_caches").IsEnabled, "ServerSafe must disable developer_caches.");

        // Test RDS profile
        var rdsOpts = new EngineOptions { Profile = CleanupProfile.Rds };
        engine.ApplyProfile(rdsOpts);
        Assert(rdsOpts.AllProfiles, "RDS profile must automatically enable AllProfiles.");
        Assert(engine.Modules.First(m => m.Id == "browser_cache").IsEnabled, "RDS profile should enable browser_cache.");
        Assert(engine.Modules.First(m => m.Id == "server_logs").IsEnabled, "RDS profile should enable server_logs.");

        return Task.CompletedTask;
    }

    private static Task TestUserProfileResolver()
    {
        var profiles = UserProfileResolver.GetTargetUserProfiles(includeAllProfiles: false);
        Assert(profiles.Count >= 1, "UserProfileResolver must return at least the current user profile.");
        Assert(!profiles.Any(p => p.EndsWith("\\Public", StringComparison.OrdinalIgnoreCase)), "Public profile must be excluded.");
        Assert(!profiles.Any(p => p.EndsWith("\\Default", StringComparison.OrdinalIgnoreCase)), "Default profile must be excluded.");
        return Task.CompletedTask;
    }

    private static async Task TestDismComponentModule()
    {
        var module = new DismComponentCleanerModule();
        Assert(module.Id == "dism_component_store", "Expected Id 'dism_component_store'.");
        Assert(module.RequiresElevation, "DISM module must declare elevation requirement.");

        var options = new EngineOptions { DryRun = true };
        var progress = new Progress<ScanProgressReport>();
        var result = await module.ScanAsync(options, progress, CancellationToken.None);

        Assert(result.Items.Count > 0, "DISM scan should produce targeted component entry.");
    }

    private static async Task TestSpaceLensScanner()
    {
        string testDir = Path.Combine(Path.GetTempPath(), "LuminaTest_SpaceLens_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDir);

        try
        {
            string hugeFile = Path.Combine(testDir, "test_archive.zip");
            string smallFile = Path.Combine(testDir, "small.txt");

            // Write 5 KB and 100 bytes
            byte[] dummyData = new byte[5 * 1024];
            new Random().NextBytes(dummyData);
            await File.WriteAllBytesAsync(hugeFile, dummyData);
            await File.WriteAllTextAsync(smallFile, "small file");

            var service = new SpaceLensService();
            // Test with 1 KB threshold
            var results = await service.ScanLibrariesAsync([testDir], 1024, null, CancellationToken.None);

            Assert(results.Count == 1, $"Expected 1 file above threshold, got {results.Count}.");
            Assert(results[0].FileName == "test_archive.zip", "Expected 'test_archive.zip' to be found.");
            Assert(results[0].FileCategory == "Archives", $"Expected FileCategory 'Archives', got {results[0].FileCategory}.");
        }
        finally
        {
            try { Directory.Delete(testDir, true); } catch { }
        }
    }

    private static async Task TestDuplicatesFinder()
    {
        string testDir = Path.Combine(Path.GetTempPath(), "LuminaTest_Dupes_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDir);

        try
        {
            string file1 = Path.Combine(testDir, "document_copy1.bin");
            string file2 = Path.Combine(testDir, "document_copy2.bin");
            string file3 = Path.Combine(testDir, "distinct.bin");

            byte[] identicalBytes = new byte[2048];
            Array.Fill(identicalBytes, (byte)0xAB);

            byte[] distinctBytes = new byte[2048];
            Array.Fill(distinctBytes, (byte)0xCD);

            await File.WriteAllBytesAsync(file1, identicalBytes);
            await File.WriteAllBytesAsync(file2, identicalBytes);
            await File.WriteAllBytesAsync(file3, distinctBytes);

            var service = new DuplicatesService();
            var groups = await service.FindDuplicatesAsync([testDir], null, CancellationToken.None);

            Assert(groups.Count == 1, $"Expected 1 duplicate group, got {groups.Count}.");
            Assert(groups[0].Copies.Count == 2, $"Expected 2 copies in duplicate group, got {groups[0].Copies.Count}.");
            Assert(groups[0].WastedBytes == 2048, $"Expected 2048 wasted bytes, got {groups[0].WastedBytes}.");
            Assert(groups[0].Copies[0].IsOriginal, "First copy should be designated original.");
            Assert(groups[0].Copies[1].IsSelected, "Second copy should be pre-selected for cleanup.");
        }
        finally
        {
            try { Directory.Delete(testDir, true); } catch { }
        }
    }

    private static async Task TestDuplicatesOverlappingRoots()
    {
        string root = Path.Combine(Path.GetTempPath(), "PilgrimTest_DupOverlap_" + Guid.NewGuid().ToString("N"));
        string photos = Path.Combine(root, "Photos");
        Directory.CreateDirectory(photos);

        try
        {
            var unique = new byte[4096];
            new Random(42).NextBytes(unique);
            string onlyCopy = Path.Combine(photos, "holiday.jpg");
            await File.WriteAllBytesAsync(onlyCopy, unique);

            var service = new DuplicatesService();

            // Same folder reached via three overlapping roots (like Pictures redirected into Documents).
            var groups = await service.FindDuplicatesAsync([root, photos, root + "\\"], null, CancellationToken.None);
            Assert(groups.Count == 0, $"A single file must not be reported as its own duplicate, got {groups.Count} group(s).");

            // A real duplicate is still found, with two different paths.
            string realCopy = Path.Combine(root, "holiday (1).jpg");
            await File.WriteAllBytesAsync(realCopy, unique);
            groups = await service.FindDuplicatesAsync([root, photos], null, CancellationToken.None);
            Assert(groups.Count == 1 && groups[0].Copies.Count == 2, "Expected exactly one group with two copies.");
            Assert(groups[0].Copies.Select(c => c.FullPath).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 2,
                "Copies in a group must be different files.");

            // The recycle helper refuses to remove a file "in favour of" itself.
            Assert(!service.RecycleDuplicate(onlyCopy, onlyCopy), "RecycleDuplicate must refuse when copy and kept file are the same.");
            Assert(File.Exists(onlyCopy), "The only copy must still exist.");
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    private static async Task TestStartupAccelerator()
    {
        var service = new StartupService();
        var items = await service.GetStartupItemsAsync(CancellationToken.None);

        Assert(items != null, "Startup items collection cannot be null.");
        Console.Write($"({items!.Count} entries) ");
    }

    private static async Task TestTelemetryService()
    {
        var service = new SystemTelemetryService();
        var telemetry = await service.SampleTelemetryAsync();

        Assert(telemetry.RamTotalBytes > 0, "Total RAM cannot be zero.");
        Assert(telemetry.RamAvailableBytes > 0, "Available RAM cannot be zero.");
        Assert(telemetry.RamUsagePercent > 0 && telemetry.RamUsagePercent <= 100, $"RAM usage percent must be 1-100% (got {telemetry.RamUsagePercent}%).");
        Assert(!string.IsNullOrWhiteSpace(telemetry.CpuName), "CPU Name must be detected.");
        Assert(telemetry.Drives != null, "Storage Drives collection cannot be null.");
        Console.Write($"(RAM={telemetry.FormattedRamUsed}/{telemetry.FormattedRamTotal}, CPU={telemetry.CpuName}) ");
    }

    private static async Task TestPrivacyService()
    {
        var service = new PrivacyService();
        var targets = await service.AuditPrivacyAsync(CancellationToken.None);

        Assert(targets.Count >= 3, $"Expected at least 3 privacy targets, got {targets.Count}.");
        Assert(targets.Any(t => t.Id == "recent_items"), "Recent items target must be present.");
        Assert(targets.Any(t => t.Id == "clipboard_buffer"), "Clipboard buffer target must be present.");

        var recent = targets.First(t => t.Id == "recent_items");
        Assert(recent.DiscoveredTraces != null, "DiscoveredTraces collection must not be null.");

        // Verify PurgePrivacyAsync in PrivacyViewModel resets target UI state safely without cross-thread violations
        var vm = new LuminaCleaner.ViewModels.PrivacyViewModel(service);
        var dummyTarget = new LuminaCleaner.Models.PrivacyTargetModel
        {
            Id = "test_custom_target",
            Title = "Test Target",
            Description = "Testing Purge Behavior",
            Category = "Testing",
            IconSymbol = Wpf.Ui.Controls.SymbolRegular.Apps16,
            ItemCount = 1,
            SizeBytes = 256,
            IsSelected = true
        };
        dummyTarget.DiscoveredTraces.Add(new LuminaCleaner.Models.PrivacyTraceItemModel
        {
            FileName = "test_trace.tmp",
            FilePath = "C:\\test\\test_trace.tmp",
            SizeBytes = 256
        });
        vm.Targets.Add(dummyTarget);
        await vm.PurgePrivacyAsync();

        Assert(dummyTarget.IsCleaned, "Target should be marked cleaned after purge.");
        Assert(dummyTarget.ItemCount == 0, "Target item count should be 0 after purge.");
        Assert(dummyTarget.SizeBytes == 0, "Target size bytes should be 0 after purge.");
        Assert(dummyTarget.DiscoveredTraces.Count == 0, "DiscoveredTraces collection must be cleared on UI thread.");
    }

    private static Task TestHelpService()
    {
        var service = new HelpService();
        var all = service.GetAllArticles();
        Assert(all.Count >= 12, $"Expected at least 12 documentation chapters, got {all.Count}.");

        // Test search query (English and alias)
        var bufferResults = service.SearchArticles("buffer", "All");
        Assert(bufferResults.Count > 0, "Expected search for 'buffer' to return results.");
        Assert(bufferResults.Any(a => a.Id == "safety_manifesto"), "Search for 'buffer' should return safety manifesto chapter.");

        // Test category filter (both English and legacy alias)
        var storageResults = service.SearchArticles("", "Storage & Cleanup");
        Assert(storageResults.Count >= 3, $"Expected at least 3 storage chapters, got {storageResults.Count}.");

        var storageAliasResults = service.SearchArticles("", "Opslag & Schoonmaken");
        Assert(storageAliasResults.Count >= 3, $"Expected at least 3 storage chapters via alias, got {storageAliasResults.Count}.");

        // Test keyword search
        var whqlResults = service.SearchArticles("whql", "All");
        Assert(whqlResults.Any(a => a.Id == "driver_health"), "Search for 'whql' should return driver health chapter.");

        Console.Write($"({all.Count} English chapters) ");
        return Task.CompletedTask;
    }

    public static void GenerateBrandAssets()
    {
        var thread = new Thread(() =>
        {
            string assetsDir = Path.GetFullPath("Assets");
            Directory.CreateDirectory(assetsDir);

            Console.WriteLine("[ASSETS] Rendering mathematical vector PilgrimLogoControl (512x512)...");
            var renderedBitmap = Controls.PilgrimLogoControl.CreateRenderedBitmap(512, 512, showSquircle: true, glowOpacity: 0.85);

            // 1. Save high-res PNG (Assets/PilgrimIconP.png)
            string pngPath = Path.Combine(assetsDir, "PilgrimIconP.png");
            var pngEncoder = new PngBitmapEncoder();
            pngEncoder.Frames.Add(BitmapFrame.Create(renderedBitmap));
            using (var fs = File.Create(pngPath))
            {
                pngEncoder.Save(fs);
            }

            // 2. Save DesktopIcon.png
            string desktopPath = Path.Combine(assetsDir, "DesktopIcon.png");
            File.Copy(pngPath, desktopPath, true);

            // 3. Generate multi-resolution ICO (Assets/app.ico)
            int[] sizes = [256, 128, 64, 48, 32, 16];
            var pngBytesList = new List<byte[]>();

            foreach (int s in sizes)
            {
                double scale = (double)s / renderedBitmap.PixelWidth;
                var resized = new TransformedBitmap(renderedBitmap, new ScaleTransform(scale, scale));
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(resized));
                using var ms = new MemoryStream();
                enc.Save(ms);
                pngBytesList.Add(ms.ToArray());
            }

            using var icoFs = File.Create(Path.Combine(assetsDir, "app.ico"));
            using var bw = new BinaryWriter(icoFs);

            bw.Write((short)0); // reserved
            bw.Write((short)1); // icon type
            bw.Write((short)sizes.Length);

            int offset = 6 + (16 * sizes.Length);
            for (int i = 0; i < sizes.Length; i++)
            {
                int sz = sizes[i];
                bw.Write((byte)(sz >= 256 ? 0 : sz));
                bw.Write((byte)(sz >= 256 ? 0 : sz));
                bw.Write((byte)0); // color count
                bw.Write((byte)0); // reserved
                bw.Write((short)1); // color planes
                bw.Write((short)32); // bit count
                bw.Write((int)pngBytesList[i].Length);
                bw.Write((int)offset);
                offset += pngBytesList[i].Length;
            }

            for (int i = 0; i < sizes.Length; i++)
            {
                bw.Write(pngBytesList[i]);
            }

            Console.WriteLine("[ASSETS] Successfully generated ultra-crisp modern logo assets in Assets/ !");
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    private class TestFolderCleanerModule : CleanerModuleBase
    {
        private readonly string _path;

        public TestFolderCleanerModule(string path) => _path = path;

        public override string Id => "test_module";
        public override string Name => "Test Cleaner Module";
        public override string Description => "Test module for safety buffer verification.";
        public override string SecurityNotice => "Test safety buffer.";
        public override CleanerCategory Category => CleanerCategory.SystemTemp;
        public override Wpf.Ui.Controls.SymbolRegular IconSymbol => Wpf.Ui.Controls.SymbolRegular.Folder24;

        public (bool Success, string? SkipReason) CleanSingleItemDirectly(CleanItemModel item, EngineOptions options)
        {
            return SafeDeleteFile(item.FullPath, options.DryRun);
        }

        public override Task<ModuleScanResult> ScanAsync(EngineOptions options, IProgress<ScanProgressReport> progress, CancellationToken ct)
        {
            ClearResults();
            var discovered = new System.Collections.Generic.List<CleanItemModel>();
            int skipped = 0;
            long totalBytes = 0;

            DateTime now = DateTime.UtcNow;
            foreach (var fi in SafeEnumerateFiles(_path))
            {
                if (now - fi.LastWriteTimeUtc < options.AgeSafetyBuffer)
                {
                    skipped++;
                    continue;
                }

                var item = new CleanItemModel
                {
                    FullPath = fi.FullName,
                    FileName = fi.Name,
                    SizeBytes = fi.Length,
                    LastModified = fi.LastWriteTime,
                    Category = Category,
                    ModuleName = Name,
                    IsSelected = true
                };
                discovered.Add(item);
                totalBytes += fi.Length;
                AddDiscoveredItem(item);
            }

            return Task.FromResult(new ModuleScanResult(Id, discovered, totalBytes, 0, skipped, TimeSpan.Zero));
        }

        public override Task<ModuleCleanResult> CleanAsync(EngineOptions options, IProgress<CleanProgressReport> progress, CancellationToken ct)
        {
            int cleaned = 0;
            long bytes = 0;
            foreach (var item in DiscoveredItems.Where(i => i.IsSelected))
            {
                var (success, _) = SafeDeleteFile(item.FullPath, options.DryRun);
                if (success)
                {
                    cleaned++;
                    bytes += item.SizeBytes;
                }
            }
            return Task.FromResult(new ModuleCleanResult(Id, bytes, cleaned, 0, 0, TimeSpan.Zero));
        }
    }

    public static async Task<int> RunUserMeticulousAudit()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("==========================================================================");
        Console.WriteLine("   PILGRIM CLEANER - COMPREHENSIVE USER-PERSPECTIVE AUDIT & TEST HARNESS ");
        Console.WriteLine("==========================================================================");
        Console.ResetColor();

        // 1. Dashboard & Engine Audit (100% Gauge sweep)
        await AuditFeature("Dashboard: Storage Health Audit & 100% Gauge Sweep", async () =>
        {
            var vm = new MainViewModel();
            Assert(vm.State == AppEngineState.Idle, "Initial state should be Idle.");
            Assert(vm.CanScan, "Should be able to scan initially.");

            await vm.StartScanCommand.ExecuteAsync(null);

            Assert(vm.State == AppEngineState.ReviewReady, $"State should be ReviewReady, got {vm.State}");
            Assert(Math.Abs(vm.CleanProgressPercentage - 100.0) < 0.001, $"Progress must reach 100.0%, got {vm.CleanProgressPercentage}");
            Assert(Math.Abs(vm.GaugeFillRatio - 1.0) < 0.001, $"Donut Gauge must reach 1.0 (100%), got {vm.GaugeFillRatio}");
            Assert(vm.TotalRecoverableBytes >= 0, "Recoverable bytes must be >= 0");
            Assert(vm.TotalFilesAnalyzed >= 0, "Audited files count must be >= 0");
            Console.Write($"[Recoverable={vm.FormattedRecoverableBytes}, Skipped={vm.FormattedSkippedBytes}, Audited={vm.TotalFilesAnalyzed:N0} files] ");
        });

        // 2. Storage Modules Granular Review
        await AuditFeature("Storage Modules: Granular Category Review & Selection", () =>
        {
            var vm = new MainViewModel();
            Assert(vm.Modules.Count == 8, $"Expected 8 core modules, got {vm.Modules.Count}");
            foreach (var mod in vm.Modules)
            {
                Assert(!string.IsNullOrWhiteSpace(mod.Name), "Module name must not be empty");
                Assert(!string.IsNullOrWhiteSpace(mod.SecurityNotice), "Security notice must not be empty");
            }
            // Test selection toggles
            vm.SelectAllModulesCommand.Execute(false);
            Assert(vm.Modules.All(m => !m.IsEnabled), "All modules should be disabled.");
            vm.SelectAllModulesCommand.Execute(true);
            Assert(vm.Modules.All(m => m.IsEnabled), "All modules should be enabled.");
            Console.Write($"(8 modules verified) ");
            return Task.CompletedTask;
        });

        // 3. Space Lens (Large & Ancient Files Explorer)
        await AuditFeature("Space Lens: Large & Ancient User Files Discovery", async () =>
        {
            var vm = new SpaceLensViewModel();
            await vm.StartScanCommand.ExecuteAsync(null);
            Console.Write($"({vm.FilteredItems.Count} files cataloged, volume={vm.TotalVolumeText}) ");

            // Test Category filter
            vm.SelectedCategoryFilter = "Videos";
            vm.SelectedCategoryFilter = "Archives";
            vm.SelectedCategoryFilter = "All Types";

            // Test Size & Age filter
            vm.SelectedSizeFilter = "> 100 MB";
            vm.SelectedSizeFilter = "All Sizes";
            vm.SelectedAgeFilter = "All Ages";
        });

        // 4. Duplicate & Redundant Files Finder
        await AuditFeature("Duplicates: SHA-256 Byte-Identical Discovery & Badges", async () =>
        {
            var vm = new DuplicatesViewModel();
            await vm.StartScanCommand.ExecuteAsync(null);
            Console.Write($"({vm.TotalDuplicateGroups} groups, {vm.TotalRedundantFiles} redundant copies, wasted={vm.WastedVolumeText}) ");

            foreach (var g in vm.DuplicateGroups.Take(5))
            {
                Assert(g.Copies.Count >= 2, "Each duplicate group must have >= 2 copies.");
                Assert(g.Copies.Count(c => c.IsOriginal) == 1, "Each group must have exactly 1 original copy.");
                Assert(!string.IsNullOrWhiteSpace(g.FormattedWastedBytes), "Wasted bytes must be formatted.");
                Assert(g.FormattedWastedBytes != "0 B" || g.SizeBytes == 0, $"Wasted bytes cannot be '0 B' if size > 0 (got {g.FormattedWastedBytes})");
            }

            vm.DeselectAllCommand.Execute(null);
            Assert(vm.DuplicateGroups.SelectMany(g => g.Copies).All(c => !c.IsSelected), "All copies should be deselected.");
            vm.SelectAllRedundantCommand.Execute(null);
            Assert(vm.DuplicateGroups.SelectMany(g => g.Copies).Where(c => !c.IsOriginal).All(c => c.IsSelected), "All redundant copies should be selected.");
        });

        // 5. App Manager & Leftover Hunter
        await AuditFeature("App Manager: Software Inventory & Leftover Guardrail", async () =>
        {
            var vm = new AppManagerViewModel();
            await vm.RefreshApplicationsCommand.ExecuteAsync(null);
            Assert(vm.TotalAppsCount > 0, "Applications list should not be empty.");
            Console.Write($"({vm.TotalAppsCount} apps detected, {vm.TotalSizeText}) ");

            // Test filters
            vm.SelectedCategoryFilter = "Win32 Desktop";
            Assert(vm.FilteredApplications.All(a => a.Category == "Win32 Desktop"), "Filtered list should contain only Win32 apps.");
            vm.SelectedCategoryFilter = "Windows Store App";
            Assert(vm.FilteredApplications.All(a => a.IsPackagedApp), "Filtered list should contain only packaged apps.");
            vm.SelectedCategoryFilter = "All";

            // Test search
            vm.SearchQuery = "Windows";
            Assert(vm.FilteredApplications.Count <= vm.TotalAppsCount, "Search should filter apps.");
            vm.SearchQuery = string.Empty;
        });

        // 6. Driver Health & Certified WHQL Updates
        await AuditFeature("Driver Health: Hardware Inventory & WHQL Session", async () =>
        {
            var vm = new DriverHealthViewModel();
            await vm.ScanDriversCommand.ExecuteAsync(null);
            Console.Write($"({vm.TotalDriversCount} devices, {vm.AvailableUpdates.Count} pending WHQL updates) ");

            // Test category filtering
            vm.SelectCategory("Graphics");
            vm.SelectCategory("Network");
            vm.SelectCategory("Audio");
            vm.SelectCategory("Storage");
            vm.SelectCategory("System");
            vm.SelectCategory("All");
        });

        // 7. Startup Boot Accelerator (StartupApproved)
        await AuditFeature("Startup Accelerator: StartupApproved Bitmask & Discord Toggle", async () =>
        {
            var vm = new StartupViewModel();
            await vm.RefreshStartupItemsCommand.ExecuteAsync(null);
            Assert(vm.FilteredItems.Count > 0, "Startup items list should not be empty.");
            Console.Write($"({vm.FilteredItems.Count} startup programs: {string.Join(", ", vm.FilteredItems.Select(x => $"{x.Name}[{x.SourceType}]"))}) ");

            var target = vm.FilteredItems.FirstOrDefault(i => i.Name.Contains("Discord", StringComparison.OrdinalIgnoreCase))
                         ?? vm.FilteredItems.First();
            bool initial = target.IsEnabled;

            // Toggle once
            vm.ToggleItem(target);
            if (target.IsEnabled != initial)
            {
                // Toggle back to restore
                vm.ToggleItem(target);
                Assert(target.IsEnabled == initial, $"Target item '{target.Name}' state should restore to {initial}");
                Console.Write($"[Toggled & Verified {target.Name}: {initial} -> {!initial} -> {initial}] ");
            }
            else
            {
                // Protected registry item: verify safe non-destructive guardrail
                Assert(vm.StatusMessage.Contains("Administrator privileges may be required"),
                    $"Expected enterprise permission warning, got: {vm.StatusMessage}");
                Console.Write($"[Protected System Guardrail Verified: '{target.Name}' safely requires elevation] ");
            }
        });

        // 8. System Vitals & Hardware Telemetry
        await AuditFeature("System Vitals: Deep CPU, RAM, GPU & Drive Partitions", async () =>
        {
            var vm = new SystemTelemetryViewModel();
            await vm.RefreshTelemetryCommand.ExecuteAsync(null);
            var t = vm.Telemetry;

            Assert(!string.IsNullOrWhiteSpace(t.CpuName), "CPU Name must be detected.");
            Assert(t.CpuUsagePercent > 0 && t.CpuUsagePercent <= 100, $"CPU usage must be 1-100% (got {t.CpuUsagePercent}%)");
            Assert(t.RamTotalBytes > 0, "RAM total must be > 0");
            Assert(t.RamUsedBytes > 0, "RAM used must be > 0");
            Assert(t.Drives.Count > 0, "At least 1 drive volume partition must be detected.");
            Assert(!string.IsNullOrWhiteSpace(t.OsVersionText), "OS Version must be populated.");
            Console.Write($"[CPU: {t.CpuName}, RAM: {t.FormattedRamUsed}/{t.FormattedRamTotal}, Drives: {t.Drives.Count} partitions] ");
        });

        // 9. Privacy Shield & Trace Inspection
        await AuditFeature("Privacy Shield: Footprint Audit & Expandable File Traces", async () =>
        {
            var vm = new PrivacyViewModel();
            await vm.AuditPrivacyCommand.ExecuteAsync(null);
            Assert(vm.Targets.Count >= 3, "Expected at least 3 privacy targets.");
            Console.Write($"({vm.TotalTraceItems} traces detected across {vm.Targets.Count} targets) ");

            var recentTarget = vm.Targets.FirstOrDefault(t => t.Id == "recent_items");
            if (recentTarget != null)
            {
                vm.ToggleExpand(recentTarget);
                Assert(recentTarget.IsExpanded, "Target should expand when toggled.");
                vm.ToggleExpand(recentTarget);
                Assert(!recentTarget.IsExpanded, "Target should collapse when toggled.");
            }

            vm.DeselectAll();
            Assert(vm.Targets.All(t => !t.IsSelected), "All targets should be deselected.");
            vm.SelectAll();
            Assert(vm.Targets.All(t => t.IsSelected), "All targets should be selected.");
        });

        // 10. Background Automation & Windows Task Scheduler
        await AuditFeature("Automation: Windows Task Scheduler & Custom Frequency/Time", () =>
        {
            var vm = new SchedulerViewModel();
            vm.RefreshStatus();
            Console.Write($"[Active={vm.IsScheduleActive}, Next={vm.TaskInfo.NextRunTime}, Status={vm.TaskInfo.Status}] ");

            // Test custom day/time update
            vm.SelectedFrequency = "Weekly";
            vm.SelectedDay = "Sunday";
            vm.SelectedTime = "02:00";
            vm.ApplyScheduleChanges();

            Assert(vm.TaskInfo.ScheduleDetails.Contains("SUN", StringComparison.OrdinalIgnoreCase) ||
                   vm.TaskInfo.ScheduleDetails.Contains("Sunday", StringComparison.OrdinalIgnoreCase) ||
                   vm.TaskInfo.ScheduleDetails.Contains("Weekly", StringComparison.OrdinalIgnoreCase),
                   $"Schedule details should reflect settings (got {vm.TaskInfo.ScheduleDetails})");

            return Task.CompletedTask;
        });

        // 11. User Manual & Knowledge Base
        await AuditFeature("User Manual: 13 English Chapters & Instant Search", () =>
        {
            var vm = new HelpViewModel();
            Assert(vm.FilteredArticles.Count == 13, $"Expected 13 chapters, got {vm.FilteredArticles.Count}");
            Assert(vm.SelectedArticle != null, "An article should be selected by default.");

            // Test search
            vm.SearchQuery = "Manifesto";
            Assert(vm.FilteredArticles.Any(a => a.Id == "safety_manifesto"), "Search for 'Manifesto' should return safety manifesto.");

            vm.SearchQuery = "WHQL";
            Assert(vm.FilteredArticles.Any(a => a.Id == "driver_health"), "Search for 'WHQL' should return driver health.");

            vm.ClearSearch();
            Assert(vm.FilteredArticles.Count == 13, "Clearing search should restore all 13 chapters.");
            Console.Write($"(13 English chapters verified) ");
            return Task.CompletedTask;
        });

        Console.WriteLine();
        Console.WriteLine("--------------------------------------------------------------------------");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(" METICULOUS USER AUDIT COMPLETED: 11 / 11 FEATURE MODULES VERIFIED (100% PASS)");
        Console.ResetColor();
        Console.WriteLine("--------------------------------------------------------------------------");

        return 0;
    }

    private static async Task AuditFeature(string name, Func<Task> test)
    {
        Console.Write($"[AUDIT] {name,-58} ... ");
        try
        {
            await test();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[PASS]");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[FAIL]: {ex.Message}");
            Console.ResetColor();
            throw;
        }
    }
}
