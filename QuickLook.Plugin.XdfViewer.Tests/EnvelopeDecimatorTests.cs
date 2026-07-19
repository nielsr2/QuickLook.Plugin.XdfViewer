using System;
using QuickLook.Plugin.XdfViewer.Xdf;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    public class EnvelopeDecimatorTests
    {
        [Fact]
        public void BucketOf_FirstIndex_IsBucketZero()
        {
            Assert.Equal(0, EnvelopeDecimator.BucketOf(0, 1000, 64));
        }

        [Fact]
        public void BucketOf_LastIndex_IsLastBucket()
        {
            long total = 1000;
            int buckets = 64;
            Assert.Equal(buckets - 1, EnvelopeDecimator.BucketOf(total - 1, total, buckets));
        }

        [Fact]
        public void BucketOf_IndexAtOrAboveTotal_ClampsToLastBucket()
        {
            long total = 1000;
            int buckets = 64;
            Assert.Equal(buckets - 1, EnvelopeDecimator.BucketOf(total, total, buckets));
            Assert.Equal(buckets - 1, EnvelopeDecimator.BucketOf(total + 500, total, buckets));
        }

        [Fact]
        public void BucketOf_TotalIsZero_ReturnsZero()
        {
            Assert.Equal(0, EnvelopeDecimator.BucketOf(0, 0, 64));
        }

        [Fact]
        public void BucketOf_TotalIsOne_ReturnsZero()
        {
            Assert.Equal(0, EnvelopeDecimator.BucketOf(0, 1, 64));
        }

        [Fact]
        public void Add_KnownSamplesAcrossBuckets_ProducesPerBucketMinMax()
        {
            var decimator = new EnvelopeDecimator(channels: 1, buckets: 4);

            // Bucket 0
            decimator.Add(0, 0, 1.0);
            decimator.Add(0, 0, -3.0);
            decimator.Add(0, 0, 2.0);

            // Bucket 1
            decimator.Add(0, 1, 10.0);
            decimator.Add(0, 1, 10.0);

            // Bucket 2
            decimator.Add(0, 2, -100.0);
            decimator.Add(0, 2, 50.0);
            decimator.Add(0, 2, 0.0);

            // Bucket 3 left untouched (no adds)

            var envelopes = decimator.Build(approximate: false);
            var envelope = envelopes[0];

            Assert.Equal(4, envelope.BucketCount);

            Assert.True(envelope.Filled[0]);
            Assert.Equal(-3.0f, envelope.Min[0]);
            Assert.Equal(2.0f, envelope.Max[0]);

            Assert.True(envelope.Filled[1]);
            Assert.Equal(10.0f, envelope.Min[1]);
            Assert.Equal(10.0f, envelope.Max[1]);

            Assert.True(envelope.Filled[2]);
            Assert.Equal(-100.0f, envelope.Min[2]);
            Assert.Equal(50.0f, envelope.Max[2]);

            Assert.False(envelope.Filled[3]);

            Assert.Equal(-100.0f, envelope.DataMin);
            Assert.Equal(50.0f, envelope.DataMax);
            Assert.False(envelope.IsEmpty);
        }

        [Fact]
        public void Add_NaN_IsIgnored_BucketStaysUnfilled()
        {
            var decimator = new EnvelopeDecimator(channels: 1, buckets: 2);
            decimator.Add(0, 0, double.NaN);

            var envelope = decimator.Build(approximate: false)[0];

            Assert.False(envelope.Filled[0]);
        }

        [Fact]
        public void Add_PositiveInfinity_IsIgnored_BucketStaysUnfilled()
        {
            var decimator = new EnvelopeDecimator(channels: 1, buckets: 2);
            decimator.Add(0, 0, double.PositiveInfinity);

            var envelope = decimator.Build(approximate: false)[0];

            Assert.False(envelope.Filled[0]);
        }

        [Fact]
        public void Add_NegativeInfinity_IsIgnored_BucketStaysUnfilled()
        {
            var decimator = new EnvelopeDecimator(channels: 1, buckets: 2);
            decimator.Add(0, 0, double.NegativeInfinity);

            var envelope = decimator.Build(approximate: false)[0];

            Assert.False(envelope.Filled[0]);
        }

        [Fact]
        public void Add_NaNAmongValidSamples_NaNIgnoredButOthersCounted()
        {
            var decimator = new EnvelopeDecimator(channels: 1, buckets: 1);
            decimator.Add(0, 0, 5.0);
            decimator.Add(0, 0, double.NaN);
            decimator.Add(0, 0, -5.0);

            var envelope = decimator.Build(approximate: false)[0];

            Assert.True(envelope.Filled[0]);
            Assert.Equal(-5.0f, envelope.Min[0]);
            Assert.Equal(5.0f, envelope.Max[0]);
        }

        [Fact]
        public void FlatChannel_AllEqualValues_DataMinEqualsDataMax()
        {
            var decimator = new EnvelopeDecimator(channels: 1, buckets: 4);
            for (int b = 0; b < 4; b++)
            {
                decimator.Add(0, b, 7.5);
                decimator.Add(0, b, 7.5);
            }

            var envelope = decimator.Build(approximate: false)[0];

            Assert.Equal(envelope.DataMin, envelope.DataMax);
            Assert.Equal(7.5f, envelope.DataMin);
            for (int b = 0; b < 4; b++)
            {
                Assert.Equal(7.5f, envelope.Min[b]);
                Assert.Equal(7.5f, envelope.Max[b]);
            }
        }

        [Fact]
        public void Channel_WithNoAdds_IsEmptyWithSaneDataMinMax()
        {
            var decimator = new EnvelopeDecimator(channels: 1, buckets: 4);

            var envelope = decimator.Build(approximate: false)[0];

            Assert.True(envelope.IsEmpty);
            Assert.False(float.IsNaN(envelope.DataMin));
            Assert.False(float.IsNaN(envelope.DataMax));
            Assert.False(float.IsInfinity(envelope.DataMin));
            Assert.False(float.IsInfinity(envelope.DataMax));
            for (int b = 0; b < 4; b++)
            {
                Assert.False(envelope.Filled[b]);
            }
        }

        [Fact]
        public void Build_ApproximateTrue_SetsIsApproximateOnEveryChannel()
        {
            var decimator = new EnvelopeDecimator(channels: 3, buckets: 2);
            decimator.Add(0, 0, 1.0);
            decimator.Add(1, 0, 2.0);
            // channel 2 gets no adds

            var envelopes = decimator.Build(approximate: true);

            Assert.Equal(3, envelopes.Length);
            foreach (var envelope in envelopes)
            {
                Assert.True(envelope.IsApproximate);
            }
        }

        [Fact]
        public void Build_ApproximateFalse_DoesNotSetIsApproximate()
        {
            var decimator = new EnvelopeDecimator(channels: 1, buckets: 2);
            decimator.Add(0, 0, 1.0);

            var envelope = decimator.Build(approximate: false)[0];

            Assert.False(envelope.IsApproximate);
        }

        [Fact]
        public void Add_OutOfRangeChannelOrBucket_DoesNotThrow()
        {
            var decimator = new EnvelopeDecimator(channels: 1, buckets: 1);

            var ex = Record.Exception(() =>
            {
                decimator.Add(-1, 0, 1.0);
                decimator.Add(5, 0, 1.0);
                decimator.Add(0, -1, 1.0);
                decimator.Add(0, 5, 1.0);
            });

            Assert.Null(ex);
        }

        [Fact]
        public void Constructor_BuildsEnvelopeArrayMatchingChannelCount()
        {
            var decimator = new EnvelopeDecimator(channels: 5, buckets: 8);
            var envelopes = decimator.Build(approximate: false);

            Assert.Equal(5, envelopes.Length);
            foreach (var envelope in envelopes)
            {
                Assert.Equal(8, envelope.BucketCount);
            }
        }
    }
}
