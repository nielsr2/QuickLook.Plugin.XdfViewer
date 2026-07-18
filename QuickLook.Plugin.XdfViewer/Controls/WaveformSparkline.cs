using System.Windows;
using System.Windows.Media;
using QuickLook.Plugin.XdfViewer.Xdf.Model;

namespace QuickLook.Plugin.XdfViewer.Controls
{
    /// <summary>
    /// Lightweight render-only element that draws a min/max envelope as a waveform:
    /// one vertical line per filled bucket spanning [Min[b], Max[b]], autoscaled to
    /// [DataMin, DataMax] with an optional vertical <see cref="Gain"/> and horizontal
    /// gridlines. The only place in the plugin allowed to touch System.Windows.Media directly.
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

        private static readonly Brush DefaultLineBrush = Brushes.SteelBlue;

        private StreamGeometry _cachedGeometry;
        private ChannelEnvelope _geometryEnvelope;
        private double _geometryWidth = double.NaN;
        private double _geometryHeight = double.NaN;
        private double _geometryGain = double.NaN;

        public ChannelEnvelope Envelope { get => (ChannelEnvelope)GetValue(EnvelopeProperty); set => SetValue(EnvelopeProperty, value); }
        public Brush LineBrush { get => (Brush)GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }

        /// <summary>Vertical amplitude zoom about the mid-value (1 = fit, &gt;1 = zoom in, clipped).</summary>
        public double Gain { get => (double)GetValue(GainProperty); set => SetValue(GainProperty, value); }

        /// <summary>When true, draws faint horizontal gridlines (used by the full-window view).</summary>
        public bool ShowGrid { get => (bool)GetValue(ShowGridProperty); set => SetValue(ShowGridProperty, value); }

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
            // No intrinsic size; the element fills whatever space it is given.
            return new Size(
                double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width,
                double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height);
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            InvalidateGeometryCache();
            // A size-only change does not auto-trigger OnRender for a custom-drawn
            // FrameworkElement; the geometry is pixel-absolute, so force a redraw.
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            double width = ActualWidth;
            double height = ActualHeight;
            if (width <= 0 || height <= 0) return;

            if (ShowGrid)
            {
                // Faint horizontal gridlines at fixed fractions of the height. The matching
                // amplitude labels live in the axis panel next to the control.
                var gridBrush = new SolidColorBrush(Color.FromArgb(0x30, 0x88, 0x88, 0x88));
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

            var geometry = GetOrBuildGeometry(envelope, width, height, Gain);
            if (geometry == null) return;

            var pen = new Pen(LineBrush ?? DefaultLineBrush, 1);
            pen.Freeze();
            dc.DrawGeometry(null, pen, geometry);
        }

        private StreamGeometry GetOrBuildGeometry(ChannelEnvelope envelope, double width, double height, double gain)
        {
            if (_cachedGeometry != null
                && ReferenceEquals(_geometryEnvelope, envelope)
                && _geometryWidth == width
                && _geometryHeight == height
                && _geometryGain == gain)
            {
                return _cachedGeometry;
            }

            var built = BuildGeometry(envelope, width, height, gain);

            _cachedGeometry = built;
            _geometryEnvelope = envelope;
            _geometryWidth = width;
            _geometryHeight = height;
            _geometryGain = gain;

            return built;
        }

        private static StreamGeometry BuildGeometry(ChannelEnvelope envelope, double width, double height, double gain)
        {
            var geometry = new StreamGeometry();
            int bucketCount = envelope.BucketCount;
            bool flat = envelope.DataMax <= envelope.DataMin;

            using (var ctx = geometry.Open())
            {
                for (int b = 0; b < bucketCount; b++)
                {
                    if (envelope.Filled == null || b >= envelope.Filled.Length || !envelope.Filled[b])
                    {
                        continue;
                    }

                    double x = MapX(b, bucketCount, width);

                    if (flat)
                    {
                        double midY = height / 2;
                        // Short horizontal tick at mid-height for a flat channel.
                        double halfTick = width > 0 && bucketCount > 1
                            ? width / bucketCount / 2
                            : 1;
                        ctx.BeginFigure(new Point(x - halfTick, midY), false, false);
                        ctx.LineTo(new Point(x + halfTick, midY), true, false);
                        continue;
                    }

                    double yMax = MapYGain(envelope.Max[b], envelope.DataMin, envelope.DataMax, height, gain);
                    double yMin = MapYGain(envelope.Min[b], envelope.DataMin, envelope.DataMax, height, gain);

                    ctx.BeginFigure(new Point(x, yMax), false, false);
                    ctx.LineTo(new Point(x, yMin), true, false);
                }
            }

            geometry.Freeze();
            return geometry;
        }

        /// <summary>
        /// Maps a bucket index to a horizontal pixel position across [0, width].
        /// Pure and WPF-free so it can be unit-tested without a rendering surface.
        /// </summary>
        internal static double MapX(int bucket, int bucketCount, double width)
        {
            if (bucketCount <= 1)
            {
                return 0d;
            }

            return width * bucket / (bucketCount - 1);
        }

        /// <summary>
        /// Maps a data value to a vertical pixel position (inverted: larger = nearer the top).
        /// Equivalent to <see cref="MapYGain"/> with gain 1. Pure/WPF-free for unit testing.
        /// </summary>
        internal static double MapY(double value, double dataMin, double dataMax, double height)
            => MapYGain(value, dataMin, dataMax, height, 1.0);

        /// <summary>
        /// Maps a data value to a vertical pixel position, zooming about the mid-value by
        /// <paramref name="gain"/> (1 = fit the full [min,max] to the height; larger zooms in)
        /// and clamping to [0, height]. Pure/WPF-free so it can be unit-tested.
        /// </summary>
        internal static double MapYGain(double value, double dataMin, double dataMax, double height, double gain)
        {
            if (dataMax <= dataMin)
            {
                return height / 2;
            }

            double center = (dataMin + dataMax) / 2;
            double halfRange = (dataMax - dataMin) / 2;
            double t = gain * (value - center) / halfRange; // +1 => top, -1 => bottom at gain 1
            double y = height / 2 * (1 - t);
            if (y < 0) y = 0;
            if (y > height) y = height;
            return y;
        }
    }
}
