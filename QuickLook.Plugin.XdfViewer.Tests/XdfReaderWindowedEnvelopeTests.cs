using System.Linq;
using QuickLook.Plugin.XdfViewer.Xdf;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    /// <summary>
    /// Windowed (sub-range) envelope reads: <see cref="EnvelopeOptions.StartSample"/> +
    /// <see cref="EnvelopeOptions.WindowSampleCount"/>. Backs the full-view zoom, which requests
    /// only the visible sample window at high bucket density.
    /// </summary>
    public class XdfReaderWindowedEnvelopeTests
    {
        // Single stream, one channel, 10 samples with values 0..9 in one chunk.
        private static (Xdf.Model.XdfFile file, byte[] bytes) TenSamples()
        {
            var builder = new XdfFixtureBuilder()
                .FileHeader()
                .StreamHeader(1, "S", "EEG", 1, 100, "int16")
                .SamplesNumeric(1, "int16", 1, false,
                    Enumerable.Range(0, 10).Select(i => new double[] { i }).ToArray());
            var bytes = builder.ToBytes();
            var file = XdfReader.Read(new System.IO.MemoryStream(bytes));
            return (file, bytes);
        }

        [Fact]
        public void Window_MidRange_OneBucket_ExactMinMax()
        {
            var (file, bytes) = TenSamples();
            var opts = new EnvelopeOptions { Buckets = 1, StartSample = 2, WindowSampleCount = 4 };
            var se = Assert.Single(XdfReader.FillEnvelopes(file, new System.IO.MemoryStream(bytes), opts));

            var ch = Assert.Single(se.Channels);
            Assert.False(ch.IsEmpty);
            Assert.Equal(2f, ch.Min[0]); // samples 2,3,4,5
            Assert.Equal(5f, ch.Max[0]);
        }

        [Fact]
        public void Window_MidRange_PerSampleBuckets_OneSamplePerBucket()
        {
            var (file, bytes) = TenSamples();
            // 4 samples (2,3,4,5) into 4 buckets => one sample per bucket.
            var opts = new EnvelopeOptions { Buckets = 4, StartSample = 2, WindowSampleCount = 4 };
            var se = Assert.Single(XdfReader.FillEnvelopes(file, new System.IO.MemoryStream(bytes), opts));
            var ch = Assert.Single(se.Channels);

            for (int b = 0; b < 4; b++)
            {
                Assert.True(ch.Filled[b]);
                Assert.Equal(2f + b, ch.Min[b]);
                Assert.Equal(2f + b, ch.Max[b]); // Min==Max => sample-accurate
            }
            Assert.False(se.IsApproximate);
        }

        [Fact]
        public void Window_AtStartEdge()
        {
            var (file, bytes) = TenSamples();
            var opts = new EnvelopeOptions { Buckets = 1, StartSample = 0, WindowSampleCount = 3 };
            var se = Assert.Single(XdfReader.FillEnvelopes(file, new System.IO.MemoryStream(bytes), opts));
            var ch = Assert.Single(se.Channels);
            Assert.Equal(0f, ch.Min[0]); // samples 0,1,2
            Assert.Equal(2f, ch.Max[0]);
        }

        [Fact]
        public void Window_PastEnd_ClampsToAvailable()
        {
            var (file, bytes) = TenSamples();
            // start 8, ask 5 but only 8,9 remain.
            var opts = new EnvelopeOptions { Buckets = 1, StartSample = 8, WindowSampleCount = 5 };
            var se = Assert.Single(XdfReader.FillEnvelopes(file, new System.IO.MemoryStream(bytes), opts));
            var ch = Assert.Single(se.Channels);
            Assert.Equal(8f, ch.Min[0]);
            Assert.Equal(9f, ch.Max[0]);
            Assert.False(se.IsApproximate);
        }

        [Fact]
        public void Window_StartBeyondTotal_EmptyChannel()
        {
            var (file, bytes) = TenSamples();
            var opts = new EnvelopeOptions { Buckets = 4, StartSample = 20, WindowSampleCount = 4 };
            var se = Assert.Single(XdfReader.FillEnvelopes(file, new System.IO.MemoryStream(bytes), opts));
            var ch = Assert.Single(se.Channels);
            Assert.True(ch.IsEmpty);
        }

        [Fact]
        public void Window_SpanningChunkBoundary_SelectsBothChunks()
        {
            var builder = new XdfFixtureBuilder()
                .FileHeader()
                .StreamHeader(1, "S", "EEG", 1, 100, "int16")
                .SamplesNumeric(1, "int16", 1, false, new double[] { 0 }, new double[] { 1 },
                    new double[] { 2 }, new double[] { 3 }, new double[] { 4 })   // chunk A: 0..4
                .SamplesNumeric(1, "int16", 1, false, new double[] { 5 }, new double[] { 6 },
                    new double[] { 7 }, new double[] { 8 }, new double[] { 9 });  // chunk B: 5..9
            var bytes = builder.ToBytes();
            var file = XdfReader.Read(new System.IO.MemoryStream(bytes));

            // window [3,4) samples => 3,4 (chunk A) + 5,6 (chunk B)
            var opts = new EnvelopeOptions { Buckets = 1, StartSample = 3, WindowSampleCount = 4 };
            var se = Assert.Single(XdfReader.FillEnvelopes(file, new System.IO.MemoryStream(bytes), opts));
            var ch = Assert.Single(se.Channels);
            Assert.Equal(3f, ch.Min[0]);
            Assert.Equal(6f, ch.Max[0]);
        }

        [Fact]
        public void Window_ExceedingCap_ForcesApproximate_SpansFirstAndLast()
        {
            var builder = new XdfFixtureBuilder()
                .FileHeader()
                .StreamHeader(1, "Big", "EEG", 1, 100, "int16");
            // one chunk of 200 samples, values 0..199
            builder.SamplesNumeric(1, "int16", 1, false,
                Enumerable.Range(0, 200).Select(i => new double[] { i }).ToArray());
            var bytes = builder.ToBytes();
            var file = XdfReader.Read(new System.IO.MemoryStream(bytes));

            // window [50,100) with cap 10 => strided/approximate, but ends must be covered.
            var opts = new EnvelopeOptions
            {
                Buckets = 10, StartSample = 50, WindowSampleCount = 100, MaxSamplesPerStream = 10
            };
            var se = Assert.Single(XdfReader.FillEnvelopes(file, new System.IO.MemoryStream(bytes), opts));
            Assert.True(se.IsApproximate);
            var ch = se.Channels[0];
            Assert.True(ch.Filled[0]);          // first window bucket covered (sample 50)
            Assert.True(ch.Filled[9]);          // last window bucket covered (sample 149)
            Assert.Equal(50f, ch.Min[0]);       // window starts at sample value 50
            Assert.True(ch.Max[9] >= 149f - 10); // tail near sample 149
        }

        [Fact]
        public void FullWindow_EqualsWholeStream()
        {
            var (file, bytes) = TenSamples();
            var whole = Assert.Single(
                XdfReader.FillEnvelopes(file, new System.IO.MemoryStream(bytes), new EnvelopeOptions { Buckets = 5 }));
            var windowed = Assert.Single(
                XdfReader.FillEnvelopes(file, new System.IO.MemoryStream(bytes),
                    new EnvelopeOptions { Buckets = 5, StartSample = 0, WindowSampleCount = 10 }));

            var a = whole.Channels[0];
            var b = windowed.Channels[0];
            Assert.Equal(a.BucketCount, b.BucketCount);
            for (int i = 0; i < a.BucketCount; i++)
            {
                Assert.Equal(a.Filled[i], b.Filled[i]);
                if (a.Filled[i]) { Assert.Equal(a.Min[i], b.Min[i]); Assert.Equal(a.Max[i], b.Max[i]); }
            }
        }
    }
}
