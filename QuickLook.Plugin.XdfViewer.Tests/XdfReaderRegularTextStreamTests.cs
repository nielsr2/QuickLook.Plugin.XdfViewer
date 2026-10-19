using System.IO;
using System.Linq;
using System.Text;
using QuickLook.Plugin.XdfViewer.Xdf;
using QuickLook.Plugin.XdfViewer.Xdf.Model;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    /// <summary>Text (cf_string) streams declared with a nominal rate &gt; 0 carry no numeric data to
    /// plot, so by default they show as events like irregular marker streams. A setting turns this off.</summary>
    public class XdfReaderRegularTextStreamTests
    {
        private static XdfFixtureBuilder RegularTextStream(double srate = 10) => new XdfFixtureBuilder()
            .FileHeader()
            .StreamHeader(1, "Log", "Text", 1, srate, "string")
            .SamplesString(1, (100.0, "start"), (100.1, "stop"));

        [Fact]
        public void RegularTextStream_IsReadAsEvents_ByDefault()
        {
            var s = Assert.Single(XdfReader.Read(RegularTextStream().ToStream()).Streams);

            Assert.True(s.IsMarkerStream);
            Assert.Equal(new[] { "start", "stop" }, s.Markers.Select(m => m.Value));
        }

        [Fact]
        public void RegularTextStream_StaysAPlainStream_WhenTheOptionIsOff()
        {
            var s = Assert.Single(XdfReader.Read(RegularTextStream().ToStream(), regularTextAsEvents: false).Streams);

            Assert.False(s.IsMarkerStream);
            Assert.Empty(s.Markers);
        }

        [Fact]
        public void IrregularTextStream_IsEvents_EvenWhenTheOptionIsOff()
        {
            var s = Assert.Single(XdfReader.Read(RegularTextStream(srate: 0).ToStream(), regularTextAsEvents: false).Streams);

            Assert.True(s.IsMarkerStream);
        }

        [Fact]
        public void VideoStreams_AreNeverEvents()
        {
            Assert.False(new XdfStream { Type = "VideoCompressed", ChannelFormat = "string", NominalSampleRate = 30 }.IsMarkerStream);
        }

        [Fact]
        public void OmittedTimestamps_AreDeducedFromTheNominalRate()
        {
            // XDF lets a regular-rate stream omit a sample's timestamp (marker byte 0): it is the
            // previous timestamp plus 1/srate. Three samples: stamped 100.0, then two unstamped.
            var builder = new XdfFixtureBuilder().FileHeader().StreamHeader(1, "Log", "Text", 1, 10, "string");
            using (var m = new MemoryStream())
            using (var w = new BinaryWriter(m))
            {
                w.Write(1u);
                w.Write((byte)1); w.Write((byte)3);                         // varlen: 3 samples
                w.Write((byte)8); w.Write(100.0); Str(w, "a");
                w.Write((byte)0); Str(w, "b");
                w.Write((byte)0); Str(w, "c");
                builder.Chunk(3, m.ToArray());
            }

            var s = Assert.Single(XdfReader.Read(builder.ToStream()).Streams);

            Assert.Equal(new[] { 100.0, 100.1, 100.2 }, s.Markers.Select(x => System.Math.Round(x.Timestamp, 6)));
        }

        private static void Str(BinaryWriter w, string v)
        {
            var b = Encoding.UTF8.GetBytes(v);
            w.Write((byte)1); w.Write((byte)b.Length); w.Write(b);
        }
    }
}
