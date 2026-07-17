using QuickLook.Plugin.XdfViewer.Xdf;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    public class XdfReaderRobustnessTests
    {
        [Fact]
        public void TruncatedTrailingBytes_ReturnsPartialWithFlag()
        {
            var bytes = new XdfFixtureBuilder()
                .FileHeader()
                .StreamHeader(1, "BioSemi", "EEG", 5, 512, "float32")
                // dangling chunk header claiming far more bytes than remain
                .Raw(new byte[] { 0x04, 0xFF, 0xFF, 0xFF, 0x7F, 0x02, 0x00 })
                .ToStream();

            var file = XdfReader.Read(bytes);
            Assert.True(file.IsTruncated);
            Assert.Single(file.Streams); // the good StreamHeader still parsed
        }
    }
}
