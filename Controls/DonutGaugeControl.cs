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
using System.Windows;
using System.Windows.Media;

namespace LuminaCleaner.Controls;

public class DonutGaugeControl : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(double),
            typeof(DonutGaugeControl),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PrimaryTextProperty =
        DependencyProperty.Register(
            nameof(PrimaryText),
            typeof(string),
            typeof(DonutGaugeControl),
            new FrameworkPropertyMetadata("0.00 B", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SecondaryTextProperty =
        DependencyProperty.Register(
            nameof(SecondaryText),
            typeof(string),
            typeof(DonutGaugeControl),
            new FrameworkPropertyMetadata("Recoverable Storage", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StatusBadgeTextProperty =
        DependencyProperty.Register(
            nameof(StatusBadgeText),
            typeof(string),
            typeof(DonutGaugeControl),
            new FrameworkPropertyMetadata("Ready to Scan", FrameworkPropertyMetadataOptions.AffectsRender));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string PrimaryText
    {
        get => (string)GetValue(PrimaryTextProperty);
        set => SetValue(PrimaryTextProperty, value);
    }

    public string SecondaryText
    {
        get => (string)GetValue(SecondaryTextProperty);
        set => SetValue(SecondaryTextProperty, value);
    }

    public string StatusBadgeText
    {
        get => (string)GetValue(StatusBadgeTextProperty);
        set => SetValue(StatusBadgeTextProperty, value);
    }

    private readonly Pen _trackPen;
    private readonly Pen _glowPen;
    private readonly Pen _fillPen;
    private readonly Typeface _primaryTypeface;
    private readonly Typeface _secondaryTypeface;
    private readonly Typeface _badgeTypeface;
    private readonly Brush _primaryTextBrush;
    private readonly Brush _secondaryTextBrush;
    private readonly Brush _badgeBgBrush;
    private readonly Brush _badgeTextBrush;

    public DonutGaugeControl()
    {
        // Smooth 60 FPS drawing pens
        _trackPen = new Pen(new SolidColorBrush(Color.FromRgb(22, 34, 50)), 14)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        _trackPen.Freeze();

        var gradient = new LinearGradientBrush
        {
            StartPoint = new Point(0, 1),
            EndPoint = new Point(1, 0)
        };
        gradient.GradientStops.Add(new GradientStop(Color.FromRgb(0, 50, 160), 0.0));   // Pilgrim Deep Blue
        gradient.GradientStops.Add(new GradientStop(Color.FromRgb(0, 200, 230), 0.5));  // Vibrant Teal
        gradient.GradientStops.Add(new GradientStop(Color.FromRgb(0, 229, 255), 1.0));  // Pilgrim Cyan
        gradient.Freeze();

        _fillPen = new Pen(gradient, 14)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        _fillPen.Freeze();

        _glowPen = new Pen(new SolidColorBrush(Color.FromArgb(50, 0, 229, 255)), 26)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        _glowPen.Freeze();

        var fontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI, sans-serif");
        _primaryTypeface = new Typeface(fontFamily, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        _secondaryTypeface = new Typeface(fontFamily, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        _badgeTypeface = new Typeface(fontFamily, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

        _primaryTextBrush = new SolidColorBrush(Colors.White);
        _primaryTextBrush.Freeze();

        _secondaryTextBrush = new SolidColorBrush(Color.FromRgb(148, 163, 184)); // Slate-400
        _secondaryTextBrush.Freeze();

        _badgeBgBrush = new SolidColorBrush(Color.FromArgb(40, 0, 229, 255));
        _badgeBgBrush.Freeze();

        _badgeTextBrush = new SolidColorBrush(Color.FromRgb(0, 229, 255));
        _badgeTextBrush.Freeze();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double width = ActualWidth;
        double height = ActualHeight;
        if (width < 50 || height < 50) return;

        double size = Math.Min(width, height);
        Point center = new(width / 2.0, height / 2.0);
        double radius = (size / 2.0) - 26; // margin for glow and stroke

        const double startAngleDeg = 135.0;
        const double totalSweepDeg = 270.0;

        // 1. Draw track arc
        var trackGeom = CreateArcGeometry(center, radius, startAngleDeg, totalSweepDeg);
        dc.DrawGeometry(null, _trackPen, trackGeom);

        // 2. Draw active arc if value > 0
        double clampedVal = Math.Clamp(Value, 0.0, 1.0);
        if (clampedVal > 0.001)
        {
            double activeSweepDeg = totalSweepDeg * clampedVal;
            var activeGeom = CreateArcGeometry(center, radius, startAngleDeg, activeSweepDeg);

            // Draw soft cyan glow underneath
            dc.DrawGeometry(null, _glowPen, activeGeom);

            // Draw primary gradient arc
            dc.DrawGeometry(null, _fillPen, activeGeom);

            // Draw bright cyan tip particle at current position
            double tipAngleRad = (startAngleDeg + activeSweepDeg) * Math.PI / 180.0;
            Point tipPoint = new(
                center.X + radius * Math.Cos(tipAngleRad),
                center.Y + radius * Math.Sin(tipAngleRad));

            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(0, 229, 255)), null, tipPoint, 4.5, 4.5);
            dc.DrawEllipse(new SolidColorBrush(Colors.White), null, tipPoint, 2.5, 2.5);
        }

        // 3. Render center typography
        // Primary size (e.g. "4.82 GB")
        var primaryFormatted = new FormattedText(
            PrimaryText,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            _primaryTypeface,
            38.0,
            _primaryTextBrush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        Point primaryPos = new(center.X - (primaryFormatted.Width / 2.0), center.Y - (primaryFormatted.Height / 2.0) - 16);
        dc.DrawText(primaryFormatted, primaryPos);

        // Secondary subtitle (e.g. "Recoverable Space")
        var secFormatted = new FormattedText(
            SecondaryText,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            _secondaryTypeface,
            13.0,
            _secondaryTextBrush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        Point secPos = new(center.X - (secFormatted.Width / 2.0), primaryPos.Y + primaryFormatted.Height + 2);
        dc.DrawText(secFormatted, secPos);

        // Status badge pill (e.g. "AUDIT READY")
        if (!string.IsNullOrWhiteSpace(StatusBadgeText))
        {
            var badgeFormatted = new FormattedText(
                StatusBadgeText.ToUpperInvariant(),
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                _badgeTypeface,
                10.5,
                _badgeTextBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            double badgeW = badgeFormatted.Width + 20;
            double badgeH = badgeFormatted.Height + 8;
            Rect badgeRect = new(center.X - (badgeW / 2.0), secPos.Y + secFormatted.Height + 12, badgeW, badgeH);

            dc.DrawRoundedRectangle(_badgeBgBrush, new Pen(_badgeTextBrush, 1.0), badgeRect, 10, 10);
            dc.DrawText(badgeFormatted, new Point(badgeRect.X + 10, badgeRect.Y + 4));
        }
    }

    private static StreamGeometry CreateArcGeometry(Point center, double radius, double startAngleDeg, double sweepAngleDeg)
    {
        var geom = new StreamGeometry();
        using (var ctx = geom.Open())
        {
            double startRad = startAngleDeg * Math.PI / 180.0;
            double endRad = (startAngleDeg + sweepAngleDeg) * Math.PI / 180.0;

            Point startPt = new(
                center.X + radius * Math.Cos(startRad),
                center.Y + radius * Math.Sin(startRad));

            Point endPt = new(
                center.X + radius * Math.Cos(endRad),
                center.Y + radius * Math.Sin(endRad));

            ctx.BeginFigure(startPt, false, false);
            ctx.ArcTo(
                endPt,
                new Size(radius, radius),
                0,
                sweepAngleDeg > 180.0,
                SweepDirection.Clockwise,
                true,
                false);
        }
        geom.Freeze();
        return geom;
    }
}
