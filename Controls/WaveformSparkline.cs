using System;
using System.Windows;
using System.Windows.Media;
using QuickLook.Plugin.XdfViewer.Xdf.Model;

namespace QuickLook.Plugin.XdfViewer.Controls
{
    /// <summary>
    /// Render-only element that draws a min/max envelope. Two looks:
    ///  - line mode (default): one vertical line per filled bucket, range-autoscaled — compact,
    ///    used for the tiny inline sparklines.
    ///  - filled mode (<see cref="Filled"/>): a filled band between the per-bucket min and max,
    ///    pivoted about the channel <see cref="ChannelEnvelope.Center"/> baseline and vertically
    ///    zoomable via <see cref="Gain"/> — used for the expanded and full-window views.
    /// The only place in the plugin allowed to touch System.Windows.Media drawing APIs directly.
    /// </summary>
    public class WaveformSparkline : FrameworkElement
    {
        public static readonly DependencyProperty EnvelopeProperty = DependencyProperty.Register(
            nameof(Envelope), typeof(ChannelEnvelope), typeof(WaveformSparkline),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnGeometryInputChanged));

        public static readonly DependencyProperty LineBrushProperty = DependencyProperty.Register(
            nameof(LineBrush), typeof(Brush), typeof(WaveformSparkline),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty GainProperty = DependencyProperty.Register(
            nameof(Gain), typeof(double), typeof(WaveformSparkline),
            new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender, OnGeometryInputChanged));

        public static readonly DependencyProperty ShowGridProperty = DependencyProperty.Register(
            nameof(ShowGrid), typeof(bool), typeof(WaveformSparkline),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty FilledProperty = DependencyProperty.Register(
            nameof(Filled), typeof(bool), typeof(WaveformSparkline),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender, OnGeometryInputChanged));

