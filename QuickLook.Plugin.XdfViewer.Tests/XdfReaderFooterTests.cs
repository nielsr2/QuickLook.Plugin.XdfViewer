using System.Linq;
using QuickLook.Plugin.XdfViewer.Xdf;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    public class XdfReaderFooterTests
    {
        [Fact]
        public void MergesFooterCountAndDuration()
        {
            var bytes = new XdfFixtureBuilder()
                .FileHeader()
                .StreamHeader(1, "BioSemi", "EEG", 5, 512, "float32")
                .SamplesRaw(1, 100, fillerBytes: 8)
                .StreamFooter(1, first: 0.0, last: 63.5, count: 32512)
                .ToStream();

            var s = Assert.Single(XdfReader.Read(bytes).Streams);
            Assert.Equal(32512, s.SampleCount.Value);
            Assert.Equal(0.0, s.FirstTimestamp.Value, 3);
            Assert.Equal(63.5, s.LastTimestamp.Value, 3);
            Assert.Equal(63.5, s.Duration.Value, 3);
            Assert.False(s.IsIncomplete);
        }
    }
}
