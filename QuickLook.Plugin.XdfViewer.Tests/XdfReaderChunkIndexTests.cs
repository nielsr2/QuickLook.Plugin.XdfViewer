using System.IO;
using System.Linq;
using QuickLook.Plugin.XdfViewer.Xdf;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    /// <summary>
    /// The Pass-1 chunk index is built once and memoized on the <see cref="Xdf.Model.XdfFile"/> so
    /// repeated envelope reads (sparkline → expand → full view → every zoom step) don't re-walk the
    /// whole file. Reusing the cache must not change results.
    /// </summary>
    public class XdfReaderChunkIndexTests
    {
        private static byte[] TwoChunkStream()
        {
            return new XdfFixtureBuilder()
                .FileHeader()
                .StreamHeader(1, "S", "EEG", 1, 100, "int16")
                .SamplesNumeric(1, "int16", 1, false,
                    Enumerable.Range(0, 50).Select(i => new double[] { i }).ToArray())
                .SamplesNumeric(1, "int16", 1, false,
                    Enumerable.Range(50, 50).Select(i => new double[] { i }).ToArray())
                .ToBytes();
        }

        [Fact]
        public void FillEnvelopes_MemoizesChunkIndexOnFile()
        {
            var bytes = TwoChunkStream();
            var file = XdfReader.Read(new MemoryStream(bytes));
            Assert.Null(file.ChunkIndex);

            XdfReader.FillEnvelopes(file, new MemoryStream(bytes), new EnvelopeOptions { Buckets = 10 });
            Assert.NotNull(file.ChunkIndex);
        }

        [Fact]
        public void SecondCall_ReusesSameIndexInstance()
        {
            var bytes = TwoChunkStream();
            var file = XdfReader.Read(new MemoryStream(bytes));

            XdfReader.FillEnvelopes(file, new MemoryStream(bytes), new EnvelopeOptions { Buckets = 10 });
            var first = file.ChunkIndex;
            XdfReader.FillEnvelopes(file, new MemoryStream(bytes),
                new EnvelopeOptions { Buckets = 10, StartSample = 10, WindowSampleCount = 20 });
            var second = file.ChunkIndex;

            Assert.Same(first, second);
        }

        [Fact]
        public void CachedIndex_ProducesIdenticalWindowedResult()
        {
            var bytes = TwoChunkStream();
            var opts = new EnvelopeOptions { Buckets = 8, StartSample = 20, WindowSampleCount = 40 };

            // Warm the cache with an unrelated whole-stream read, then take a windowed read.
            var warm = XdfReader.Read(new MemoryStream(bytes));
            XdfReader.FillEnvelopes(warm, new MemoryStream(bytes), new EnvelopeOptions { Buckets = 3 });
            var cached = Assert.Single(XdfReader.FillEnvelopes(warm, new MemoryStream(bytes), opts));

            // Fresh file, index built cold for the same windowed read.
            var cold = XdfReader.Read(new MemoryStream(bytes));
            var fresh = Assert.Single(XdfReader.FillEnvelopes(cold, new MemoryStream(bytes), opts));

            var a = cached.Channels[0];
            var b = fresh.Channels[0];
            Assert.Equal(a.BucketCount, b.BucketCount);
            for (int i = 0; i < a.BucketCount; i++)
            {
                Assert.Equal(a.Filled[i], b.Filled[i]);
                if (a.Filled[i]) { Assert.Equal(a.Min[i], b.Min[i]); Assert.Equal(a.Max[i], b.Max[i]); }
            }
        }
    }
}
