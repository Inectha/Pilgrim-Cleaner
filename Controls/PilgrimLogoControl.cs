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
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LuminaCleaner.Controls;

/// <summary>
/// High-contrast, DPI-independent mathematical vector logo for Pilgrim Software / Lumina Suite.
/// Renders the iconic stylized 'P' with a complete vertical pillar and ascending forward arrow.
/// </summary>
public class PilgrimLogoControl : FrameworkElement
{
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

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double w = ActualWidth;
        double h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        double size = Math.Min(w, h);
        double offsetX = (w - size) / 2.0;
        double offsetY = (h - size) / 2.0;

        // Push coordinate transform to center square
        dc.PushTransform(new TranslateTransform(offsetX, offsetY));

        // 1. Draw Glass Squircle Backdrop (if enabled)
        if (ShowSquircleBackground)
        {
            double cornerRadius = size * 0.22;
            var squircleRect = new Rect(size * 0.03, size * 0.03, size * 0.94, size * 0.94);

            // Ambient outer glow
            var ambientGlowBrush = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.4),
                GradientOrigin = new Point(0.5, 0.4),
                RadiusX = 0.65,
                RadiusY = 0.65,
                GradientStops = new GradientStopCollection
            {
                new GradientStop(Color.FromArgb((byte)(70 * Math.Clamp(GlowOpacity, 0.0, 1.0)), 0, 229, 255), 0.0),
                new GradientStop(Color.FromArgb(0, 0, 40, 140), 1.0)
            }
            };
            dc.DrawRoundedRectangle(ambientGlowBrush, null, squircleRect, cornerRadius, cornerRadius);

            // Squircle body: Deep obsidian to deep midnight blue
            var bgBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops = new GradientStopCollection
            {
                new GradientStop((Color)ColorConverter.ConvertFromString("#101C2E"), 0.0),
                new GradientStop((Color)ColorConverter.ConvertFromString("#080F1A"), 0.55),
                new GradientStop((Color)ColorConverter.ConvertFromString("#040810"), 1.0)
            }
            };

            var borderPen = new Pen
            {
                Brush = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1),
                    GradientStops = new GradientStopCollection
                {
                    new GradientStop((Color)ColorConverter.ConvertFromString("#00E5FF"), 0.0),
                    new GradientStop((Color)ColorConverter.ConvertFromString("#0077B6"), 0.5),
                    new GradientStop((Color)ColorConverter.ConvertFromString("#002875"), 1.0)
                }
                },
                Thickness = Math.Max(1.0, size * 0.032)
            };
            borderPen.Freeze();

            dc.DrawRoundedRectangle(bgBrush, borderPen, squircleRect, cornerRadius, cornerRadius);
        }

        // 2. Subtle Navigation Compass Radar Ring (Futuristic Navigator Accent)
        var ringPen = new Pen(new SolidColorBrush(Color.FromArgb(40, 0, 229, 255)), Math.Max(0.75, size * 0.012));
        ringPen.Freeze();
        Point compassCenter = new Point(size * 0.54, size * 0.46);
        dc.DrawEllipse(null, ringPen, compassCenter, size * 0.24, size * 0.24);

        // Cardinal tick marks
        var tickPen = new Pen(new SolidColorBrush(Color.FromArgb(60, 0, 229, 255)), Math.Max(0.75, size * 0.014));
        tickPen.Freeze();
        dc.DrawLine(tickPen, new Point(compassCenter.X, compassCenter.Y - size * 0.28), new Point(compassCenter.X, compassCenter.Y - size * 0.22));
        dc.DrawLine(tickPen, new Point(compassCenter.X + size * 0.22, compassCenter.Y), new Point(compassCenter.X + size * 0.28, compassCenter.Y));

        // 3. The Vertical Stem / Pillar of the "P" (Solid, ground-anchored, full height)
        double stemLeft = size * 0.20;
        double stemTop = size * 0.16;
        double stemWidth = size * 0.18;
        double stemHeight = size * 0.68; // Goes down to 0.84, completely visible with pill caps
        double stemRadius = stemWidth / 2.0;

        var stemBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
            GradientStops = new GradientStopCollection
        {
            new GradientStop((Color)ColorConverter.ConvertFromString("#7DF9FF"), 0.0), // Electric Cyan glow
            new GradientStop((Color)ColorConverter.ConvertFromString("#00D2FF"), 0.30),
            new GradientStop((Color)ColorConverter.ConvertFromString("#0072B2"), 0.65),
            new GradientStop((Color)ColorConverter.ConvertFromString("#002875"), 1.0) // Deep Pilgrim Blue
        }
        };

        var stemBorderPen = new Pen(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00E5FF")), Math.Max(0.75, size * 0.015));
        stemBorderPen.Freeze();
        dc.DrawRoundedRectangle(stemBrush, stemBorderPen, new Rect(stemLeft, stemTop, stemWidth, stemHeight), stemRadius, stemRadius);

        // 4. The Loop of the "P" (Bold, aerodynamic sweep)
        var loopGeometry = new StreamGeometry();
        using (var ctx = loopGeometry.Open())
        {
            // Outer contour
            ctx.BeginFigure(new Point(stemLeft + stemWidth * 0.4, size * 0.16), isFilled: true, isClosed: true);

            // Top curve
            ctx.BezierTo(
                new Point(size * 0.60, size * 0.14),
                new Point(size * 0.84, size * 0.22),
                new Point(size * 0.84, size * 0.42),
                isStroked: true, isSmoothJoin: true);

            // Bottom curve returning to stem
            ctx.BezierTo(
                new Point(size * 0.84, size * 0.56),
                new Point(size * 0.62, size * 0.60),
                new Point(stemLeft + stemWidth, size * 0.56),
                isStroked: true, isSmoothJoin: true);

            // Straight connect to inner cutout
            ctx.LineTo(new Point(stemLeft + stemWidth, size * 0.44), isStroked: true, isSmoothJoin: true);

            // Inner cutout (counter) curve
            ctx.BezierTo(
                new Point(size * 0.62, size * 0.46),
                new Point(size * 0.68, size * 0.38),
                new Point(size * 0.64, size * 0.30),
                isStroked: true, isSmoothJoin: true);

            ctx.BezierTo(
                new Point(size * 0.58, size * 0.24),
                new Point(stemLeft + stemWidth * 0.8, size * 0.24),
                new Point(stemLeft + stemWidth * 0.4, size * 0.16),
                isStroked: true, isSmoothJoin: true);
        }
        loopGeometry.Freeze();

        var loopBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
            GradientStops = new GradientStopCollection
        {
            new GradientStop((Color)ColorConverter.ConvertFromString("#00F0FF"), 0.0),
            new GradientStop((Color)ColorConverter.ConvertFromString("#00A6E0"), 0.35),
            new GradientStop((Color)ColorConverter.ConvertFromString("#005DB3"), 0.70),
            new GradientStop((Color)ColorConverter.ConvertFromString("#002875"), 1.0)
        }
        };
        dc.DrawGeometry(loopBrush, stemBorderPen, loopGeometry);

        // 5. The Dynamic Forward Arrow (Ascending up-right through the loop)
        var arrowGeometry = new StreamGeometry();
        using (var ctx = arrowGeometry.Open())
        {
            // Tail starting dynamically from stem interior
            ctx.BeginFigure(new Point(stemLeft + stemWidth * 0.65, size * 0.60), isFilled: true, isClosed: true);

            // Sweeping curve upward through center of loop
            ctx.BezierTo(
                new Point(size * 0.40, size * 0.48),
                new Point(size * 0.50, size * 0.36),
                new Point(size * 0.64, size * 0.25),
                isStroked: true, isSmoothJoin: true);

            // Arrow head - left barb
            ctx.LineTo(new Point(size * 0.58, size * 0.21), isStroked: true, isSmoothJoin: true);

            // Arrow head - apex (pointing top-right at 45 degrees, breaking outward)
            ctx.LineTo(new Point(size * 0.80, size * 0.15), isStroked: true, isSmoothJoin: true);

            // Arrow head - right barb
            ctx.LineTo(new Point(size * 0.75, size * 0.36), isStroked: true, isSmoothJoin: true);

            // Arrow head - right notch
            ctx.LineTo(new Point(size * 0.69, size * 0.29), isStroked: true, isSmoothJoin: true);

            // Inner curve back to origin
            ctx.BezierTo(
                new Point(size * 0.56, size * 0.38),
                new Point(size * 0.48, size * 0.51),
                new Point(stemLeft + stemWidth * 0.65, size * 0.60),
                isStroked: true, isSmoothJoin: true);
        }
        arrowGeometry.Freeze();

        var arrowBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 1),
            EndPoint = new Point(1, 0),
            GradientStops = new GradientStopCollection
        {
            new GradientStop((Color)ColorConverter.ConvertFromString("#0050C8"), 0.0),
            new GradientStop((Color)ColorConverter.ConvertFromString("#00A2E8"), 0.35),
            new GradientStop((Color)ColorConverter.ConvertFromString("#00F5FF"), 0.75),
            new GradientStop((Color)ColorConverter.ConvertFromString("#FFFFFF"), 1.0) // Pure glowing apex
        }
        };

        var arrowPen = new Pen(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF")), Math.Max(0.75, size * 0.016));
        arrowPen.Freeze();
        dc.DrawGeometry(arrowBrush, arrowPen, arrowGeometry);

        // 6. Navigation Spark Highlight at arrow origin
        var sparkPen = new Pen(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#80F2FF")), Math.Max(0.75, size * 0.015));
        sparkPen.Freeze();
        double sparkX = size * 0.52;
        double sparkY = size * 0.46;
        double sparkR = size * 0.045;
        dc.DrawLine(sparkPen, new Point(sparkX - sparkR, sparkY), new Point(sparkX + sparkR, sparkY));
        dc.DrawLine(sparkPen, new Point(sparkX, sparkY - sparkR), new Point(sparkX, sparkY + sparkR));
        dc.DrawEllipse(new SolidColorBrush(Colors.White), null, new Point(sparkX, sparkY), size * 0.016, size * 0.016);

        // Pop coordinate transform
        dc.Pop();
    }

    /// <summary>
    /// Renders the mathematical vector logo to a crisp high-resolution RenderTargetBitmap.
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
