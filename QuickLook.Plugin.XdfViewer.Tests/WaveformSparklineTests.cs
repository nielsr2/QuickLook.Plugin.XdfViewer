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

        [Fact]
        public void MapYGain_Gain1_MatchesMapY()
        {
            // Gain 1 is the identity: min -> bottom, max -> top, mid -> centre.
            Assert.Equal(100d, WaveformSparkline.MapYGain(0d, 0d, 10d, 100d, 1d));
            Assert.Equal(0d, WaveformSparkline.MapYGain(10d, 0d, 10d, 100d, 1d));
            Assert.Equal(50d, WaveformSparkline.MapYGain(5d, 0d, 10d, 100d, 1d));
        }

        [Fact]
        public void MapYGain_Gain2_ZoomsAboutCentre()
        {
            // Range 0..10, centre 5, height 100. At gain 2 the visible half-range is 2.5,
            // so value 7.5 maps to the top (0) and 2.5 to the bottom (100); centre stays 50.
            Assert.Equal(50d, WaveformSparkline.MapYGain(5d, 0d, 10d, 100d, 2d));
            Assert.Equal(0d, WaveformSparkline.MapYGain(7.5d, 0d, 10d, 100d, 2d));
            Assert.Equal(100d, WaveformSparkline.MapYGain(2.5d, 0d, 10d, 100d, 2d));
        }

        [Fact]
        public void MapYGain_ClampsBeyondVisibleRange()
        {
            // With gain 2, anything past the visible ±2.5 window clamps to the edges.
            Assert.Equal(0d, WaveformSparkline.MapYGain(10d, 0d, 10d, 100d, 2d));
            Assert.Equal(100d, WaveformSparkline.MapYGain(0d, 0d, 10d, 100d, 2d));
        }

        [Fact]
        public void MapYGain_FlatRange_ReturnsMidHeight()
        {
            Assert.Equal(50d, WaveformSparkline.MapYGain(5d, 5d, 5d, 100d, 3d));
        }

        // --- Vertical inset (full-view top/bottom padding) ---

        [Fact]
        public void MapYInset_MaxValue_SitsAtTopInset()
        {
            // center 5, halfRange 5, height 100, inset 10 => usable band [10, 90].
            Assert.Equal(10d, WaveformSparkline.MapYInset(10d, 5d, 5d, 100d, 1d, 10d), 9);
        }

        [Fact]
        public void MapYInset_MinValue_SitsAtBottomInset()
        {
            Assert.Equal(90d, WaveformSparkline.MapYInset(0d, 5d, 5d, 100d, 1d, 10d), 9);
        }

        [Fact]
        public void MapYInset_Center_IsMidHeight()
        {
            Assert.Equal(50d, WaveformSparkline.MapYInset(5d, 5d, 5d, 100d, 1d, 10d), 9);
        }

        [Fact]
        public void MapYInset_ZeroInset_MatchesMapYPivot()
        {
            Assert.Equal(WaveformSparkline.MapYGain(3d, 0d, 10d, 100d, 1d),
                         WaveformSparkline.MapYInset(3d, 5d, 5d, 100d, 1d, 0d), 9);
        }

        // --- Sample-dot mode decision (zoomed to ~>=1 sample/bucket) ---

        [Fact]
        public void UseSampleDots_ThresholdZero_Disabled()
        {
            Assert.False(WaveformSparkline.UseSampleDots(50, 1000d, 0d));
        }

        [Fact]
        public void UseSampleDots_WidePerBucket_Enabled()
        {
            // 100 buckets across 1000px => ~10px/bucket >= 6 threshold.
            Assert.True(WaveformSparkline.UseSampleDots(100, 1000d, 6d));
        }

        [Fact]
        public void UseSampleDots_NarrowPerBucket_Disabled()
        {
            // 1000 buckets across 1000px => ~1px/bucket < 6 threshold.
            Assert.False(WaveformSparkline.UseSampleDots(1000, 1000d, 6d));
        }

        [Fact]
        public void UseSampleDots_SingleBucket_UsesFullWidth()
        {
            Assert.True(WaveformSparkline.UseSampleDots(1, 1000d, 6d));
        }

        [Fact]
        public void UseSampleDots_NoBuckets_Disabled()
        {
            Assert.False(WaveformSparkline.UseSampleDots(0, 1000d, 6d));
        }
    }
}
