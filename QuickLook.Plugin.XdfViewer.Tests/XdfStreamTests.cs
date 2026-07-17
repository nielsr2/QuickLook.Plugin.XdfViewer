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
            Assert.True(new XdfStream { ChannelFormat = "string" }.IsMarkerStream);
            Assert.False(new XdfStream { ChannelFormat = "float32" }.IsMarkerStream);
        }

        [Fact]
        public void TotalDuration_SpansMinFirstToMaxLast()
        {
            var f = new XdfFile();
            f.Streams.Add(new XdfStream { FirstTimestamp = 1.0, LastTimestamp = 5.0 });
            f.Streams.Add(new XdfStream { FirstTimestamp = 2.0, LastTimestamp = 9.0 });
            Assert.Equal(8.0, f.TotalDuration.Value, 3);
        }
    }
}
