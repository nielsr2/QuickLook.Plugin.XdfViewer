using System.Linq;
using QuickLook.Plugin.XdfViewer.Xdf;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    public class XdfReaderHeaderTests
    {
        [Fact]
        public void ParsesFileHeaderAndStreamHeader()
        {
            var bytes = new XdfFixtureBuilder()
                .FileHeader("1.0", "2026-04-16T16:48:17+0200")
                .StreamHeader(2, "Trigno-AUX", "IMU", 144, 148.148, "float32",
                    ("ACCX1", "g"), ("ACCY1", "g"))
                .ToStream();

            var file = XdfReader.Read(bytes);

            Assert.Equal("1.0", file.Version);
            var s = Assert.Single(file.Streams);
            Assert.Equal(2u, s.Id);
            Assert.Equal("Trigno-AUX", s.Name);
            Assert.Equal("IMU", s.Type);
            Assert.Equal(144, s.ChannelCount);
            Assert.Equal("float32", s.ChannelFormat);
            Assert.Equal(148.148, s.NominalSampleRate, 3);
            Assert.Equal(144, s.Channels.Count); // 2 declared, the rest padded up to channel_count
            Assert.Equal("ACCX1", s.Channels[0].Label);
            Assert.Equal("g", s.Channels[0].Unit);
            Assert.Equal("ACCY1", s.Channels[1].Label);
            Assert.Equal("ch 3", s.Channels[2].Label);
        }

        [Fact]
        public void ReturnsNull_WhenNotXdf()
        {
            var bytes = new XdfFixtureBuilder(withMagic: false).Raw(new byte[] { 1, 2, 3, 4 }).ToStream();
            Assert.Null(XdfReader.Read(bytes));
        }
    }
}
