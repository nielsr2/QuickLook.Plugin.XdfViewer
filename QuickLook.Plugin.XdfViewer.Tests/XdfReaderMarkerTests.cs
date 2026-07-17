using System.Linq;
using QuickLook.Plugin.XdfViewer.Xdf;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    public class XdfReaderMarkerTests
    {
        [Fact]
        public void DecodesMarkerValuesAndTimestamps()
        {
            var bytes = new XdfFixtureBuilder()
                .FileHeader()
                .StreamHeader(7, "Markers", "Markers", 1, 0, "string")
                .SamplesString(7, (12.3, "stim_on"), (14.9, "stim_off"))
                .ToStream();

            var s = Assert.Single(XdfReader.Read(bytes).Streams);
            Assert.True(s.IsMarkerStream);
            Assert.Equal(2, s.Markers.Count);
            Assert.Equal(12.3, s.Markers[0].Timestamp, 3);
            Assert.Equal("stim_on", s.Markers[0].Value);
            Assert.Equal("stim_off", s.Markers[1].Value);
        }

        [Fact]
        public void CapsMarkersAndSetsTruncatedFlag()
        {
            var markers = Enumerable.Range(0, XdfReader.MaxMarkersPerStream + 25)
                .Select(i => ((double)i, "m" + i)).ToArray();
            var bytes = new XdfFixtureBuilder()
                .FileHeader()
                .StreamHeader(7, "Markers", "Markers", 1, 0, "string")
                .SamplesString(7, markers)
                .ToStream();

            var s = Assert.Single(XdfReader.Read(bytes).Streams);
            Assert.Equal(XdfReader.MaxMarkersPerStream, s.Markers.Count);
            Assert.True(s.MarkersTruncated);
        }
    }
}
