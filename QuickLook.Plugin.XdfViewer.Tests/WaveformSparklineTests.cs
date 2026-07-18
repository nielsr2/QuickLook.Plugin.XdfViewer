using QuickLook.Plugin.XdfViewer.Controls;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    /// <summary>
    /// Unit tests for the pure, WPF-free MapX/MapY helpers on WaveformSparkline.
    /// The control itself (FrameworkElement, OnRender) is never constructed here —
    /// no GUI, no Dispatcher, no Window required.
    /// </summary>
    public class WaveformSparklineTests
    {
        [Fact]
        public void MapX_FirstBucket_IsZero()
        {
            Assert.Equal(0d, WaveformSparkline.MapX(0, 10, 200d));
        }

        [Fact]
        public void MapX_LastBucket_IsWidth()
        {
            const int bucketCount = 10;
            Assert.Equal(200d, WaveformSparkline.MapX(bucketCount - 1, bucketCount, 200d));
        }

        [Fact]
        public void MapX_MidBucket_IsProportional()
        {
            // bucket 5 of 0..9 (bucketCount=10) -> width * 5/9
            Assert.Equal(200d * 5 / 9, WaveformSparkline.MapX(5, 10, 200d));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void MapX_BucketCountLessThanOrEqualOne_ReturnsZero(int bucketCount)
        {
            Assert.Equal(0d, WaveformSparkline.MapX(0, bucketCount, 200d));
        }

        [Fact]
        public void MapY_AtDataMin_IsHeight_Bottom()
        {
            Assert.Equal(100d, WaveformSparkline.MapY(0d, 0d, 10d, 100d));
        }

        [Fact]
        public void MapY_AtDataMax_IsZero_Top()
        {
            Assert.Equal(0d, WaveformSparkline.MapY(10d, 0d, 10d, 100d));
        }

        [Fact]
        public void MapY_AtMidpoint_IsApproximatelyHalfHeight()
        {
            Assert.Equal(50d, WaveformSparkline.MapY(5d, 0d, 10d, 100d), 9);
        }

        [Fact]
        public void MapY_FlatChannel_DataMaxEqualsDataMin_ReturnsHalfHeight()
        {
            Assert.Equal(50d, WaveformSparkline.MapY(5d, 5d, 5d, 100d));
        }

        [Fact]
        public void MapY_FlatChannel_DataMaxLessThanDataMin_ReturnsHalfHeight()
        {
            Assert.Equal(50d, WaveformSparkline.MapY(5d, 5d, 3d, 100d));
        }

        [Fact]
        public void MapY_ValueBelowDataMin_ClampsToHeight()
        {
            Assert.Equal(100d, WaveformSparkline.MapY(-5d, 0d, 10d, 100d));
        }

        [Fact]
        public void MapY_ValueAboveDataMax_ClampsToZero()
        {
            Assert.Equal(0d, WaveformSparkline.MapY(15d, 0d, 10d, 100d));
        }
    }
}
