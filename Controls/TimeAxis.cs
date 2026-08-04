using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace QuickLook.Plugin.XdfViewer.Controls
{
    /// <summary>
    /// Draws a horizontal time axis (a top rule, tick marks, and time labels) across the visible
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

            double[] ticks = Ticks(start, end, width);
            string[] labels = Labels(ticks, NiceStep(span, width));

            for (int i = 0; i < ticks.Length; i++)
            {
                double x = width * ((ticks[i] - start) / span);
                dc.DrawLine(pen, new Point(x, 0), new Point(x, 4));

                var ft = new FormattedText(
                    labels[i], CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
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

        /// <summary>
        /// Labels for <paramref name="ticks"/>, at the precision <paramref name="step"/> demands.
        /// The first tick of each whole second is written in full ("1764.002s"); the ones after it
        /// show only the fraction that changed (".004"), which is what keeps sub-second labels
        /// legible — at a 2 ms step every tick would otherwise repeat the same "1764." prefix and
        /// run into its neighbour. A tick that lands in a new second is written in full again, so
        /// crossing a boundary can never read as an ambiguous ".000".
        ///
        /// Precision comes from the step, not from the timestamp's magnitude: deriving it from the
        /// magnitude is what collapsed every tick in a 26 ms window at t≈1764s to a bare "1764s".
        /// Pure/WPF-free.
        /// </summary>
        internal static string[] Labels(double[] ticks, double step)
        {
            if (ticks == null || ticks.Length == 0) return new string[0];

            int decimals = DecimalsFor(step);
            var labels = new string[ticks.Length];

            if (decimals == 0)
            {
                for (int i = 0; i < ticks.Length; i++)
                    labels[i] = ticks[i].ToString("0", CultureInfo.InvariantCulture) + "s";
                return labels;
            }

            // Fixed (not "#") decimals so the fractions stay column-aligned: ".040", never ".04".
            string fmt = "0." + new string('0', decimals);
            double prevWhole = 0;
            bool havePrev = false;

            for (int i = 0; i < ticks.Length; i++)
            {
                string full = ticks[i].ToString(fmt, CultureInfo.InvariantCulture) + "s";
                // Take the whole-second part from the formatted text, not from the raw double:
                // rounding can carry 1764.9999 up to "1765.000", and the prefix has to match
                // what was actually printed or the abbreviation would be wrong.
                int dot = full.IndexOf('.');
                double whole = double.Parse(full.Substring(0, dot), CultureInfo.InvariantCulture);

                labels[i] = havePrev && whole == prevWhole
                    ? full.Substring(dot, full.Length - dot - 1)   // ".004", no repeated unit
                    : full;

                prevWhole = whole;
                havePrev = true;
            }
            return labels;
        }

        /// <summary>Decimal places a tick step needs to render distinctly: 2 ms → 3, 0.5 s → 1,
        /// anything ≥ 1 s → 0. Capped at 9 so a degenerate step cannot produce an absurd label.</summary>
        internal static int DecimalsFor(double step)
        {
            if (double.IsNaN(step) || double.IsInfinity(step) || step >= 1) return 0;
            if (step <= 0) return 0;
            int d = (int)Math.Ceiling(-Math.Log10(step) - 1e-9);
            return d < 0 ? 0 : d > 9 ? 9 : d;
        }
    }
}
