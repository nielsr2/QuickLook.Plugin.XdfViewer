using System;
using System.Linq;
using QuickLook.Plugin.XdfViewer.Xdf;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    public class XdfReaderEnvelopeTests
    {
        [Fact]
        public void SingleChunk_Int16_TwoBuckets_ComputesPerBucketMinMax()
        {
            var builder = new XdfFixtureBuilder()
                .FileHeader()
                .StreamHeader(1, "S", "EEG", 1, 100, "int16")
                .SamplesNumeric(1, "int16", 1, false,
                    new double[] { 0 }, new double[] { 10 }, new double[] { -5 }, new double[] { 3 });

            var file = XdfReader.Read(builder.ToStream());
            var opts = new EnvelopeOptions { Buckets = 2 };
            var envelopes = XdfReader.FillEnvelopes(file, builder.ToStream(), opts);

            var se = Assert.Single(envelopes);
            Assert.Equal(1u, se.StreamId);
            Assert.False(se.IsApproximate);
            var ch = Assert.Single(se.Channels);

            Assert.True(ch.Filled[0]);
            Assert.Equal(0f, ch.Min[0]);
            Assert.Equal(10f, ch.Max[0]);

            Assert.True(ch.Filled[1]);
            Assert.Equal(-5f, ch.Min[1]);
            Assert.Equal(3f, ch.Max[1]);
        }

        [Fact]
        public void TwoChunks_GlobalBucketingSpansBothChunks()
        {
            var builder = new XdfFixtureBuilder()
                .FileHeader()
                .StreamHeader(1, "S", "EEG", 1, 100, "int16")
                .SamplesNumeric(1, "int16", 1, false, new double[] { 0 }, new double[] { 10 })
                .SamplesNumeric(1, "int16", 1, false, new double[] { -5 }, new double[] { 3 });

            var file = XdfReader.Read(builder.ToStream());
            var opts = new EnvelopeOptions { Buckets = 2 };
            var envelopes = XdfReader.FillEnvelopes(file, builder.ToStream(), opts);

            var se = Assert.Single(envelopes);
            var ch = Assert.Single(se.Channels);

            Assert.True(ch.Filled[0]);
            Assert.Equal(0f, ch.Min[0]);
            Assert.Equal(10f, ch.Max[0]);

            Assert.True(ch.Filled[1]);
            Assert.Equal(-5f, ch.Min[1]);
            Assert.Equal(3f, ch.Max[1]);
        }

        [Fact]
        public void TinyMaxSamplesPerStream_ForcesApproximate_AndSpansFirstAndLastBucket()
        {
            var builder = new XdfFixtureBuilder()
                .FileHeader()
                .StreamHeader(1, "Big", "EEG", 1, 100, "int16");

            // 5 chunks x 100 samples = 500 total samples.
            for (int chunkIdx = 0; chunkIdx < 5; chunkIdx++)
            {
                var samples = Enumerable.Range(0, 100)
                    .Select(i => new double[] { chunkIdx * 100 + i })
                    .ToArray();
                builder.SamplesNumeric(1, "int16", 1, false, samples);
            }

            var file = XdfReader.Read(builder.ToStream());
            // cap=50 over 5x100-sample chunks => k=ceil(500/50)=10 => only chunk 0 (ci%10==0) and
            // chunk 4 (last) get decoded => middle chunks 1-3 are skipped entirely.
            var opts = new EnvelopeOptions { Buckets = 10, MaxSamplesPerStream = 50 };
            var envelopes = XdfReader.FillEnvelopes(file, builder.ToStream(), opts);

            var se = Assert.Single(envelopes);
            Assert.True(se.IsApproximate);
            var ch = se.Channels[0];
            Assert.True(ch.IsApproximate);

            Assert.True(ch.Filled[0] || ch.Filled[1]); // front of timeline covered
            Assert.True(ch.Filled[8] || ch.Filled[9]); // tail of timeline covered
            Assert.False(ch.Filled[4]);                // skipped middle chunks left this bucket empty
        }

        [Fact]
        public void IrregularStream_ZeroNominalSrate_StillDecodes()
        {
            var builder = new XdfFixtureBuilder()
                .FileHeader()
                .StreamHeader(1, "Irregular", "Gaze", 1, 0, "int16")
                .SamplesNumeric(1, "int16", 1, false, new double[] { 1 }, new double[] { 2 }, new double[] { 3 });

            var file = XdfReader.Read(builder.ToStream());
            var opts = new EnvelopeOptions { Buckets = 1 };
            var envelopes = XdfReader.FillEnvelopes(file, builder.ToStream(), opts);

            var se = Assert.Single(envelopes);
            var ch = Assert.Single(se.Channels);
            Assert.False(ch.IsEmpty);
            Assert.Equal(1f, ch.Min[0]);
            Assert.Equal(3f, ch.Max[0]);
        }

        [Fact]
        public void ZeroSampleNumericStream_AbsentFromResult()
        {
            var builder = new XdfFixtureBuilder()
                .FileHeader()
                .StreamHeader(1, "Empty", "EEG", 1, 100, "int16");

            var file = XdfReader.Read(builder.ToStream());
            var envelopes = XdfReader.FillEnvelopes(file, builder.ToStream(), new EnvelopeOptions());

            Assert.Empty(envelopes);
        }

        [Fact]
        public void NaNInFloat32Payload_SkippedButOtherSamplesFine()
        {
            var builder = new XdfFixtureBuilder()
                .FileHeader()
                .StreamHeader(1, "S", "EEG", 2, 100, "float32")
                .SamplesNumeric(1, "float32", 2, false,
                    new double[] { 1, 2 },
                    new double[] { double.NaN, 4 },
                    new double[] { 3, double.NaN });

            var file = XdfReader.Read(builder.ToStream());
            var opts = new EnvelopeOptions { Buckets = 1 };
            var envelopes = XdfReader.FillEnvelopes(file, builder.ToStream(), opts);

            var se = Assert.Single(envelopes);
            Assert.Equal(2, se.Channels.Length);

            Assert.False(se.Channels[0].IsEmpty);
            Assert.Equal(1f, se.Channels[0].Min[0]);
            Assert.Equal(3f, se.Channels[0].Max[0]);

            Assert.False(se.Channels[1].IsEmpty);
            Assert.Equal(2f, se.Channels[1].Min[0]);
            Assert.Equal(4f, se.Channels[1].Max[0]);
        }

        [Fact]
        public void TruncatedTail_DoesNotThrow_ReturnsPartialResult()
        {
            var builder = new XdfFixtureBuilder()
                .FileHeader()
                .StreamHeader(1, "S", "EEG", 1, 100, "int16")
                .SamplesNumeric(1, "int16", 1, false,
                    new double[] { 0 }, new double[] { 10 }, new double[] { -5 }, new double[] { 3 })
                // dangling chunk header claiming far more bytes than remain (same pattern as
                // XdfReaderRobustnessTests.TruncatedTrailingBytes_ReturnsPartialWithFlag)
                .Raw(new byte[] { 0x04, 0xFF, 0xFF, 0xFF, 0x7F, 0x02, 0x00 });

            var file = XdfReader.Read(builder.ToStream());
            var opts = new EnvelopeOptions { Buckets = 2 };

            QuickLook.Plugin.XdfViewer.Xdf.Model.StreamEnvelope[] envelopes = null;
            var ex = Record.Exception(() => envelopes = XdfReader.FillEnvelopes(file, builder.ToStream(), opts));

            Assert.Null(ex);
            Assert.NotNull(envelopes);
            var se = Assert.Single(envelopes);
            Assert.Equal(1u, se.StreamId);
            var ch = Assert.Single(se.Channels);
            Assert.False(ch.IsEmpty);
            Assert.Equal(0f, ch.Min[0]);
            Assert.Equal(10f, ch.Max[0]);
            Assert.Equal(-5f, ch.Min[1]);
            Assert.Equal(3f, ch.Max[1]);
        }

        [Fact]
        public void OnlyStreamId_ReturnsJustThatStream()
        {
            var builder = new XdfFixtureBuilder()
                .FileHeader()
                .StreamHeader(1, "First", "EEG", 1, 100, "int16")
                .SamplesNumeric(1, "int16", 1, false, new double[] { 1 }, new double[] { 2 })
                .StreamHeader(2, "Second", "EEG", 1, 100, "int16")
                .SamplesNumeric(2, "int16", 1, false, new double[] { 5 }, new double[] { 6 });

            var file = XdfReader.Read(builder.ToStream());
            var opts = new EnvelopeOptions { OnlyStreamId = 2 };
            var envelopes = XdfReader.FillEnvelopes(file, builder.ToStream(), opts);

            var se = Assert.Single(envelopes);
            Assert.Equal(2u, se.StreamId);
        }

        [Fact]
        public void Regression_ReadStillReturnsExpectedStreamsAndNames()
        {
            var builder = new XdfFixtureBuilder()
                .FileHeader()
                .StreamHeader(1, "First", "EEG", 1, 100, "int16")
                .SamplesNumeric(1, "int16", 1, false, new double[] { 1 }, new double[] { 2 })
                .StreamFooter(1, 0, 1, count: 2)
                .StreamHeader(2, "Second", "EEG", 1, 100, "int16")
                .SamplesNumeric(2, "int16", 1, false, new double[] { 5 }, new double[] { 6 })
                .StreamFooter(2, 0, 1, count: 2);

            var file = XdfReader.Read(builder.ToStream());

            Assert.Equal(2, file.Streams.Count);
            Assert.Equal("First", file.Streams[0].Name);
            Assert.Equal("Second", file.Streams[1].Name);
            Assert.Equal(2, file.Streams[0].SampleCount);
            Assert.Equal(2, file.Streams[1].SampleCount);
        }
    }
}
