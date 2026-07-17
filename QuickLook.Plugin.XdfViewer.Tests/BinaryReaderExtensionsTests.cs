using System.IO;
using QuickLook.Plugin.XdfViewer.Xdf;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    public class BinaryReaderExtensionsTests
    {
        private static long ReadVar(params byte[] bytes)
        {
            using (var ms = new MemoryStream(bytes))
            using (var r = new BinaryReader(ms))
                return r.ReadVarLength();
        }

        [Fact] public void OneByteLength() => Assert.Equal(0x77, ReadVar(0x01, 0x77));

        [Fact] public void FourByteLength_LittleEndian()
            => Assert.Equal(0x3c14, ReadVar(0x04, 0x14, 0x3c, 0x00, 0x00));

        [Fact] public void EightByteLength_LittleEndian()
            => Assert.Equal(1L, ReadVar(0x08, 0x01, 0, 0, 0, 0, 0, 0, 0));

        [Fact] public void InvalidCountThrows()
            => Assert.Throws<InvalidDataException>(() => ReadVar(0x03, 0, 0, 0));
    }
}
