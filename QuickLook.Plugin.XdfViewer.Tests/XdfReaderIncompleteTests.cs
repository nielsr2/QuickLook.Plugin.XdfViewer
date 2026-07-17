using System.Linq;
using QuickLook.Plugin.XdfViewer.Xdf;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    public class XdfReaderIncompleteTests
    {
        [Fact]
        public void NoFooter_TalliesSamplesAndFlagsIncomplete()
        {
            var bytes = new XdfFixtureBuilder()
                .FileHeader()
                .StreamHeader(1, "BioSemi", "EEG", 5, 512, "float32")
                .SamplesRaw(1, 300, fillerBytes: 4)
                .SamplesRaw(1, 200, fillerBytes: 4)
                .ToStream(); // no footer

            var s = Assert.Single(XdfReader.Read(bytes).Streams);
            Assert.True(s.IsIncomplete);
            Assert.Equal(500, s.SampleCount.Value);
        }

        [Fact]
        public void FooterPresent_FooterCountWins_NotIncomplete()
        {
            var bytes = new XdfFixtureBuilder()
                .FileHeader()
                .StreamHeader(1, "BioSemi", "EEG", 5, 512, "float32")
                .SamplesRaw(1, 300, fillerBytes: 4)
                .StreamFooter(1, 0, 1, count: 300)
                .ToStream();

            var s = Assert.Single(XdfReader.Read(bytes).Streams);
            Assert.False(s.IsIncomplete);
            Assert.Equal(300, s.SampleCount.Value);
        }
    }
}
