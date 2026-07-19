using System;
using System.IO;
using QuickLook.Plugin.XdfViewer.Xdf;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    public class XdfSampleFormatTests
    {
        [Fact]
        public void Int8_Width()
        {
            var result = XdfSampleFormat.TryGetNumeric("int8", out int width, out var read);
            Assert.True(result);
            Assert.Equal(1, width);
        }

        [Fact]
        public void Int8_Decode()
        {
            XdfSampleFormat.TryGetNumeric("int8", out _, out var read);
            using (var ms = new MemoryStream(new byte[] { 0xFF }))
            using (var r = new BinaryReader(ms))
                Assert.Equal(-1.0, read(r));
        }

        [Fact]
        public void Int16_Width()
        {
            var result = XdfSampleFormat.TryGetNumeric("int16", out int width, out var read);
            Assert.True(result);
            Assert.Equal(2, width);
        }

        [Fact]
        public void Int16_Decode_LittleEndian()
        {
            XdfSampleFormat.TryGetNumeric("int16", out _, out var read);
            // 0x14, 0x3c in little-endian is 0x3c14 = 15380
            using (var ms = new MemoryStream(new byte[] { 0x14, 0x3c }))
            using (var r = new BinaryReader(ms))
                Assert.Equal(15380.0, read(r));
        }

        [Fact]
        public void Int32_Width()
        {
            var result = XdfSampleFormat.TryGetNumeric("int32", out int width, out var read);
            Assert.True(result);
            Assert.Equal(4, width);
        }

        [Fact]
        public void Int32_Decode()
        {
            XdfSampleFormat.TryGetNumeric("int32", out _, out var read);
            using (var ms = new MemoryStream(BitConverter.GetBytes(12345)))
            using (var r = new BinaryReader(ms))
                Assert.Equal(12345.0, read(r));
        }

        [Fact]
        public void Int64_Width()
        {
            var result = XdfSampleFormat.TryGetNumeric("int64", out int width, out var read);
            Assert.True(result);
            Assert.Equal(8, width);
        }

        [Fact]
        public void Int64_Decode()
        {
            XdfSampleFormat.TryGetNumeric("int64", out _, out var read);
            using (var ms = new MemoryStream(BitConverter.GetBytes(9876543210L)))
            using (var r = new BinaryReader(ms))
                Assert.Equal(9876543210.0, read(r));
        }

        [Fact]
        public void Float32_Width()
        {
            var result = XdfSampleFormat.TryGetNumeric("float32", out int width, out var read);
            Assert.True(result);
            Assert.Equal(4, width);
        }

        [Fact]
        public void Float32_Decode()
        {
            XdfSampleFormat.TryGetNumeric("float32", out _, out var read);
            using (var ms = new MemoryStream(BitConverter.GetBytes(1.5f)))
            using (var r = new BinaryReader(ms))
                Assert.Equal(1.5, read(r), precision: 5);
        }

        [Fact]
        public void Double64_Width()
        {
            var result = XdfSampleFormat.TryGetNumeric("double64", out int width, out var read);
            Assert.True(result);
            Assert.Equal(8, width);
        }

        [Fact]
        public void Double64_Decode()
        {
            XdfSampleFormat.TryGetNumeric("double64", out _, out var read);
            using (var ms = new MemoryStream(BitConverter.GetBytes(3.14159265)))
            using (var r = new BinaryReader(ms))
                Assert.Equal(3.14159265, read(r), precision: 8);
        }

        [Fact]
        public void NullFormat_ReturnsFalse()
        {
            var result = XdfSampleFormat.TryGetNumeric(null, out int width, out var read);
            Assert.False(result);
            Assert.Equal(0, width);
            Assert.Null(read);
        }

        [Fact]
        public void EmptyString_ReturnsFalse()
        {
            var result = XdfSampleFormat.TryGetNumeric("", out int width, out var read);
            Assert.False(result);
            Assert.Equal(0, width);
            Assert.Null(read);
        }

        [Fact]
        public void InvalidFormat_ReturnsFalse()
        {
            var result = XdfSampleFormat.TryGetNumeric("foo", out int width, out var read);
            Assert.False(result);
            Assert.Equal(0, width);
            Assert.Null(read);
        }

        [Fact]
        public void FormatWithWhitespace_Trims()
        {
            var result = XdfSampleFormat.TryGetNumeric("  int8  ", out int width, out var read);
            Assert.True(result);
            Assert.Equal(1, width);
        }

        [Fact]
        public void FormatWithUppercase_CaseInsensitive()
        {
            var result = XdfSampleFormat.TryGetNumeric("INT16", out int width, out var read);
            Assert.True(result);
            Assert.Equal(2, width);
        }
    }
}
