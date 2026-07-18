using System;
using QuickLook.Plugin.XdfViewer.Xdf.Model;

namespace QuickLook.Plugin.XdfViewer.Xdf
{
    internal sealed class EnvelopeDecimator
    {
        private readonly int _channels, _buckets;
        private readonly float[,] _min, _max;
        private readonly bool[,] _filled;

        public EnvelopeDecimator(int channels, int buckets)
        {
            _channels = channels; _buckets = Math.Max(1, buckets);
            _min = new float[_channels, _buckets];
            _max = new float[_channels, _buckets];
            _filled = new bool[_channels, _buckets];
        }

        public static int BucketOf(long globalIndex, long total, int buckets)
        {
            if (buckets <= 1 || total <= 1) return 0;
            long b = globalIndex * (long)buckets / total;
            if (b < 0) return 0;
            if (b >= buckets) return buckets - 1;
            return (int)b;
        }

        public void Add(int channel, int bucket, double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return;
            if (channel < 0 || channel >= _channels || bucket < 0 || bucket >= _buckets) return;
            float f = (float)v;
            if (!_filled[channel, bucket]) { _min[channel, bucket] = f; _max[channel, bucket] = f; _filled[channel, bucket] = true; }
            else { if (f < _min[channel, bucket]) _min[channel, bucket] = f; if (f > _max[channel, bucket]) _max[channel, bucket] = f; }
        }

        public ChannelEnvelope[] Build(bool approximate)
        {
            var result = new ChannelEnvelope[_channels];
            for (int c = 0; c < _channels; c++)
            {
                var min = new float[_buckets]; var max = new float[_buckets]; var filled = new bool[_buckets];
                bool any = false; float dmin = float.MaxValue, dmax = float.MinValue;
                var mids = new System.Collections.Generic.List<float>();
                for (int b = 0; b < _buckets; b++)
                {
                    filled[b] = _filled[c, b];
                    if (filled[b]) { min[b] = _min[c, b]; max[b] = _max[c, b]; any = true;
                        if (min[b] < dmin) dmin = min[b]; if (max[b] > dmax) dmax = max[b];
                        mids.Add((min[b] + max[b]) * 0.5f); }
                }
                // Robust baseline: median of per-bucket mid-values. Unlike (DataMin+DataMax)/2 this
                // isn't skewed by a one-sided spike (e.g. EMG bursts), so vertical zoom pivots on
                // the resting signal rather than dragging it off-centre.
                float center = 0f;
                if (mids.Count > 0) { mids.Sort(); center = mids[mids.Count / 2]; }
                result[c] = new ChannelEnvelope {
                    Min = min, Max = max, Filled = filled, BucketCount = _buckets,
                    DataMin = any ? dmin : 0f, DataMax = any ? dmax : 0f, Center = center,
                    IsEmpty = !any, IsApproximate = approximate
                };
            }
            return result;
        }
    }
}
