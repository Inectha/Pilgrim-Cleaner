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

using System.Windows;
using System.Windows.Input;
using LuminaCleaner.ViewModels;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace LuminaCleaner;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();
        Title = $"Pilgrim Cleaner {AppInfo.ShortVersion}";
        SystemThemeWatcher.Watch(this);
        var vm = new MainViewModel();
        DataContext = vm;

        Loaded += async (_, _) =>
        {
            await vm.CheckForUpdatesCommand.ExecuteAsync(null);
        };
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source)
        {
            for (DependencyObject? current = source; current != null; current = System.Windows.Media.VisualTreeHelper.GetParent(current))
            {
                if (current is System.Windows.Controls.Primitives.ButtonBase)
                {
                    return;
                }
            }
        }

        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            try
            {
                DragMove();
            }
            catch
            {
                // Ignore if mouse was released before DragMove started
            }
        }
    }
}
