using System.IO;
using QuickLook.Plugin.XdfViewer.Xdf;
using QuickLook.Plugin.XdfViewer.Xdf.Model;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    /// <summary>Defensive edge cases exercised before a public release.</summary>
    public class XdfReaderHardeningTests
    {
        [Fact]
        public void EmptyFile_ReturnsNull()
        {
            Assert.Null(XdfReader.Read(new MemoryStream(new byte[0])));
        }

        [Fact]
        public void ChannelCountAboveCap_ProducesNoEnvelope_NoThrow()
        {
            var builder = new XdfFixtureBuilder()
                .FileHeader()
                .StreamHeader(1, "Corrupt", "EEG", XdfReader.MaxEnvelopeChannels + 1, 100, "int16")
                .SamplesRaw(1, numSamples: 2, fillerBytes: 8);

            var file = XdfReader.Read(builder.ToStream());
            StreamEnvelope[] envs = null;
            var ex = Record.Exception(() =>
                envs = XdfReader.FillEnvelopes(file, builder.ToStream(), new EnvelopeOptions()));

            Assert.Null(ex);
            Assert.Empty(envs); // metadata still parsed, just no waveform
        }

        [Fact]
        public void UnknownNumericFormat_ProducesNoEnvelope_NoThrow()
        {
            var builder = new XdfFixtureBuilder()
                .FileHeader()
                .StreamHeader(1, "Weird", "X", 1, 100, "weird32")
                .SamplesRaw(1, numSamples: 3, fillerBytes: 6);

            var file = XdfReader.Read(builder.ToStream());
            StreamEnvelope[] envs = null;
            var ex = Record.Exception(() =>
                envs = XdfReader.FillEnvelopes(file, builder.ToStream(), new EnvelopeOptions()));

            Assert.Null(ex);
            Assert.Empty(envs);
        }

        [Fact]
        public void SamplesBeforeHeader_StillParsesAndDecodes()
        {
            var builder = new XdfFixtureBuilder()
                .FileHeader()
                .SamplesNumeric(1, "int16", 1, false, new double[] { 5 }, new double[] { 9 }) // samples first
                .StreamHeader(1, "Late", "EEG", 1, 100, "int16");                             // header after

            var file = XdfReader.Read(builder.ToStream());
            var s = Assert.Single(file.Streams);
            Assert.Equal("Late", s.Name);

            var envs = XdfReader.FillEnvelopes(file, builder.ToStream(), new EnvelopeOptions { Buckets = 1 });
            var se = Assert.Single(envs);
            Assert.Equal(5f, se.Channels[0].Min[0]);
            Assert.Equal(9f, se.Channels[0].Max[0]);
        }
    }
}
