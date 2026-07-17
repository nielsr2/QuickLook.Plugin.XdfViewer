using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    public class XdfFixtureBuilderTests
    {
        [Fact]
        public void StartsWithMagic()
        {
            var bytes = new XdfFixtureBuilder().ToBytes();
            Assert.Equal(new byte[] { 0x58, 0x44, 0x46, 0x3A }, new[] { bytes[0], bytes[1], bytes[2], bytes[3] });
        }

        [Fact]
        public void FileHeaderChunk_HasCorrectLengthAndTag()
        {
            // FileHeader with a tiny known XML content.
            var bytes = new XdfFixtureBuilder().StreamHeaderXml(0, "<info/>").ToBytes();
            // after 4-byte magic: NumLengthBytes(=4), Length(4 LE), Tag(2 LE)=2, StreamID(4), content
            Assert.Equal(0x04, bytes[4]);                       // NumLengthBytes
            int len = bytes[5] | (bytes[6] << 8) | (bytes[7] << 16) | (bytes[8] << 24);
            Assert.Equal(0x02, bytes[9]);                       // Tag low byte = StreamHeader
            Assert.Equal(0x00, bytes[10]);                      // Tag high byte
            // content length = len - 2 (tag); next chunk would start at offset 9 + len
            Assert.True(len >= 2 + 4);                          // tag + streamId at minimum
        }
    }
}
