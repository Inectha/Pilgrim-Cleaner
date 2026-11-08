using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace LuminaInstaller;

public class Program : Application
{
    [STAThread]
    public static void Main()
    {
        var app = new Program();
        app.Run(new InstallerWindow());
    }
}

public class InstallerWindow : Window
{
    private readonly ProgressBar _progressBar;
    private readonly TextBlock _statusText;
    private readonly Button _actionButton;
    private readonly CheckBox _desktopShortcutCheck;
    private readonly CheckBox _startMenuShortcutCheck;
    private readonly CheckBox _launchCheck;
    private readonly TextBox _targetPathBox;
    private bool _isCompleted = false;

    public InstallerWindow()
    {
        Title = "Pilgrim Cleaner — Setup Wizard";
        Width = 560;
        Height = 440;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(Color.FromRgb(0x07, 0x0D, 0x16));
        Foreground = Brushes.White;
        FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI, sans-serif");

        var mainGrid = new Grid { Margin = new Thickness(24) };
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Body
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer / Buttons

        // --- Header ---
        var headerPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };
        var titleText = new TextBlock
        {
            Text = "Pilgrim Cleaner Setup",
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xE5, 0xFF))
        };
        var subtitleText = new TextBlock
        {
            Text = "Windows Disk Cleaner, App Residue & Driver Health by Pilgrim Software",
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

        // Destination Box
        var destLabel = new TextBlock
        {
            Text = "Installation Folder:",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 6)
        };
        bodyPanel.Children.Add(destLabel);

        string defaultTarget = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Pilgrim Cleaner");
        _targetPathBox = new TextBox
        {
            Text = defaultTarget,
            FontSize = 12,
            Background = new SolidColorBrush(Color.FromRgb(0x0C, 0x15, 0x22)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x1C, 0x2E, 0x46)),
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 0, 0, 16),
            IsReadOnly = true
        };
        bodyPanel.Children.Add(_targetPathBox);

        // Options
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
        _launchCheck = new CheckBox
        {
            Content = "Launch Pilgrim Cleaner when setup finishes",
            IsChecked = true,
            Foreground = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1)),
            Margin = new Thickness(0, 0, 0, 16)
        };
        bodyPanel.Children.Add(_desktopShortcutCheck);
        bodyPanel.Children.Add(_startMenuShortcutCheck);
        bodyPanel.Children.Add(_launchCheck);

        // Status & Progress Bar
        _statusText = new TextBlock
        {
            Text = "Ready to install Pilgrim Cleaner v1.0.0.",
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
            Margin = new Thickness(0, 16, 0, 0)
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

        try
        {
            _statusText.Text = "Stopping running Pilgrim Cleaner instances...";
            _progressBar.Value = 10;
            await Task.Run(() =>
            {
                foreach (var procName in new[] { "PilgrimCleaner", "LuminaCleaner" })
                {
                    foreach (var p in Process.GetProcessesByName(procName))
                    {
                        try { p.Kill(); p.WaitForExit(1000); } catch { }
                    }
                }
            });

            _statusText.Text = "Extracting application package...";
            _progressBar.Value = 30;

            await Task.Run(() =>
            {
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
            });

            _statusText.Text = "Configuring Windows shortcuts...";
            _progressBar.Value = 70;

            await Task.Run(() =>
            {
                string exePath = Path.Combine(targetDir, "PilgrimCleaner.exe");
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    dynamic? shell = Activator.CreateInstance(shellType);
                    if (shell != null)
                    {
                        if (createStartShortcut)
                        {
                            string programsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Pilgrim Cleaner");
                            Directory.CreateDirectory(programsDir);
                            dynamic shortcut = shell.CreateShortcut(Path.Combine(programsDir, "Pilgrim Cleaner.lnk"));
                            shortcut.TargetPath = exePath;
                            shortcut.WorkingDirectory = targetDir;
                            shortcut.Description = "Pilgrim Cleaner - Windows Storage Cleaner, App Manager & Hardware Health";
                            shortcut.Save();
                        }

                        if (createDesktopShortcut)
                        {
                            string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                            dynamic shortcut = shell.CreateShortcut(Path.Combine(desktopDir, "Pilgrim Cleaner.lnk"));
                            shortcut.TargetPath = exePath;
                            shortcut.WorkingDirectory = targetDir;
                            shortcut.Description = "Pilgrim Cleaner - Windows Storage Cleaner, App Manager & Hardware Health";
                            shortcut.Save();
                        }
                    }
                }

                // Register uninstaller in Windows Settings (Add or Remove Programs)
                RegisterUninstaller(targetDir, exePath);
            });

            _progressBar.Value = 100;
            _statusText.Text = "Installation completed successfully!";
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

    private static void RegisterUninstaller(string targetDir, string exePath)
    {
        try
        {
            string uninstallBatch = Path.Combine(targetDir, "Uninstall.bat");
            if (!File.Exists(uninstallBatch))
            {
                string script = $@"@echo off
title Uninstalling Pilgrim Cleaner...
taskkill /f /im PilgrimCleaner.exe 2>nul
taskkill /f /im LuminaCleaner.exe 2>nul
timeout /t 1 /nobreak >nul
reg delete ""HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\PilgrimCleaner"" /f >nul 2>&1
del ""%APPDATA%\Microsoft\Windows\Start Menu\Programs\Pilgrim Cleaner\Pilgrim Cleaner.lnk"" >nul 2>&1
rd ""%APPDATA%\Microsoft\Windows\Start Menu\Programs\Pilgrim Cleaner"" >nul 2>&1
del ""%USERPROFILE%\Desktop\Pilgrim Cleaner.lnk"" >nul 2>&1
cd ""%TEMP%""
start /b """" cmd /c ""timeout /t 2 /nobreak >nul & rd /s /q """"{targetDir}""""""
echo Pilgrim Cleaner was successfully uninstalled.
pause";
                File.WriteAllText(uninstallBatch, script);
            }

            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\PilgrimCleaner");
            if (key != null)
            {
                key.SetValue("DisplayName", "Pilgrim Cleaner");
                key.SetValue("DisplayVersion", "1.0.0");
                key.SetValue("Publisher", "Pilgrim Software");
                key.SetValue("DisplayIcon", exePath);
                key.SetValue("InstallLocation", targetDir);
                key.SetValue("UninstallString", $"\"{uninstallBatch}\"");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }
        catch { }
    }
}
