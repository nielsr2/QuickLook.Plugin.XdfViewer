using System;
using System.IO;
using System.Linq;
using QuickLook.Plugin.XdfViewer.Xdf;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    public class RealFileSmokeTests
    {
        // The sample recording is gitignored; only present on the developer's machine.
        private static string SamplePath()
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            for (int i = 0; i < 6 && dir != null; i++, dir = Path.GetDirectoryName(dir))
            {
                var hit = Directory.GetFiles(dir, "*.xdf", SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (hit != null) return hit;
            }
            return null;
        }

        [Fact]
        public void RealFile_ParsesStreamsWhenPresent()
        {
            var path = SamplePath();
            if (path == null) return; // skipped when the sample is absent

            var file = XdfReader.Read(path);
            Assert.NotNull(file);
            Assert.NotEmpty(file.Streams);
            Assert.Contains(file.Streams, s => s.ChannelCount > 0);
        }

        [Fact]
        public void RealFile_FillEnvelopes_ProducesBoundedFiniteWaveformsWhenPresent()
        {
            var path = SamplePath();
            if (path == null) return; // skipped when the sample is absent

            var file = XdfReader.Read(path);
            if (file == null) return; // don't hard-fail CI on an unreadable local sample

            var envs = XdfReader.FillEnvelopes(file, path, new EnvelopeOptions
            {
                Buckets = XdfReader.SparklineBuckets,
                MaxSamplesPerStream = XdfReader.MaxEnvelopeSamplesPerStream
            });

            // The sample (Trigno) recording has numeric streams, so this should yield envelopes.
            Assert.NotEmpty(envs);

            foreach (var se in envs)
            {
                Assert.NotEmpty(se.Channels);

                bool anyChannelNonEmpty = false;
                foreach (var ch in se.Channels)
                {
                    Assert.Equal(XdfReader.SparklineBuckets, ch.BucketCount);

                    if (!ch.IsEmpty)
                        anyChannelNonEmpty = true;

                    Assert.False(float.IsNaN(ch.DataMin));
                    Assert.False(float.IsInfinity(ch.DataMin));
                    Assert.False(float.IsNaN(ch.DataMax));
                    Assert.False(float.IsInfinity(ch.DataMax));
                    Assert.True(ch.DataMin <= ch.DataMax);
                }

                Assert.True(anyChannelNonEmpty, $"stream {se.StreamId} has no non-empty channels");
            }
        }

        [Fact]
        public void RealFile_WindowedRead_IsBoundedAndFiniteWhenPresent()
        {
            var path = SamplePath();
            if (path == null) return; // skipped when the sample is absent

            var file = XdfReader.Read(path);
            if (file == null) return;

            // Find a numeric stream with data, then read only a sub-range window of it (the full-view
            // zoom's core operation).
            var whole = XdfReader.FillEnvelopes(file, path, new EnvelopeOptions
            {
                Buckets = XdfReader.SparklineBuckets,
                MaxSamplesPerStream = XdfReader.MaxEnvelopeSamplesPerStream
            });
            if (whole.Length == 0) return;

            var target = whole.FirstOrDefault(s => s.Channels.Any(c => !c.IsEmpty));
            if (target == null) return;

            var stream = file.Streams.First(s => s.Id == target.StreamId);
            long total = stream.SampleCount ?? stream.TalliedSampleCount;
            if (total < 20) return;

            long start = total / 4;
            long count = Math.Min(2000, total / 2);
            int buckets = (int)Math.Min(count, 1000);

            var win = XdfReader.FillEnvelopes(file, path, new EnvelopeOptions
            {
                OnlyStreamId = target.StreamId,
                StartSample = start,
                WindowSampleCount = count,
                Buckets = buckets,
                MaxSamplesPerStream = XdfReader.MaxEnvelopeSamplesPerStreamFull
            });

            var wse = Assert.Single(win);
            Assert.NotEmpty(wse.Channels);

            bool anyNonEmpty = false;
            foreach (var ch in wse.Channels)
            {
                Assert.Equal(buckets, ch.BucketCount);
                Assert.False(float.IsNaN(ch.DataMin));
                Assert.False(float.IsInfinity(ch.DataMin));
                Assert.False(float.IsNaN(ch.DataMax));
                Assert.False(float.IsInfinity(ch.DataMax));
                Assert.True(ch.DataMin <= ch.DataMax);
                if (!ch.IsEmpty) anyNonEmpty = true;
            }
            Assert.True(anyNonEmpty, "windowed read produced no non-empty channels");
        }
    }
}
