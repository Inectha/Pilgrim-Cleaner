using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace LuminaInstaller;

public class Program : Application
{
    public const string SetupVersion = "1.1.0";

    [STAThread]
    public static int Main(string[] args)
    {
        bool silent = args.Any(a =>
        {
            string clean = a.Trim().Trim('"', '\'');
            return clean.Equals("--silent", StringComparison.OrdinalIgnoreCase) ||
                   clean.Equals("-silent", StringComparison.OrdinalIgnoreCase) ||
                   clean.Equals("/silent", StringComparison.OrdinalIgnoreCase) ||
                   clean.Equals("/S", StringComparison.OrdinalIgnoreCase) ||
                   clean.Equals("-s", StringComparison.OrdinalIgnoreCase) ||
                   clean.Equals("/quiet", StringComparison.OrdinalIgnoreCase) ||
                   clean.Equals("--quiet", StringComparison.OrdinalIgnoreCase) ||
                   clean.Equals("/q", StringComparison.OrdinalIgnoreCase) ||
                   clean.Equals("-q", StringComparison.OrdinalIgnoreCase) ||
                   clean.Equals("/qn", StringComparison.OrdinalIgnoreCase) ||
                   clean.Equals("/passive", StringComparison.OrdinalIgnoreCase) ||
                   clean.Equals("/verysilent", StringComparison.OrdinalIgnoreCase) ||
                   clean.StartsWith("/S", StringComparison.OrdinalIgnoreCase) ||
                   clean.StartsWith("--silent", StringComparison.OrdinalIgnoreCase);
        });

        bool isAdmin = IsRunningAsAdministrator();
        string defaultTarget = isAdmin
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Pilgrim Cleaner")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Pilgrim Cleaner");

        if (silent)
        {
            try
            {
                InstallerEngine.InstallPackage(defaultTarget, createStartShortcut: true, createDesktopShortcut: false, addToPath: true, isAdmin: isAdmin);
                return 0;
            }
            catch
            {
                return 1;
            }
        }

        var app = new Program();
        return app.Run(new InstallerWindow(defaultTarget, isAdmin));
    }

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
}

