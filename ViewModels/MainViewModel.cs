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
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LuminaCleaner.Interop;
using LuminaCleaner.Models;
using LuminaCleaner.Modules;
using LuminaCleaner.Services;

namespace LuminaCleaner.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IEngineService _engineService;
    private readonly IUpdateService _updateService;
    private UpdateCheckResult? _latestUpdate;
    private CancellationTokenSource? _cts;

    public ObservableCollection<ICleanerModule> Modules { get; } = [];
    public ObservableCollection<LogMessageModel> ActionLogs { get; } = [];

    public AppManagerViewModel AppManager { get; } = new();
    public DriverHealthViewModel DriverHealth { get; } = new();
    public SpaceLensViewModel SpaceLens { get; } = new();
    public StartupViewModel Startup { get; } = new();
    public DuplicatesViewModel Duplicates { get; } = new();
    public SystemTelemetryViewModel Telemetry { get; } = new();
    public PrivacyViewModel Privacy { get; } = new();
    public SchedulerViewModel Scheduler { get; } = new();
    public HelpViewModel Help { get; } = new();

    [ObservableProperty]
    private AppEngineState _state = AppEngineState.Idle;

    [ObservableProperty]
    private string _currentView = "Dashboard";

    [ObservableProperty]
    private long _totalRecoverableBytes;

    [ObservableProperty]
    private string _formattedRecoverableBytes = "0.00 B";

    [ObservableProperty]
    private int _totalFilesAnalyzed;

    // Skipped & Protected Storage Telemetry
    [ObservableProperty]
    private long _totalSkippedBytes;

    [ObservableProperty]
    private string _formattedSkippedBytes = "0.00 B";

    [ObservableProperty]
    private int _totalSkippedFiles;

    [ObservableProperty]
    private string _skippedExplanation = "No files skipped. Run audit to inspect storage targets.";

    [ObservableProperty]
    private long _lifetimeRecoveredBytes;

    [ObservableProperty]
    private string _formattedLifetimeRecovered = "0.00 B";

    [ObservableProperty]
    private int _cleanlinessScore = 100;

    [ObservableProperty]
    private double _gaugeFillRatio; // 0.0 to 1.0 (1.0 = 100%)

    [ObservableProperty]
    private double _cleanProgressPercentage;

    [ObservableProperty]
    private string _currentStatusText = "Ready to audit system storage";

    [ObservableProperty]
    private string _currentModuleName = "Idle";

    [ObservableProperty]
    private bool _isLogDrawerOpen;

    [ObservableProperty]
    private bool _hasScannedOnce;

    [ObservableProperty]
    private ICleanerModule? _selectedModule;

    // In-App Updater Telemetry & State
    [ObservableProperty]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    private bool _isCheckingForUpdate;

    [ObservableProperty]
    private bool _isUpdating;

    [ObservableProperty]
    private double _updateDownloadProgress;

    [ObservableProperty]
    private string _latestVersionText = string.Empty;

    [ObservableProperty]
    private string _updateStatusText = $"You are running Pilgrim Cleaner v{AppInfo.Version}.";

    [ObservableProperty]
    private string _updateButtonText = "Update & Restart";

    public bool IsElevated => NativeMethods.IsRunningAsAdministrator();
    public string ElevationStatusText => IsElevated ? "Administrator Mode" : "Standard User Mode";

    public bool CanScan => State is AppEngineState.Idle or AppEngineState.ReviewReady or AppEngineState.Finished;
    public bool CanClean => State is AppEngineState.ReviewReady && TotalRecoverableBytes > 0;
    public bool CanCancel => State is AppEngineState.Scanning or AppEngineState.Cleaning;
    public bool IsBusy => State is AppEngineState.Scanning or AppEngineState.Cleaning;

    public MainViewModel() : this(new EngineService(), new UpdateService())
    {
    }

    public MainViewModel(IEngineService engineService) : this(engineService, new UpdateService())
    {
    }

    public MainViewModel(IEngineService engineService, IUpdateService updateService)
    {
        _engineService = engineService;
        _updateService = updateService;
        _engineService.LogAppended += OnLogAppended;

        foreach (var mod in _engineService.Modules)
        {
            mod.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName is nameof(ICleanerModule.SelectedSizeBytes) or nameof(ICleanerModule.TotalSizeBytes) or nameof(ICleanerModule.SkippedSizeBytes))
                {
                    RecalculateMetrics();
                }
            };
            Modules.Add(mod);
        }

        SelectedModule = Modules.FirstOrDefault();

        _engineService.Log(LogLevel.Info, "[INITIALIZED]", "Pilgrim Cleaner initialized with Pilgrim Software architecture.");
        _engineService.Log(LogLevel.Info, "[MANIFESTO]", "Strict guardrails active: 0 Registry edits, 0 RAM flushing, 24h temp file buffer.");
        if (IsElevated)
        {
            _engineService.Log(LogLevel.Success, "[SECURITY]", "Running with Administrator privileges. All system modules unlocked.");
        }
        else
        {
            _engineService.Log(LogLevel.Warning, "[SECURITY]", "Running in Standard User mode. Windows Update cache module will run in audit-only.");
        }
    }

    private void OnLogAppended(LogMessageModel log)
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            ActionLogs.Add(log);
            if (ActionLogs.Count > 1500)
            {
                ActionLogs.RemoveAt(0);
            }
        });
    }

    [RelayCommand]
    private void Navigate(string viewName)
    {
        CurrentView = viewName;
        if (viewName == "Applications" && !AppManager.HasLoadedOnce)
        {
            _ = AppManager.RefreshApplicationsCommand.ExecuteAsync(null);
        }
        else if (viewName == "Drivers" && !DriverHealth.HasScannedOnce)
        {
            _ = DriverHealth.ScanDriversCommand.ExecuteAsync(null);
        }
        else if (viewName == "SpaceLens" && SpaceLens.FilteredItems.Count == 0)
        {
            _ = SpaceLens.StartScanCommand.ExecuteAsync(null);
        }
        else if (viewName == "Startup" && Startup.FilteredItems.Count == 0)
        {
            _ = Startup.RefreshStartupItemsCommand.ExecuteAsync(null);
        }
        else if (viewName == "Privacy" && Privacy.Targets.Count == 0)
        {
            _ = Privacy.AuditPrivacyCommand.ExecuteAsync(null);
        }
    }

    [RelayCommand]
    private void ToggleLogDrawer()
    {
        IsLogDrawerOpen = !IsLogDrawerOpen;
    }

    [RelayCommand]
    private void ClearLogs()
    {
        ActionLogs.Clear();
        _engineService.Log(LogLevel.Info, "[LOGS]", "Terminal log buffer cleared.");
    }

    [RelayCommand]
    private void SelectAllModules(object? parameter)
    {
        bool select = true;
        if (parameter is bool b) select = b;
        else if (parameter is string s && bool.TryParse(s, out bool parsed)) select = parsed;

        foreach (var mod in Modules)
        {
            mod.IsEnabled = select;
            mod.SelectAll(select);
        }
        RecalculateMetrics();
    }

    [RelayCommand]
    private async Task StartScanAsync()
    {
        if (IsBusy) return;

        State = AppEngineState.Scanning;
        UpdateCommandStates();

        _cts = new CancellationTokenSource();
        CleanProgressPercentage = 0;
        GaugeFillRatio = 0.0;
        CurrentStatusText = "Auditing system storage...";

        var options = new EngineOptions
        {
            AgeSafetyBuffer = TimeSpan.FromHours(24),
            DryRun = false
        };

        var progress = new Progress<ScanProgressReport>(report =>
        {
            CurrentStatusText = $"Analyzing: {Path.GetFileName(report.CurrentPath)}";
            CurrentModuleName = report.ModuleName;
            TotalFilesAnalyzed = report.ItemsFound;
            TotalRecoverableBytes = report.TotalBytesFound;
            FormattedRecoverableBytes = CleanItemModel.FormatBytes(TotalRecoverableBytes);

            // Audit progress bar smoothly follows 0 to 100%
            CleanProgressPercentage = Math.Clamp(report.Percentage, 0.0, 100.0);
            GaugeFillRatio = CleanProgressPercentage / 100.0;
        });

        try
        {
            var results = await _engineService.ScanAllAsync(options, progress, _cts.Token);
            RecalculateMetrics();

            // When audit finishes, progress and gauge lock at 100%
            CleanProgressPercentage = 100.0;
            GaugeFillRatio = 1.0;
            HasScannedOnce = true;
            State = AppEngineState.ReviewReady;
            CurrentStatusText = $"Audit complete (100%): {FormattedRecoverableBytes} ready to purge. ({FormattedSkippedBytes} safely preserved).";
            CurrentModuleName = "Audit Complete";
        }
        catch (OperationCanceledException)
        {
            State = AppEngineState.Idle;
            CurrentStatusText = "Storage audit was cancelled.";
            CleanProgressPercentage = 0;
            GaugeFillRatio = 0.0;
        }
        catch (Exception ex)
        {
            State = AppEngineState.Idle;
            CurrentStatusText = $"Scan failed: {ex.Message}";
            _engineService.Log(LogLevel.Error, "[ERROR]", ex.Message);
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            UpdateCommandStates();
        }
    }

    [RelayCommand]
    private async Task StartCleanAsync()
    {
        if (State != AppEngineState.ReviewReady || TotalRecoverableBytes == 0) return;

        State = AppEngineState.Cleaning;
        UpdateCommandStates();

        _cts = new CancellationTokenSource();
        CleanProgressPercentage = 0.0;
        CurrentStatusText = "Executing safe purge...";

        var options = new EngineOptions
        {
            AgeSafetyBuffer = TimeSpan.FromHours(24),
            DryRun = false
        };

        var progress = new Progress<CleanProgressReport>(report =>
        {
            CurrentStatusText = $"Purging: {Path.GetFileName(report.CurrentFile)}";
            CurrentModuleName = report.ModuleName;
            CleanProgressPercentage = Math.Clamp(report.Percentage, 0.0, 100.0);
            GaugeFillRatio = CleanProgressPercentage / 100.0;
        });

        try
        {
            var results = await _engineService.CleanSelectedAsync(options, progress, _cts.Token);
            long cleanedBytes = results.Sum(r => r.CleanedBytes);

            LifetimeRecoveredBytes += cleanedBytes;
            FormattedLifetimeRecovered = CleanItemModel.FormatBytes(LifetimeRecoveredBytes);

            State = AppEngineState.Finished;
            CleanProgressPercentage = 100.0;
            GaugeFillRatio = 1.0;
            CurrentStatusText = $"Cleanup finished (100%): Successfully reclaimed {CleanItemModel.FormatBytes(cleanedBytes)} of disk space!";
            CurrentModuleName = "System Optimized";

            RecalculateMetrics();
            CleanlinessScore = 100;
        }
        catch (OperationCanceledException)
        {
            State = AppEngineState.ReviewReady;
            CurrentStatusText = "Cleanup operation was cancelled.";
        }
        catch (Exception ex)
        {
            State = AppEngineState.ReviewReady;
            CurrentStatusText = $"Cleanup encountered an issue: {ex.Message}";
            _engineService.Log(LogLevel.Error, "[ERROR]", ex.Message);
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            UpdateCommandStates();
        }
    }

    [RelayCommand]
    private void CancelOperation()
    {
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            _cts.Cancel();
            CurrentStatusText = "Cancelling operation...";
            _engineService.Log(LogLevel.Warning, "[USER ACTION]", "Cancellation requested by user.");
        }
    }

    private void RecalculateMetrics()
    {
        long totalBytes = 0;
        int totalItems = 0;
        long skippedBytes = 0;
        int skippedItems = 0;

        foreach (var mod in Modules)
        {
            if (mod.IsEnabled)
            {
                totalBytes += mod.SelectedSizeBytes;
                totalItems += mod.SelectedCount;
                skippedBytes += mod.SkippedSizeBytes;
                skippedItems += mod.SkippedCount;
            }
        }

        TotalRecoverableBytes = totalBytes;
        FormattedRecoverableBytes = CleanItemModel.FormatBytes(totalBytes);
        TotalFilesAnalyzed = totalItems;

        TotalSkippedBytes = skippedBytes;
        FormattedSkippedBytes = CleanItemModel.FormatBytes(skippedBytes);
        TotalSkippedFiles = skippedItems;

        if (skippedBytes > 0)
        {
            SkippedExplanation = $"{FormattedSkippedBytes} ({skippedItems:N0} files) preserved by Pilgrim Cleaner guardrails: Files created or modified within the last 24 hours are kept to protect active installers and in-use applications.";
        }
        else
        {
            SkippedExplanation = "All inspected temporary files met the >24h age requirement and are eligible for cleaning.";
        }

        // Cleanliness score
        if (TotalRecoverableBytes == 0)
        {
            CleanlinessScore = 100;
        }
        else
        {
            const double maxScaleBytes = 10.0 * 1024 * 1024 * 1024; // 10 GB
            double penalty = Math.Min(1.0, TotalRecoverableBytes / maxScaleBytes) * 70.0;
            CleanlinessScore = Math.Max(25, (int)Math.Round(100.0 - penalty));
        }
    }

    [RelayCommand]
    public async Task CheckForUpdatesAsync()
    {
        if (IsCheckingForUpdate || IsUpdating) return;

        IsCheckingForUpdate = true;
        UpdateStatusText = "Checking GitHub Releases for updates...";

        try
        {
            var result = await _updateService.CheckForUpdateAsync();
            _latestUpdate = result;

            if (result.IsUpdateAvailable)
            {
                IsUpdateAvailable = true;
                LatestVersionText = result.LatestTag;
                UpdateButtonText = $"Update to {result.LatestTag} & Restart";
                UpdateStatusText = $"New release {result.LatestTag} is available (current: v{result.CurrentVersion}). Click to update and restart.";
                _engineService.Log(LogLevel.Success, "[UPDATER]", $"New release {result.LatestTag} detected on GitHub Releases.");
            }
            else if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                IsUpdateAvailable = false;
                UpdateStatusText = $"Could not check for updates: {result.ErrorMessage}";
                _engineService.Log(LogLevel.Warning, "[UPDATER]", result.ErrorMessage);
            }
            else
            {
                IsUpdateAvailable = false;
                UpdateStatusText = $"Pilgrim Cleaner is up to date (v{AppInfo.Version}).";
                _engineService.Log(LogLevel.Info, "[UPDATER]", $"Running latest version (v{AppInfo.Version}).");
            }
        }
        catch (Exception ex)
        {
            IsUpdateAvailable = false;
            UpdateStatusText = $"Update check failed: {ex.Message}";
            _engineService.Log(LogLevel.Warning, "[UPDATER]", $"Update check error: {ex.Message}");
        }
        finally
        {
            IsCheckingForUpdate = false;
        }
    }

    [RelayCommand]
    public async Task InstallUpdateAsync()
    {
        if (_latestUpdate is null || !IsUpdateAvailable || IsUpdating) return;

        string targetTag = _latestUpdate.LatestTag;
        IsUpdating = true;
        UpdateDownloadProgress = 0;
        UpdateButtonText = $"Downloading {targetTag} (0%)...";
        UpdateStatusText = $"Downloading and verifying {targetTag} installer...";
        _engineService.Log(LogLevel.Info, "[UPDATER]", $"Downloading {UpdateService.InstallerAssetName} for {targetTag}...");

        var progress = new Progress<double>(pct =>
        {
            UpdateDownloadProgress = Math.Round(pct, 0);
            if (pct < 95.0)
            {
                UpdateButtonText = $"Downloading {targetTag} ({pct:0}%)...";
                UpdateStatusText = $"Downloading {targetTag} installer ({pct:0}%)...";
            }
            else
            {
                UpdateButtonText = "Verifying SHA-256 & restarting...";
                UpdateStatusText = "Verifying SHA-256 signature and launching installer...";
            }
        });

        try
        {
            string verifiedInstallerPath = await _updateService.DownloadAndVerifyInstallerAsync(_latestUpdate, progress);
            _engineService.Log(LogLevel.Success, "[UPDATER]", "SHA-256 checksum verified. Launching installer and restarting Pilgrim Cleaner...");

            if (_updateService.LaunchInstallerAndRequestRestart(verifiedInstallerPath, out string? errorMessage))
            {
                Application.Current?.Dispatcher.Invoke(() => Application.Current.Shutdown());
            }
            else
            {
                UpdateButtonText = $"Update to {targetTag} & Restart";
                UpdateStatusText = errorMessage ?? "Update installer could not be started.";
                _engineService.Log(LogLevel.Warning, "[UPDATER]", UpdateStatusText);
            }
        }
        catch (Exception ex)
        {
            UpdateButtonText = $"Update to {targetTag} & Restart";
            UpdateStatusText = $"Update failed: {ex.Message}";
            _engineService.Log(LogLevel.Error, "[UPDATER]", $"Update failed: {ex.Message}");
        }
        finally
        {
            IsUpdating = false;
        }
    }

    private void UpdateCommandStates()
    {
        OnPropertyChanged(nameof(CanScan));
        OnPropertyChanged(nameof(CanClean));
        OnPropertyChanged(nameof(CanCancel));
        OnPropertyChanged(nameof(IsBusy));
        StartScanCommand.NotifyCanExecuteChanged();
        StartCleanCommand.NotifyCanExecuteChanged();
        CancelOperationCommand.NotifyCanExecuteChanged();
    }
}
