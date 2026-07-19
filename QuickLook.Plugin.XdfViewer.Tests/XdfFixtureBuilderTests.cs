using System.IO;
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

        [Fact]
        public void SamplesNumeric_Int16_RoundTripsThroughRawBytes()
        {
            var samples = new[] { new double[] { 1, -2 }, new double[] { 3, -4 } };
            var bytes = new XdfFixtureBuilder()
                .SamplesNumeric(1, "int16", 2, false, samples)
                .ToBytes();

            using (var ms = new MemoryStream(bytes))
            using (var r = new BinaryReader(ms))
            {
                r.ReadBytes(4); // "XDF:" magic

                byte numLengthBytes = r.ReadByte();
                Assert.Equal(4, numLengthBytes);
                uint length = r.ReadUInt32();
                Assert.True(length >= 2 + 4);

                ushort tag = r.ReadUInt16();
                Assert.Equal(3, tag); // Samples chunk

                uint streamId = r.ReadUInt32();
                Assert.Equal(1u, streamId);

                byte numSamplesLenBytes = r.ReadByte();
                Assert.Equal(4, numSamplesLenBytes);
                uint numSamples = r.ReadUInt32();
                Assert.Equal((uint)samples.Length, numSamples);

                foreach (var sample in samples)
                {
                    byte tsMarker = r.ReadByte();
                    Assert.Equal(0, tsMarker); // withTimestamps = false

                    foreach (var expected in sample)
                    {
                        short actual = r.ReadInt16();
                        Assert.Equal((short)expected, actual);
                    }
                }
            }
        }

        [Fact]
        public void SamplesNumeric_Float32WithTimestamps_RoundTripsThroughRawBytes()
        {
            var samples = new[] { new double[] { 3.5f }, new double[] { -1.25f } };
            var bytes = new XdfFixtureBuilder()
                .SamplesNumeric(2, "float32", 1, true, samples)
                .ToBytes();

            using (var ms = new MemoryStream(bytes))
            using (var r = new BinaryReader(ms))
            {
                r.ReadBytes(4); // "XDF:" magic

                Assert.Equal(4, r.ReadByte());     // NumLengthBytes
                r.ReadUInt32();                    // Length (not asserted here)
                Assert.Equal(3, r.ReadUInt16());   // Tag = Samples
                Assert.Equal(2u, r.ReadUInt32());  // StreamID

                Assert.Equal(4, r.ReadByte());     // NumSamples length bytes
                uint numSamples = r.ReadUInt32();
                Assert.Equal((uint)samples.Length, numSamples);

                foreach (var sample in samples)
                {
                    byte tsMarker = r.ReadByte();
                    Assert.Equal(8, tsMarker);     // withTimestamps = true
                    r.ReadDouble();                // timestamp value (not asserted here)

                    foreach (var expected in sample)
                    {
                        float actual = r.ReadSingle();
                        Assert.Equal((float)expected, actual);
                    }
                }
            }
        }
    }
}
