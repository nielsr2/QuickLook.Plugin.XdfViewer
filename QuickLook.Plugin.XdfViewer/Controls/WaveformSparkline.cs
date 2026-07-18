using System.Windows;
using System.Windows.Media;
using QuickLook.Plugin.XdfViewer.Xdf.Model;

namespace QuickLook.Plugin.XdfViewer.Controls
{
    /// <summary>
    /// Lightweight render-only element that draws a min/max envelope as a sparkline:
    /// one vertical line per filled bucket spanning [Min[b], Max[b]], autoscaled to
    /// [DataMin, DataMax]. This is the only place in the plugin allowed to touch
    /// System.Windows.Media drawing APIs directly.
    /// </summary>
    public class WaveformSparkline : FrameworkElement
    {
        public static readonly DependencyProperty EnvelopeProperty = DependencyProperty.Register(
            nameof(Envelope),
            typeof(ChannelEnvelope),
            typeof(WaveformSparkline),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnEnvelopeChanged));

        public static readonly DependencyProperty LineBrushProperty = DependencyProperty.Register(
            nameof(LineBrush),
            typeof(Brush),
            typeof(WaveformSparkline),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        private static readonly Brush DefaultLineBrush = Brushes.SteelBlue;

        private StreamGeometry _cachedGeometry;
        private ChannelEnvelope _geometryEnvelope;
        private double _geometryWidth = double.NaN;
        private double _geometryHeight = double.NaN;

        public ChannelEnvelope Envelope
        {
            get => (ChannelEnvelope)GetValue(EnvelopeProperty);
            set => SetValue(EnvelopeProperty, value);
        }

        public Brush LineBrush
        {
            get => (Brush)GetValue(LineBrushProperty);
            set => SetValue(LineBrushProperty, value);
        }

        private static void OnEnvelopeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
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
            var envelope = Envelope;
            if (envelope == null || envelope.IsEmpty)
            {
                return;
            }

            var geometry = GetOrBuildGeometry(envelope);
            if (geometry == null)
            {
                return;
            }

            var brush = LineBrush ?? DefaultLineBrush;
            var pen = new Pen(brush, 1);
            pen.Freeze();

            dc.DrawGeometry(null, pen, geometry);
        }

        private StreamGeometry GetOrBuildGeometry(ChannelEnvelope envelope)
        {
            double width = ActualWidth;
            double height = ActualHeight;

            if (width <= 0 || height <= 0)
            {
                return null;
            }

            if (_cachedGeometry != null
                && ReferenceEquals(_geometryEnvelope, envelope)
                && _geometryWidth == width
                && _geometryHeight == height)
            {
                return _cachedGeometry;
            }

            var built = BuildGeometry(envelope, width, height);

            _cachedGeometry = built;
            _geometryEnvelope = envelope;
            _geometryWidth = width;
            _geometryHeight = height;

            return built;
        }

        private static StreamGeometry BuildGeometry(ChannelEnvelope envelope, double width, double height)
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

                    double yMax = MapY(envelope.Max[b], envelope.DataMin, envelope.DataMax, height);
                    double yMin = MapY(envelope.Min[b], envelope.DataMin, envelope.DataMax, height);

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
        /// Maps a data value to a vertical pixel position, inverted so larger values
        /// render nearer the top (y=0) and smaller values nearer the bottom (y=height).
        /// Pure and WPF-free so it can be unit-tested without a rendering surface.
        /// </summary>
        internal static double MapY(double value, double dataMin, double dataMax, double height)
        {
            if (dataMax <= dataMin)
            {
                return height / 2;
            }

            double t = (value - dataMin) / (dataMax - dataMin);
            if (t < 0) t = 0;
            if (t > 1) t = 1;

            return height * (1 - t);
        }
    }
}
