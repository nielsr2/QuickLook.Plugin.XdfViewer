using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace QuickLook.Plugin.XdfViewer.Controls
{
    /// <summary>
    /// Draws a horizontal time axis (a top rule, tick marks, and "N s" labels) across the visible
    /// window [<see cref="StartSeconds"/>, <see cref="EndSeconds"/>]. Sits under the full-window
    /// waveform. When no window is set it falls back to [0, <see cref="DurationSeconds"/>].
    /// </summary>
    public class TimeAxis : FrameworkElement
    {
        public static readonly DependencyProperty DurationSecondsProperty = DependencyProperty.Register(
            nameof(DurationSeconds), typeof(double), typeof(TimeAxis),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty StartSecondsProperty = DependencyProperty.Register(
            nameof(StartSeconds), typeof(double), typeof(TimeAxis),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty EndSecondsProperty = DependencyProperty.Register(
            nameof(EndSeconds), typeof(double), typeof(TimeAxis),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty LabelBrushProperty = DependencyProperty.Register(
            nameof(LabelBrush), typeof(Brush), typeof(TimeAxis),
            new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

        public double DurationSeconds { get => (double)GetValue(DurationSecondsProperty); set => SetValue(DurationSecondsProperty, value); }

        /// <summary>Left edge of the visible window, in seconds.</summary>
        public double StartSeconds { get => (double)GetValue(StartSecondsProperty); set => SetValue(StartSecondsProperty, value); }

        /// <summary>Right edge of the visible window, in seconds. Must exceed StartSeconds to take effect.</summary>
        public double EndSeconds { get => (double)GetValue(EndSecondsProperty); set => SetValue(EndSecondsProperty, value); }

        public Brush LabelBrush { get => (Brush)GetValue(LabelBrushProperty); set => SetValue(LabelBrushProperty, value); }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width, 22);
        }

        protected override void OnRender(DrawingContext dc)
        {
            double width = ActualWidth;
            double height = ActualHeight;
            if (width <= 0 || height <= 0) return;

            double start = StartSeconds, end = EndSeconds;
            if (end <= start) { start = 0; end = DurationSeconds; } // fallback: whole stream
            double span = end - start;
            if (span <= 0) return;

            var brush = LabelBrush ?? Brushes.Gray;
            var pen = new Pen(brush, 1);
            pen.Freeze();

            // Top rule.
            dc.DrawLine(pen, new Point(0, 0), new Point(width, 0));

            var typeface = new Typeface("Segoe UI");
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            foreach (double t in Ticks(start, end, width))
            {
                double x = width * ((t - start) / span);
                dc.DrawLine(pen, new Point(x, 0), new Point(x, 4));

                var ft = new FormattedText(
                    FormatTime(t), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    typeface, 10, brush, dpi);
                double tx = x + 2;
                if (tx + ft.Width > width) tx = width - ft.Width; // keep the last label on-screen
                if (tx < 0) tx = 0;
                dc.DrawText(ft, new Point(tx, 5));
            }
        }

        /// <summary>
        /// The tick times (absolute seconds) across the window [<paramref name="start"/>,
        /// <paramref name="end"/>], aligned to "nice" 1/2/5×10^k steps targeting ~1 tick/90px.
        /// Pure/WPF-free.
        /// </summary>
        internal static double[] Ticks(double start, double end, double width)
        {
            double span = end - start;
            if (span <= 0 || width <= 0) return new double[0];

            double step = NiceStep(span, width);
            if (step <= 0) return new double[0];

            var list = new List<double>();
            double first = Math.Ceiling(start / step - 1e-9) * step;
            for (double t = first; t <= end + step * 1e-9; t += step)
                list.Add(t < start ? start : t);
            return list.ToArray();
        }

        /// <summary>Chooses a "nice" tick interval (1/2/5 × 10^k seconds) targeting ~1 tick per 90px.</summary>
        private static double NiceStep(double span, double width)
        {
            double targetTicks = Math.Max(2.0, width / 90.0);
            double raw = span / targetTicks;
            if (raw <= 0) return span;
            double mag = Math.Pow(10, Math.Floor(Math.Log10(raw)));
            double norm = raw / mag;
            double nice = norm < 1.5 ? 1 : norm < 3.5 ? 2 : norm < 7.5 ? 5 : 10;
            return nice * mag;
        }

        private static string FormatTime(double s)
        {
            double a = Math.Abs(s);
            return a >= 100
                ? s.ToString("0", CultureInfo.InvariantCulture) + "s"
                : a >= 1
                    ? s.ToString("0.##", CultureInfo.InvariantCulture) + "s"
                    : s.ToString("0.###", CultureInfo.InvariantCulture) + "s";
        }
    }
}
