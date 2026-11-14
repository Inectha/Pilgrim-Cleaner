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
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LuminaCleaner.Controls;

/// <summary>
/// Renders the Pilgrim Cleaner Material 3 periodic-element ("Pcl") brand icon
/// from the embedded high-resolution asset (Assets/PilgrimIconP.png), with a matching
/// vector fallback when running outside a packaged WPF resource context.
/// </summary>
public class PilgrimLogoControl : FrameworkElement
{
    private static readonly Lazy<BitmapSource?> CachedBrandBitmap = new(LoadBrandBitmap);

    public static readonly DependencyProperty ShowSquircleBackgroundProperty =
        DependencyProperty.Register(
            nameof(ShowSquircleBackground),
            typeof(bool),
            typeof(PilgrimLogoControl),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GlowOpacityProperty =
        DependencyProperty.Register(
            nameof(GlowOpacity),
            typeof(double),
            typeof(PilgrimLogoControl),
            new FrameworkPropertyMetadata(0.65, FrameworkPropertyMetadataOptions.AffectsRender));

    public PilgrimLogoControl()
    {
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
    }

    public bool ShowSquircleBackground
    {
        get => (bool)GetValue(ShowSquircleBackgroundProperty);
        set => SetValue(ShowSquircleBackgroundProperty, value);
    }

    public double GlowOpacity
    {
        get => (double)GetValue(GlowOpacityProperty);
        set => SetValue(GlowOpacityProperty, value);
    }

    private static BitmapSource? LoadBrandBitmap()
    {
        string[] candidateUris =
        [
            "pack://application:,,,/PilgrimCleaner;component/Assets/PilgrimIconP.png",
            "pack://application:,,,/Assets/PilgrimIconP.png"
        ];

        foreach (string uriString in candidateUris)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(uriString, UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                // Try next URI or disk fallback
            }
        }

        try
        {
            string diskPath = Path.Combine(AppContext.BaseDirectory, "Assets", "PilgrimIconP.png");
            if (File.Exists(diskPath))
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(diskPath, UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
        }
        catch
        {
            // Fall back to vector rendering
        }

        return null;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double w = ActualWidth;
        double h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        double size = Math.Min(w, h);
        double offsetX = (w - size) / 2.0;
        double offsetY = (h - size) / 2.0;

        BitmapSource? brandBitmap = CachedBrandBitmap.Value;
        if (brandBitmap != null)
        {
            dc.DrawImage(brandBitmap, new Rect(offsetX, offsetY, size, size));
            return;
        }

        // Vector fallback matching the Material 3 Pcl icon design
        dc.PushTransform(new TranslateTransform(offsetX, offsetY));

        if (ShowSquircleBackground)
        {
            double cornerRadius = size * 0.22;
            var squircleRect = new Rect(size * 0.03, size * 0.03, size * 0.94, size * 0.94);

            var bgBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop((Color)ColorConverter.ConvertFromString("#2575FC"), 0.0),
                    new GradientStop((Color)ColorConverter.ConvertFromString("#0B57D0"), 0.65),
                    new GradientStop((Color)ColorConverter.ConvertFromString("#063996"), 1.0)
                }
            };
            bgBrush.Freeze();
            dc.DrawRoundedRectangle(bgBrush, null, squircleRect, cornerRadius, cornerRadius);
        }

        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var pText = new FormattedText(
            "P",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            size * 0.62,
            Brushes.White,
            dpi);
        dc.DrawText(pText, new Point(size * 0.27, size * 0.12));

        var pillRect = new Rect(size * 0.58, size * 0.66, size * 0.30, size * 0.22);
        var pillBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D3E3FD"));
        pillBrush.Freeze();
        dc.DrawRoundedRectangle(pillBrush, null, pillRect, pillRect.Height / 2.0, pillRect.Height / 2.0);

        var clBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#041E49"));
        clBrush.Freeze();
        var clText = new FormattedText(
            "CL",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            size * 0.13,
            clBrush,
            dpi);
        dc.DrawText(
            clText,
            new Point(
                pillRect.X + (pillRect.Width - clText.Width) / 2.0,
                pillRect.Y + (pillRect.Height - clText.Height) / 2.0));

        dc.Pop();
    }

    /// <summary>
    /// Renders the brand logo to a crisp high-resolution RenderTargetBitmap.
    /// </summary>
    public static RenderTargetBitmap CreateRenderedBitmap(int width, int height, bool showSquircle = true, double glowOpacity = 0.75)
    {
        var control = new PilgrimLogoControl
        {
            Width = width,
            Height = height,
            ShowSquircleBackground = showSquircle,
            GlowOpacity = glowOpacity
        };
        control.Measure(new Size(width, height));
        control.Arrange(new Rect(0, 0, width, height));
        control.UpdateLayout();

        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(control);
        return rtb;
    }
}
