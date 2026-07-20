using QuickLook.Plugin.XdfViewer.Xdf.Model;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    public class XdfStreamTests
    {
        [Fact]
        public void Duration_FromTimestamps_WhenPresent()
        {
            var s = new XdfStream { FirstTimestamp = 10.0, LastTimestamp = 73.5 };
            Assert.Equal(63.5, s.Duration.Value, 3);
        }

        [Fact]
        public void Duration_FromSampleCountAndRate_WhenNoTimestamps()
        {
            var s = new XdfStream { NominalSampleRate = 512, SampleCount = 1024 };
            Assert.Equal(2.0, s.Duration.Value, 3);
        }

        [Fact]
        public void Duration_Null_WhenIrregularAndNoTimestamps()
        {
            var s = new XdfStream { NominalSampleRate = 0, SampleCount = 18 };
            Assert.Null(s.Duration);
        }

        [Fact]
        public void IsMarkerStream_WhenStringFormat()
        {
            Assert.True(new XdfStream { ChannelFormat = "string", NominalSampleRate = 0 }.IsMarkerStream);
            Assert.False(new XdfStream { ChannelFormat = "float32" }.IsMarkerStream);
        }

        [Fact]
        public void IsMarkerStream_StringIrregular_True()
        {
            var s = new XdfStream { ChannelFormat = "string", NominalSampleRate = 0, Type = "Markers" };
            Assert.True(s.IsMarkerStream);
        }

        [Fact]
        public void IsMarkerStream_VideoCompressedStringIrregular_False()
        {
            var s = new XdfStream { ChannelFormat = "string", NominalSampleRate = 0, Type = "VideoCompressed" };
            Assert.False(s.IsMarkerStream);
        }

        [Fact]
        public void IsMarkerStream_StringAtRegularRate_False()
        {
            var s = new XdfStream { ChannelFormat = "string", NominalSampleRate = 10000, Type = "sampledMarkers" };
            Assert.False(s.IsMarkerStream);
        }

        [Fact]
        public void IsMarkerStream_NumericIrregular_False()
        {
            var s = new XdfStream { ChannelFormat = "float32", NominalSampleRate = 0, Type = "EEG" };
            Assert.False(s.IsMarkerStream);
        }

        [Fact]
        public void TotalDuration_SpansMinFirstToMaxLast()
        {
            var f = new XdfFile();
            f.Streams.Add(new XdfStream { FirstTimestamp = 1.0, LastTimestamp = 5.0 });
            f.Streams.Add(new XdfStream { FirstTimestamp = 2.0, LastTimestamp = 9.0 });
            Assert.Equal(8.0, f.TotalDuration.Value, 3);
        }

        [Fact]
        public void TotalDuration_Null_WhenNoStreamHasBothTimestamps()
        {
            var f = new XdfFile();
            f.Streams.Add(new XdfStream { FirstTimestamp = 1.0, LastTimestamp = null });
            f.Streams.Add(new XdfStream { FirstTimestamp = null, LastTimestamp = 100.0 });
            Assert.Null(f.TotalDuration);
        }

        [Fact]
        public void TotalDuration_IgnoresStreamsMissingATimestamp()
        {
            var f = new XdfFile();
            f.Streams.Add(new XdfStream { FirstTimestamp = 1.0, LastTimestamp = 5.0 }); // paired
            f.Streams.Add(new XdfStream { FirstTimestamp = 0.0, LastTimestamp = null }); // unpaired — ignored
            Assert.Equal(4.0, f.TotalDuration.Value, 3);
        }
    }
}