public static class InstallerEngine
{
    public static void InstallPackage(
        string targetDir,
        bool createStartShortcut,
        bool createDesktopShortcut,
        bool addToPath,
        bool isAdmin,
        Action<string, double>? onProgress = null)
    {
        onProgress?.Invoke("Stopping running Pilgrim Cleaner instances...", 10);
        foreach (var procName in new[] { "PilgrimCleaner", "pilgrim", "LuminaCleaner" })
        {
            foreach (var p in Process.GetProcessesByName(procName))
            {
                try { p.Kill(); p.WaitForExit(1000); } catch { }
            }
        }

        onProgress?.Invoke("Extracting application & Sysadmin CLI binaries...", 35);
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream("PilgrimPayload.zip")
            ?? throw new InvalidOperationException("Embedded installer payload not found.");

        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        foreach (var entry in archive.Entries)
        {
            string destPath = Path.Combine(targetDir, entry.FullName);
            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destPath);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                entry.ExtractToFile(destPath, true);
            }
        }

        if (addToPath)
        {
            onProgress?.Invoke("Registering 'pilgrim' CLI in Windows PATH...", 60);
            try
            {
                using var envKey = isAdmin
                    ? Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment", writable: true)
                    : Registry.CurrentUser.OpenSubKey(@"Environment", writable: true);

                if (envKey != null)
                {
                    string currentPath = envKey.GetValue("Path", "", RegistryValueOptions.DoNotExpandEnvironmentNames)?.ToString() ?? string.Empty;
                    var parts = currentPath.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                    if (!parts.Any(p => p.Equals(targetDir, StringComparison.OrdinalIgnoreCase)))
                    {
                        parts.Add(targetDir);
                        envKey.SetValue("Path", string.Join(";", parts), RegistryValueKind.ExpandString);
                    }
                }
            }
            catch { }
        }

        onProgress?.Invoke("Configuring Windows shortcuts & uninstaller...", 80);
        string exePath = Path.Combine(targetDir, "PilgrimCleaner.exe");
        Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType != null)
        {
            dynamic? shell = Activator.CreateInstance(shellType);
            if (shell != null)
            {
                if (createStartShortcut)
                {
                    var folder = isAdmin ? Environment.SpecialFolder.CommonPrograms : Environment.SpecialFolder.Programs;
                    string programsDir = Path.Combine(Environment.GetFolderPath(folder), "Pilgrim Cleaner");
                    Directory.CreateDirectory(programsDir);
                    dynamic shortcut = shell.CreateShortcut(Path.Combine(programsDir, "Pilgrim Cleaner.lnk"));
                    shortcut.TargetPath = exePath;
                    shortcut.WorkingDirectory = targetDir;
                    shortcut.Description = "Pilgrim Cleaner - Windows Storage Cleaner, App Manager, Hardware Health & Sysadmin CLI";
                    shortcut.Save();
                }

                if (createDesktopShortcut)
                {
                    string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    if (!string.IsNullOrWhiteSpace(desktopDir) && Directory.Exists(desktopDir))
                    {
                        dynamic shortcut = shell.CreateShortcut(Path.Combine(desktopDir, "Pilgrim Cleaner.lnk"));
                        shortcut.TargetPath = exePath;
                        shortcut.WorkingDirectory = targetDir;
                        shortcut.Description = "Pilgrim Cleaner - Windows Storage Cleaner, App Manager, Hardware Health & Sysadmin CLI";
                        shortcut.Save();
                    }
                }
            }
        }

        RegisterUninstaller(targetDir, exePath, isAdmin);
        onProgress?.Invoke("Installation completed successfully!", 100);
    }

    private static void RegisterUninstaller(string targetDir, string exePath, bool isAdmin)
    {
        try
        {
            string regRoot = isAdmin ? "HKLM" : "HKCU";
            string uninstallBatch = Path.Combine(targetDir, "Uninstall.bat");
            string script = $@"@echo off
title Uninstalling Pilgrim Cleaner...
taskkill /f /im PilgrimCleaner.exe 2>nul
taskkill /f /im pilgrim.exe 2>nul
taskkill /f /im LuminaCleaner.exe 2>nul
schtasks /delete /tn ""PilgrimCleanerAutoMaintenance"" /f >nul 2>&1
schtasks /delete /tn ""LuminaSuiteAutoMaintenance"" /f >nul 2>&1
timeout /t 1 /nobreak >nul
reg delete ""{regRoot}\Software\Microsoft\Windows\CurrentVersion\Uninstall\PilgrimCleaner"" /f >nul 2>&1
del ""%PROGRAMDATA%\Microsoft\Windows\Start Menu\Programs\Pilgrim Cleaner\Pilgrim Cleaner.lnk"" >nul 2>&1
rd ""%PROGRAMDATA%\Microsoft\Windows\Start Menu\Programs\Pilgrim Cleaner"" >nul 2>&1
del ""%APPDATA%\Microsoft\Windows\Start Menu\Programs\Pilgrim Cleaner\Pilgrim Cleaner.lnk"" >nul 2>&1
rd ""%APPDATA%\Microsoft\Windows\Start Menu\Programs\Pilgrim Cleaner"" >nul 2>&1
del ""%USERPROFILE%\Desktop\Pilgrim Cleaner.lnk"" >nul 2>&1
cd ""%TEMP%""
start /b """" cmd /c ""timeout /t 2 /nobreak >nul & rd /s /q """"{targetDir}""""""
exit /b 0";
            File.WriteAllText(uninstallBatch, script);

            RegistryKey baseKey = isAdmin ? Registry.LocalMachine : Registry.CurrentUser;
            using var key = baseKey.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\PilgrimCleaner");
            if (key != null)
            {
                key.SetValue("DisplayName", "Pilgrim Cleaner");
                key.SetValue("DisplayVersion", Program.SetupVersion);
                key.SetValue("Publisher", "Pilgrim Software");
                key.SetValue("DisplayIcon", exePath);
                key.SetValue("InstallLocation", targetDir);
                key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                key.SetValue("EstimatedSize", 14500, RegistryValueKind.DWord);
                key.SetValue("WindowsInstaller", 0, RegistryValueKind.DWord);
                key.SetValue("UninstallString", $"\"{uninstallBatch}\"");
                key.SetValue("QuietUninstallString", $"\"{uninstallBatch}\" --silent");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }
        catch { }
    }
}

public class InstallerWindow : Window
{
    private readonly ProgressBar _progressBar;
    private readonly TextBlock _statusText;
    private readonly Button _actionButton;
    private readonly CheckBox _desktopShortcutCheck;
    private readonly CheckBox _startMenuShortcutCheck;
    private readonly CheckBox _cliPathCheck;
    private readonly CheckBox _launchCheck;
    private readonly TextBox _targetPathBox;
    private readonly bool _isAdmin;
    private bool _isCompleted = false;

    public InstallerWindow(string defaultTarget, bool isAdmin)
    {
        _isAdmin = isAdmin;
        Title = $"Pilgrim Cleaner v{Program.SetupVersion} — Setup Wizard";
        Width = 580;
        Height = 485;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(Color.FromRgb(0x07, 0x0D, 0x16));
        Foreground = Brushes.White;
        FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI, sans-serif");
        FontSize = 13.5;

        MouseLeftButtonDown += (s, e) =>
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
            {
                try { DragMove(); } catch { }
            }
        };

        var mainGrid = new Grid { Margin = new Thickness(24) };
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Body
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer / Buttons

        // --- Header ---
        var headerPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };
        var titleText = new TextBlock
        {
            Text = $"Pilgrim Cleaner v{Program.SetupVersion} Setup",
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xE5, 0xFF))
        };
        var subtitleText = new TextBlock
        {
            Text = "Windows Disk Cleaner, Server Maintenance & Sysadmin CLI by Pilgrim Software",
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)),
            Margin = new Thickness(0, 4, 0, 0)
        };
        headerPanel.Children.Add(titleText);
        headerPanel.Children.Add(subtitleText);
        Grid.SetRow(headerPanel, 0);
        mainGrid.Children.Add(headerPanel);

        // --- Body ---
        var bodyPanel = new StackPanel();

        var destLabel = new TextBlock
        {
            Text = isAdmin ? "Installation Folder (Machine-Wide / AppLocker Compliant):" : "Installation Folder (Current User):",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 6)
        };
        bodyPanel.Children.Add(destLabel);

        _targetPathBox = new TextBox
        {
            Text = defaultTarget,
            FontSize = 12,
            Background = new SolidColorBrush(Color.FromRgb(0x0C, 0x15, 0x22)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x1C, 0x2E, 0x46)),
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 0, 0, 14),
            IsReadOnly = true
        };
        bodyPanel.Children.Add(_targetPathBox);

        var optionsLabel = new TextBlock
        {
            Text = "Installation Options:",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 8)
        };
        bodyPanel.Children.Add(optionsLabel);

        _desktopShortcutCheck = new CheckBox
        {
            Content = "Create a Desktop shortcut",
            IsChecked = true,
            Foreground = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1)),
            Margin = new Thickness(0, 0, 0, 8)
        };
        _startMenuShortcutCheck = new CheckBox
        {
            Content = "Create a Start Menu shortcut",
            IsChecked = true,
            Foreground = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1)),
            Margin = new Thickness(0, 0, 0, 8)
        };
        _cliPathCheck = new CheckBox
        {
            Content = "Register 'pilgrim' CLI in Windows PATH (for PowerShell / Sysadmins)",
            IsChecked = true,
            Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xE5, 0xFF)),
            Margin = new Thickness(0, 0, 0, 8)
        };
        _launchCheck = new CheckBox
        {
            Content = "Launch Pilgrim Cleaner when setup finishes",
            IsChecked = true,
            Foreground = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1)),
            Margin = new Thickness(0, 0, 0, 14)
        };
        bodyPanel.Children.Add(_desktopShortcutCheck);
        bodyPanel.Children.Add(_startMenuShortcutCheck);
        bodyPanel.Children.Add(_cliPathCheck);
        bodyPanel.Children.Add(_launchCheck);

        _statusText = new TextBlock
        {
            Text = $"Ready to install Pilgrim Cleaner v{Program.SetupVersion} (GUI + Sysadmin CLI).",
            FontSize = 11.5,
            Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)),
            Margin = new Thickness(0, 0, 0, 6)
        };
        _progressBar = new ProgressBar
        {
            Height = 12,
            Value = 0,
            Maximum = 100,
            Background = new SolidColorBrush(Color.FromRgb(0x0C, 0x15, 0x22)),
            Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xE5, 0xFF)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x1C, 0x2E, 0x46)),
            Margin = new Thickness(0, 0, 0, 10)
        };
        bodyPanel.Children.Add(_statusText);
        bodyPanel.Children.Add(_progressBar);

        Grid.SetRow(bodyPanel, 1);
        mainGrid.Children.Add(bodyPanel);

        // --- Footer Buttons ---
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0)
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 90,
            Height = 32,
            Background = new SolidColorBrush(Color.FromRgb(0x12, 0x1D, 0x2C)),
            Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)),
            BorderThickness = new Thickness(0),
            Margin = new Thickness(0, 0, 10, 0)
        };
        cancelButton.Click += (s, e) => Close();

        _actionButton = new Button
        {
            Content = "Install Now",
            Width = 110,
            Height = 32,
            FontWeight = FontWeights.Bold,
            Background = new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xFF)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0)
        };
        _actionButton.Click += async (s, e) =>
        {
            if (_isCompleted)
            {
                if (_launchCheck.IsChecked == true)
                {
                    string exePath = Path.Combine(_targetPathBox.Text, "PilgrimCleaner.exe");
                    if (File.Exists(exePath))
                    {
                        Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
                    }
                }
                Close();
            }
            else
            {
                _actionButton.IsEnabled = false;
                cancelButton.IsEnabled = false;
                await RunInstallationAsync();
            }
        };

        buttonPanel.Children.Add(cancelButton);
        buttonPanel.Children.Add(_actionButton);
        Grid.SetRow(buttonPanel, 2);
        mainGrid.Children.Add(buttonPanel);

        Content = mainGrid;
    }

    private async Task RunInstallationAsync()
    {
        string targetDir = _targetPathBox.Text;
        bool createStartShortcut = _startMenuShortcutCheck.IsChecked == true;
        bool createDesktopShortcut = _desktopShortcutCheck.IsChecked == true;
        bool addToPath = _cliPathCheck.IsChecked == true;

        try
        {
            await Task.Run(() =>
            {
                InstallerEngine.InstallPackage(
                    targetDir,
                    createStartShortcut,
                    createDesktopShortcut,
                    addToPath,
                    _isAdmin,
                    (msg, pct) =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            _statusText.Text = msg;
                            _progressBar.Value = pct;
                        });
                    });
            });

            _progressBar.Value = 100;
            _statusText.Text = "Installation completed! Both GUI and 'pilgrim' CLI are ready.";
            _statusText.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x9D));
            _actionButton.Content = "Finish";
            _actionButton.Background = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x9D));
            _actionButton.Foreground = new SolidColorBrush(Color.FromRgb(0x07, 0x0D, 0x16));
            _actionButton.IsEnabled = true;
            _isCompleted = true;
        }
        catch (Exception ex)
        {
            _statusText.Text = "Installation error: " + ex.Message;
            _statusText.Foreground = Brushes.Red;
            _actionButton.Content = "Close";
            _actionButton.IsEnabled = true;
            _isCompleted = true;
        }
    }
}
