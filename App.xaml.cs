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

using System.IO;
using System.Windows;
using Wpf.Ui.Appearance;

namespace LuminaCleaner;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            try
            {
                File.WriteAllText("crash.log", args.ExceptionObject?.ToString());
            }
            catch { }
        };

        DispatcherUnhandledException += (s, args) =>
        {
            try
            {
                File.WriteAllText("crash.log", args.Exception?.ToString());
            }
            catch { }
        };

        ApplicationThemeManager.Apply(ApplicationTheme.Dark);

        if (e.Args.Contains("--silent-audit"))
        {
            Task.Run(async () =>
            {
                var engine = new Services.EngineService();
                var options = new Models.EngineOptions();
                var progress = new Progress<Models.ScanProgressReport>();
                var results = await engine.ScanAllAsync(options, progress, System.Threading.CancellationToken.None);
                long bytes = results.Sum(r => r.TotalBytes);
                if (bytes > 500L * 1024 * 1024)
                {
                    new Services.SchedulerService().ShowWindowsNotification(
                        "Pilgrim Cleaner - System Storage Alert",
                        $"Automatic audit discovered {bytes / (1024.0 * 1024.0 * 1024.0):n1} GB of safe recoverable cache files.");
                }
                Environment.Exit(0);
            });
            return;
        }
    }
}
