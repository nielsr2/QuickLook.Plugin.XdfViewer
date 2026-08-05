using QuickLook.Plugin.XdfViewer.Xdf;
using QuickLook.Plugin.XdfViewer.Xdf.Model;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    /// <summary>
    /// Recognizes a sample-counter channel (e.g. OpenSignals "nSeq") from its envelope alone: the
    /// value climbs linearly across the recording instead of carrying signal. Pure, no WPF.
    /// </summary>
    public class CounterChannelDetectorTests
    {
        /// <summary>Builds an envelope from per-bucket (min, max) pairs; a null pair leaves the bucket
        /// unfilled, as chunk striding does for most buckets of a long stream.</summary>
        private static ChannelEnvelope Envelope(params float[][] buckets)
        {
            int n = buckets.Length;
            var min = new float[n];
            var max = new float[n];
            var filled = new bool[n];
            float lo = float.MaxValue, hi = float.MinValue;

            for (int b = 0; b < n; b++)
            {
                if (buckets[b] == null) continue;
                min[b] = buckets[b][0];
                max[b] = buckets[b][1];
                filled[b] = true;
                if (min[b] < lo) lo = min[b];
                if (max[b] > hi) hi = max[b];
            }

            return new ChannelEnvelope
            {
                Min = min, Max = max, Filled = filled, BucketCount = n,
                DataMin = lo == float.MaxValue ? 0f : lo,
                DataMax = hi == float.MinValue ? 0f : hi
            };
        }

        /// <summary>A counter: each bucket spans one step of a straight climb, none overlapping.</summary>
        private static ChannelEnvelope Ramp(int buckets, int every = 1)
        {
            var pairs = new float[buckets][];
            for (int b = 0; b < buckets; b++)
                pairs[b] = b % every == 0 ? new[] { (float)b, b + 0.9f } : null;
            return Envelope(pairs);
        }

        [Fact]
        public void IsSampleCounter_NullEnvelope_ReturnsFalse()
        {
            Assert.False(CounterChannelDetector.IsSampleCounter(null));
        }

        [Fact]
        public void IsSampleCounter_EmptyEnvelope_ReturnsFalse()
        {
            var e = Ramp(12);
            e.IsEmpty = true;

            Assert.False(CounterChannelDetector.IsSampleCounter(e));
        }

        [Fact]
        public void IsSampleCounter_FlatEnvelope_ReturnsFalse()
        {
            // A constant channel is IsFlat's business; a counter has to actually climb.
            var pairs = new float[12][];
            for (int b = 0; b < 12; b++) pairs[b] = new[] { 5f, 5f };

            Assert.False(CounterChannelDetector.IsSampleCounter(Envelope(pairs)));
        }

        [Fact]
        public void IsSampleCounter_LinearRamp_ReturnsTrue()
        {
            Assert.True(CounterChannelDetector.IsSampleCounter(Ramp(12)));
        }

        [Fact]
        public void IsSampleCounter_RampWithUnfilledGaps_ReturnsTrue()
        {
            // Only every third chunk is decoded on a long stream, so most buckets stay unfilled —
            // the ones that survive still lie on the ramp's line.
            Assert.True(CounterChannelDetector.IsSampleCounter(Ramp(30, every: 3)));
        }

        [Fact]
        public void IsSampleCounter_NoisyOverlappingBuckets_ReturnsFalse()
        {
            // A real signal swings within each bucket, so consecutive buckets share values.
            var pairs = new float[12][];
            for (int b = 0; b < 12; b++)
                pairs[b] = new[] { -1f + 0.05f * b, 1f + 0.05f * b };

            Assert.False(CounterChannelDetector.IsSampleCounter(Envelope(pairs)));
        }

        [Fact]
        public void IsSampleCounter_FewerThanMinFilledBuckets_ReturnsFalse()
        {
            // Too little evidence: a handful of rising buckets is not yet a counter.
            Assert.False(CounterChannelDetector.IsSampleCounter(Ramp(7)));
        }

        [Fact]
        public void IsSampleCounter_MonotonicNonlinearDrift_ReturnsFalse()
        {
            // Slow physiological drift climbs without ever falling, but not on a straight line.
            var pairs = new float[16][];
            for (int b = 0; b < 16; b++)
            {
                float v = b * b;                       // quadratic: clearly bowed away from the chord
                pairs[b] = new[] { v, v + 0.5f };
            }

            Assert.False(CounterChannelDetector.IsSampleCounter(Envelope(pairs)));
        }

        [Fact]
        public void IsSampleCounter_TinyOverlapWithinTolerance_ReturnsTrue()
        {
            // float32 holds a ~1.5M sample index to ~0.1, so neighbouring buckets can overlap by a
            // hair without meaning the counter went backwards.
            var pairs = new float[12][];
            for (int b = 0; b < 12; b++)
                pairs[b] = new[] { b * 1000f - 0.5f, b * 1000f + 999.5f };

            Assert.True(CounterChannelDetector.IsSampleCounter(Envelope(pairs)));
        }

        [Fact]
        public void IsSampleCounter_SlowSawtooth_ReturnsTrue()
        {
            // Real nSeq channels wrap. As long as the wrap is slower than a bucket it shows up as a
            // clean drop back to the floor between straight climbs, which is still a counter.
            var pairs = new float[24][];
            for (int b = 0; b < 24; b++)
            {
                float v = b % 8;
                pairs[b] = new[] { v, v + 0.9f };
            }

            Assert.True(CounterChannelDetector.IsSampleCounter(Envelope(pairs)));
        }

        [Fact]
        public void IsSampleCounter_WrapsFasterThanABucket_ReturnsFalse()
        {
            // Nothing survives decimation here: each bucket holds several whole wraps, so its min/max
            // spans the counter's entire range — exactly what a full-scale signal looks like. This is
            // the case the envelope simply cannot distinguish, and the detector must not guess.
            var pairs = new float[16][];
            for (int b = 0; b < 16; b++) pairs[b] = new[] { 0f, 15f };

            Assert.False(CounterChannelDetector.IsSampleCounter(Envelope(pairs)));
        }

        [Fact]
        public void IsSampleCounter_ShallowDip_ReturnsFalse()
        {
            // A dip that doesn't return to the floor is a signal changing direction, not a wrap.
            var pairs = new float[16][];
            for (int b = 0; b < 16; b++)
            {
                float v = b < 8 ? b : 14 - b * 0.5f;
                pairs[b] = new[] { v, v + 0.4f };
            }

            Assert.False(CounterChannelDetector.IsSampleCounter(Envelope(pairs)));
        }

        [Fact]
        public void IsSampleCounter_RunTooShortBetweenWraps_ReturnsFalse()
        {
            // Alternating floor/ceiling values drop the full range every other bucket. Each "climb" is
            // two buckets long, which is a square wave, not a counter.
            var pairs = new float[16][];
            for (int b = 0; b < 16; b++)
            {
                float v = b % 2 == 0 ? 0f : 10f;
                pairs[b] = new[] { v, v + 0.4f };
            }

            Assert.False(CounterChannelDetector.IsSampleCounter(Envelope(pairs)));
        }

        [Fact]
        public void IsSampleCounter_DescendingRamp_ReturnsFalse()
        {
            var pairs = new float[12][];
            for (int b = 0; b < 12; b++)
                pairs[b] = new[] { 12f - b, 12.9f - b };

            Assert.False(CounterChannelDetector.IsSampleCounter(Envelope(pairs)));
        }
    }
}
