using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace QuickLook.Plugin.XdfViewer.Controls
{
    /// <summary>
    /// Draws a simple horizontal time axis (a top rule, tick marks, and "N s" labels) spanning
    /// [0, DurationSeconds] across its width. Sits under the full-window waveform and scrolls with it.
    /// </summary>
    public class TimeAxis : FrameworkElement
    {
        public static readonly DependencyProperty DurationSecondsProperty = DependencyProperty.Register(
            nameof(DurationSeconds), typeof(double), typeof(TimeAxis),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty LabelBrushProperty = DependencyProperty.Register(
            nameof(LabelBrush), typeof(Brush), typeof(TimeAxis),
            new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

        public double DurationSeconds { get => (double)GetValue(DurationSecondsProperty); set => SetValue(DurationSecondsProperty, value); }
        public Brush LabelBrush { get => (Brush)GetValue(LabelBrushProperty); set => SetValue(LabelBrushProperty, value); }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width, 22);
        }

        protected override void OnRender(DrawingContext dc)
        {
            double width = ActualWidth;
            double height = ActualHeight;
            double dur = DurationSeconds;
            if (width <= 0 || height <= 0 || dur <= 0) return;

            var brush = LabelBrush ?? Brushes.Gray;
            var pen = new Pen(brush, 1) { };
            pen.Freeze();

            // Top rule.
            dc.DrawLine(pen, new Point(0, 0), new Point(width, 0));

            double step = NiceStep(dur, width);
            var typeface = new Typeface("Segoe UI");
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            for (double t = 0; t <= dur + step * 0.001; t += step)
            {
                double tt = t > dur ? dur : t;
                double x = width * (tt / dur);
                dc.DrawLine(pen, new Point(x, 0), new Point(x, 4));

                var ft = new FormattedText(
                    FormatTime(tt), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    typeface, 10, brush, dpi);
                double tx = x + 2;
                if (tx + ft.Width > width) tx = width - ft.Width; // keep the last label on-screen
                if (tx < 0) tx = 0;
                dc.DrawText(ft, new Point(tx, 5));
            }
        }

        /// <summary>Chooses a "nice" tick interval (1/2/5 × 10^k seconds) targeting ~1 tick per 90px.</summary>
        private static double NiceStep(double dur, double width)
        {
            double targetTicks = Math.Max(2.0, width / 90.0);
            double raw = dur / targetTicks;
            if (raw <= 0) return dur;
            double mag = Math.Pow(10, Math.Floor(Math.Log10(raw)));
            double norm = raw / mag;
            double nice = norm < 1.5 ? 1 : norm < 3.5 ? 2 : norm < 7.5 ? 5 : 10;
            return nice * mag;
        }

        private static string FormatTime(double s)
        {
            return s >= 100
                ? s.ToString("0", CultureInfo.InvariantCulture) + "s"
                : s.ToString("0.##", CultureInfo.InvariantCulture) + "s";
        }
    }
}