        public static readonly DependencyProperty VerticalInsetProperty = DependencyProperty.Register(
            nameof(VerticalInset), typeof(double), typeof(WaveformSparkline),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender, OnGeometryInputChanged));

        public static readonly DependencyProperty SampleDotThresholdPxProperty = DependencyProperty.Register(
            nameof(SampleDotThresholdPx), typeof(double), typeof(WaveformSparkline),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender, OnGeometryInputChanged));

        private static readonly Brush DefaultLineBrush = Brushes.SteelBlue;

        private StreamGeometry _cachedGeometry;
        private ChannelEnvelope _geometryEnvelope;
        private double _geometryWidth = double.NaN;
        private double _geometryHeight = double.NaN;
        private double _geometryGain = double.NaN;
        private bool _geometryFilled;
        private double _geometryInset = double.NaN;
        private bool _geometryDots;

        public ChannelEnvelope Envelope { get => (ChannelEnvelope)GetValue(EnvelopeProperty); set => SetValue(EnvelopeProperty, value); }
        public Brush LineBrush { get => (Brush)GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }

        /// <summary>Vertical amplitude zoom about the baseline (1 = fit, &gt;1 = zoom in, clipped). Filled mode only.</summary>
        public double Gain { get => (double)GetValue(GainProperty); set => SetValue(GainProperty, value); }

        /// <summary>When true, draws faint horizontal gridlines (used by the full-window view).</summary>
        public bool ShowGrid { get => (bool)GetValue(ShowGridProperty); set => SetValue(ShowGridProperty, value); }

        /// <summary>When true, draws a filled min/max band (baseline-pivoted) instead of vertical lines.</summary>
        public bool Filled { get => (bool)GetValue(FilledProperty); set => SetValue(FilledProperty, value); }

        /// <summary>Top &amp; bottom padding (px) reserved inside the element in filled mode; the waveform,
        /// gridlines and baseline all sit inside [inset, height-inset]. Filled mode only.</summary>
        public double VerticalInset { get => (double)GetValue(VerticalInsetProperty); set => SetValue(VerticalInsetProperty, value); }

        /// <summary>When &gt; 0 and the zoom reaches at least this many px per bucket (≈1 sample/bucket),
        /// the filled band switches to discrete sample dots + a thin connector. 0 disables it.</summary>
        public double SampleDotThresholdPx { get => (double)GetValue(SampleDotThresholdPxProperty); set => SetValue(SampleDotThresholdPxProperty, value); }

        private static void OnGeometryInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var element = (WaveformSparkline)d;
            element.InvalidateGeometryCache();
            element.InvalidateVisual();
        }

        private void InvalidateGeometryCache()
        {
            _cachedGeometry = null;
            _geometryEnvelope = null;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(
                double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width,
                double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height);
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            InvalidateGeometryCache();
            // A size-only change does not auto-trigger OnRender for a custom-drawn element.
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            double width = ActualWidth;
            double height = ActualHeight;
            if (width <= 0 || height <= 0) return;

            bool filled = Filled;
            double inset = filled ? Clamp0(VerticalInset, height) : 0;

            if (ShowGrid)
            {
                var gridBrush = new SolidColorBrush(Color.FromArgb(0x28, 0x88, 0x88, 0x88));
                gridBrush.Freeze();
                var gridPen = new Pen(gridBrush, 1);
                gridPen.Freeze();
                double top = inset, usable = height - 2 * inset;
                for (int k = 0; k <= 4; k++)
                {
                    double y = top + usable * k / 4.0;
                    dc.DrawLine(gridPen, new Point(0, y), new Point(width, y));
                }
            }

            var envelope = Envelope;
            if (envelope == null || envelope.IsEmpty) return;

            var lineBrush = LineBrush ?? DefaultLineBrush;
            bool dots = filled && UseSampleDots(envelope.BucketCount, width, SampleDotThresholdPx);

            var geometry = GetOrBuildGeometry(envelope, width, height, Gain, filled, inset, dots);
            if (geometry == null) return;

            if (dots)
            {
                // Thin connector through the samples + a dot on each — the visible "sample-accurate" cue.
                var thin = new Pen(lineBrush, 1);
                thin.Freeze();
                dc.DrawGeometry(null, thin, geometry);
                DrawSampleDots(dc, envelope, width, height, Gain, inset, lineBrush);
            }
            else if (filled)
            {
                var pen = new Pen(lineBrush, 1);
                pen.Freeze();
                dc.DrawGeometry(MakeFillBrush(lineBrush), pen, geometry);
            }
            else
            {
                var pen = new Pen(lineBrush, 1);
                pen.Freeze();
                dc.DrawGeometry(null, pen, geometry);
            }
        }

        private static double Clamp0(double inset, double height)
        {
            if (inset < 0 || double.IsNaN(inset)) return 0;
            double maxInset = height / 2 - 1;   // keep a usable band
            if (maxInset < 0) maxInset = 0;
            return inset > maxInset ? maxInset : inset;
        }

        /// <summary>Draws a filled dot at each filled bucket's mid-value (sample-accurate zoom).</summary>
        private static void DrawSampleDots(DrawingContext dc, ChannelEnvelope envelope,
            double width, double height, double gain, double inset, Brush brush)
        {
            int bucketCount = envelope.BucketCount;
            double center = envelope.Center;
            double halfRange = Math.Max(envelope.DataMax - center, center - envelope.DataMin);
            if (halfRange <= 0) halfRange = (envelope.DataMax - envelope.DataMin) / 2.0;
            double pxPerBucket = bucketCount <= 1 ? width : width / (bucketCount - 1);
            double r = Math.Min(3.5, Math.Max(1.5, pxPerBucket / 4.0));

            for (int b = 0; b < bucketCount; b++)
            {
                if (envelope.Filled == null || b >= envelope.Filled.Length || !envelope.Filled[b]) continue;
                double x = MapX(b, bucketCount, width);
                double mid = (envelope.Min[b] + envelope.Max[b]) * 0.5;
                double y = MapYInset(mid, center, halfRange, height, gain, inset);
                dc.DrawEllipse(brush, null, new Point(x, y), r, r);
            }
        }

        private static Brush MakeFillBrush(Brush lineBrush)
        {
            var color = (lineBrush as SolidColorBrush)?.Color ?? ((SolidColorBrush)DefaultLineBrush).Color;
            var fill = new SolidColorBrush(Color.FromArgb(0x66, color.R, color.G, color.B));
            fill.Freeze();
            return fill;
        }

        private StreamGeometry GetOrBuildGeometry(ChannelEnvelope envelope, double width, double height,
            double gain, bool filled, double inset, bool dots)
        {
            if (_cachedGeometry != null
                && ReferenceEquals(_geometryEnvelope, envelope)
                && _geometryWidth == width
                && _geometryHeight == height
                && _geometryGain == gain
                && _geometryFilled == filled
                && _geometryInset == inset
                && _geometryDots == dots)
            {
                return _cachedGeometry;
            }

            StreamGeometry built;
            if (dots) built = BuildDotConnector(envelope, width, height, gain, inset);
            else if (filled) built = BuildFilledGeometry(envelope, width, height, gain, inset);
            else built = BuildLineGeometry(envelope, width, height);

            _cachedGeometry = built;
            _geometryEnvelope = envelope;
            _geometryWidth = width;
            _geometryHeight = height;
            _geometryGain = gain;
            _geometryFilled = filled;
            _geometryInset = inset;
            _geometryDots = dots;
            return built;
        }

        // --- Line mode (compact inline sparklines): range-autoscaled vertical ticks. ---
        private static StreamGeometry BuildLineGeometry(ChannelEnvelope envelope, double width, double height)
        {
            var geometry = new StreamGeometry();
            int bucketCount = envelope.BucketCount;
            bool flat = envelope.DataMax <= envelope.DataMin;

            using (var ctx = geometry.Open())
            {
                for (int b = 0; b < bucketCount; b++)
                {
                    if (envelope.Filled == null || b >= envelope.Filled.Length || !envelope.Filled[b]) continue;
                    double x = MapX(b, bucketCount, width);

                    if (flat)
                    {
                        double midY = height / 2;
                        double halfTick = width > 0 && bucketCount > 1 ? width / bucketCount / 2 : 1;
                        ctx.BeginFigure(new Point(x - halfTick, midY), false, false);
                        ctx.LineTo(new Point(x + halfTick, midY), true, false);
                        continue;
                    }

                    double yMax = MapY(envelope.Max[b], envelope.DataMin, envelope.DataMax, height);
                    double yMin = MapY(envelope.Min[b], envelope.DataMin, envelope.DataMax, height);
                    ctx.BeginFigure(new Point(x, yMax), false, false);
                    ctx.LineTo(new Point(x, yMin), true, false);
                }
            }

            geometry.Freeze();
            return geometry;
        }

        // --- Filled mode: a band between min & max, pivoted about the baseline Center, gain-zoomed. ---
        private static StreamGeometry BuildFilledGeometry(ChannelEnvelope envelope, double width, double height, double gain, double inset)
        {
            var geometry = new StreamGeometry();
            int bucketCount = envelope.BucketCount;
            double center = envelope.Center;
            double halfRange = Math.Max(envelope.DataMax - center, center - envelope.DataMin);
            if (halfRange <= 0) halfRange = (envelope.DataMax - envelope.DataMin) / 2.0;

            using (var ctx = geometry.Open())
            {
                if (halfRange <= 0)
                {
                    // Flat channel: a single horizontal line at mid-height.
                    ctx.BeginFigure(new Point(0, height / 2), false, false);
                    ctx.LineTo(new Point(width, height / 2), true, false);
                }
                else
                {
                    int b = 0;
                    while (b < bucketCount)
                    {
                        if (envelope.Filled == null || b >= envelope.Filled.Length || !envelope.Filled[b]) { b++; continue; }
                        int start = b;
                        while (b < bucketCount && b < envelope.Filled.Length && envelope.Filled[b]) b++;
                        int end = b - 1; // inclusive run [start, end]

                        // Closed, filled figure: top edge (max) left->right, bottom edge (min) right->left.
                        ctx.BeginFigure(new Point(MapX(start, bucketCount, width),
                            MapYInset(envelope.Max[start], center, halfRange, height, gain, inset)), true, true);
                        for (int i = start + 1; i <= end; i++)
                            ctx.LineTo(new Point(MapX(i, bucketCount, width),
                                MapYInset(envelope.Max[i], center, halfRange, height, gain, inset)), true, false);
                        for (int i = end; i >= start; i--)
                            ctx.LineTo(new Point(MapX(i, bucketCount, width),
                                MapYInset(envelope.Min[i], center, halfRange, height, gain, inset)), true, false);
                    }
                }
            }

            geometry.Freeze();
            return geometry;
        }

        // --- Sample-dot mode: a thin connector through each filled bucket's mid-value (dots drawn separately). ---
        private static StreamGeometry BuildDotConnector(ChannelEnvelope envelope, double width, double height, double gain, double inset)
        {
            var geometry = new StreamGeometry();
            int bucketCount = envelope.BucketCount;
            double center = envelope.Center;
            double halfRange = Math.Max(envelope.DataMax - center, center - envelope.DataMin);
            if (halfRange <= 0) halfRange = (envelope.DataMax - envelope.DataMin) / 2.0;

            using (var ctx = geometry.Open())
            {
                bool started = false;
                for (int b = 0; b < bucketCount; b++)
                {
                    if (envelope.Filled == null || b >= envelope.Filled.Length || !envelope.Filled[b]) continue;
                    double x = MapX(b, bucketCount, width);
                    double mid = (envelope.Min[b] + envelope.Max[b]) * 0.5;
                    double y = MapYInset(mid, center, halfRange, height, gain, inset);
                    if (!started) { ctx.BeginFigure(new Point(x, y), false, false); started = true; }
                    else ctx.LineTo(new Point(x, y), true, false);
                }
            }

            geometry.Freeze();
            return geometry;
        }

        /// <summary>True once the horizontal zoom reaches ~1 sample/bucket wide enough to show discrete
        /// samples: pixels-per-bucket ≥ <paramref name="thresholdPx"/> (which must be &gt; 0). Pure/WPF-free.</summary>
        internal static bool UseSampleDots(int bucketCount, double width, double thresholdPx)
        {
            if (thresholdPx <= 0 || bucketCount <= 0 || width <= 0) return false;
            double pxPerBucket = bucketCount <= 1 ? width : width / (bucketCount - 1);
            return pxPerBucket >= thresholdPx;
        }

        /// <summary>
        /// <see cref="MapYPivot"/> confined to a band inset by <paramref name="inset"/> px top and bottom:
        /// values map into [inset, height-inset] instead of [0, height]. Pure/WPF-free.
        /// </summary>
        internal static double MapYInset(double value, double center, double halfRange, double height, double gain, double inset)
        {
            double usable = height - 2 * inset;
            if (usable <= 0) { usable = height; inset = 0; }
            return inset + MapYPivot(value, center, halfRange, usable, gain);
        }

        /// <summary>
        /// Inverse of <see cref="MapYInset"/>: the data value at a vertical pixel position
        /// <paramref name="y"/> (for the hover-crosshair amplitude readout). A flat channel
        /// (<paramref name="halfRange"/> ≤ 0) has no invertible amplitude, so returns
        /// <paramref name="center"/>. Not clamped — a cursor outside the band reads past the data
        /// range, which is fine for a readout. Pure/WPF-free.
        /// </summary>
        internal static double InvMapYInset(double y, double center, double halfRange, double height, double gain, double inset)
        {
            double usable = height - 2 * inset;
            if (usable <= 0) { usable = height; inset = 0; }
            if (halfRange <= 0 || gain <= 0 || usable <= 0) return center;
            double t = 1 - 2 * (y - inset) / usable; // inverse of y = inset + usable/2*(1 - t)
            return center + t * halfRange / gain;
        }

        /// <summary>Maps a bucket index to a horizontal pixel position across [0, width]. Pure/WPF-free.</summary>
        internal static double MapX(int bucket, int bucketCount, double width)
        {
            if (bucketCount <= 1) return 0d;
            return width * bucket / (bucketCount - 1);
        }

        /// <summary>Range-autoscaled vertical map (min->bottom, max->top). Pure/WPF-free.</summary>
        internal static double MapY(double value, double dataMin, double dataMax, double height)
            => MapYGain(value, dataMin, dataMax, height, 1.0);

        /// <summary>Range-centered vertical map with gain (kept for tests). Pure/WPF-free.</summary>
        internal static double MapYGain(double value, double dataMin, double dataMax, double height, double gain)
            => MapYPivot(value, (dataMin + dataMax) / 2.0, (dataMax - dataMin) / 2.0, height, gain);

        /// <summary>
        /// Maps a value to a vertical pixel position, pivoting about <paramref name="center"/> with
        /// half-window <paramref name="halfRange"/>, zoomed by <paramref name="gain"/>, clamped to
        /// [0, height] and inverted (larger = nearer the top). Pure/WPF-free.
        /// </summary>
        internal static double MapYPivot(double value, double center, double halfRange, double height, double gain)
        {
            if (halfRange <= 0) return height / 2;
            double t = gain * (value - center) / halfRange; // +1 => top, -1 => bottom at gain 1
            double y = height / 2 * (1 - t);
            if (y < 0) y = 0;
            if (y > height) y = height;
            return y;
        }
    }
}
