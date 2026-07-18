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

        private static readonly Brush DefaultLineBrush = Brushes.SteelBlue;

        private StreamGeometry _cachedGeometry;
        private ChannelEnvelope _geometryEnvelope;
        private double _geometryWidth = double.NaN;
        private double _geometryHeight = double.NaN;
        private double _geometryGain = double.NaN;
        private bool _geometryFilled;

        public ChannelEnvelope Envelope { get => (ChannelEnvelope)GetValue(EnvelopeProperty); set => SetValue(EnvelopeProperty, value); }
        public Brush LineBrush { get => (Brush)GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }

        /// <summary>Vertical amplitude zoom about the baseline (1 = fit, &gt;1 = zoom in, clipped). Filled mode only.</summary>
        public double Gain { get => (double)GetValue(GainProperty); set => SetValue(GainProperty, value); }

        /// <summary>When true, draws faint horizontal gridlines (used by the full-window view).</summary>
        public bool ShowGrid { get => (bool)GetValue(ShowGridProperty); set => SetValue(ShowGridProperty, value); }

        /// <summary>When true, draws a filled min/max band (baseline-pivoted) instead of vertical lines.</summary>
        public bool Filled { get => (bool)GetValue(FilledProperty); set => SetValue(FilledProperty, value); }

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

            if (ShowGrid)
            {
                var gridBrush = new SolidColorBrush(Color.FromArgb(0x28, 0x88, 0x88, 0x88));
                gridBrush.Freeze();
                var gridPen = new Pen(gridBrush, 1);
                gridPen.Freeze();
                for (int k = 0; k <= 4; k++)
                {
                    double y = height * k / 4.0;
                    dc.DrawLine(gridPen, new Point(0, y), new Point(width, y));
                }
            }

            var envelope = Envelope;
            if (envelope == null || envelope.IsEmpty) return;

            bool filled = Filled;
            var geometry = GetOrBuildGeometry(envelope, width, height, Gain, filled);
            if (geometry == null) return;

            var lineBrush = LineBrush ?? DefaultLineBrush;
            var pen = new Pen(lineBrush, 1);
            pen.Freeze();

            if (filled)
            {
                dc.DrawGeometry(MakeFillBrush(lineBrush), pen, geometry);
            }
            else
            {
                dc.DrawGeometry(null, pen, geometry);
            }
        }

        private static Brush MakeFillBrush(Brush lineBrush)
        {
            var color = (lineBrush as SolidColorBrush)?.Color ?? ((SolidColorBrush)DefaultLineBrush).Color;
            var fill = new SolidColorBrush(Color.FromArgb(0x66, color.R, color.G, color.B));
            fill.Freeze();
            return fill;
        }

        private StreamGeometry GetOrBuildGeometry(ChannelEnvelope envelope, double width, double height, double gain, bool filled)
        {
            if (_cachedGeometry != null
                && ReferenceEquals(_geometryEnvelope, envelope)
                && _geometryWidth == width
                && _geometryHeight == height
                && _geometryGain == gain
                && _geometryFilled == filled)
            {
                return _cachedGeometry;
            }

            var built = filled
                ? BuildFilledGeometry(envelope, width, height, gain)
                : BuildLineGeometry(envelope, width, height);

            _cachedGeometry = built;
            _geometryEnvelope = envelope;
            _geometryWidth = width;
            _geometryHeight = height;
            _geometryGain = gain;
            _geometryFilled = filled;
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
        private static StreamGeometry BuildFilledGeometry(ChannelEnvelope envelope, double width, double height, double gain)
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
                            MapYPivot(envelope.Max[start], center, halfRange, height, gain)), true, true);
                        for (int i = start + 1; i <= end; i++)
                            ctx.LineTo(new Point(MapX(i, bucketCount, width),
                                MapYPivot(envelope.Max[i], center, halfRange, height, gain)), true, false);
                        for (int i = end; i >= start; i--)
                            ctx.LineTo(new Point(MapX(i, bucketCount, width),
                                MapYPivot(envelope.Min[i], center, halfRange, height, gain)), true, false);
                    }
                }
            }

            geometry.Freeze();
            return geometry;
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
