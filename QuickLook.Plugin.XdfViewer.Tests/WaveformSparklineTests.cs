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

        // --- Amplitude inverse (pixel Y -> data value, for the hover crosshair readout) ---

        [Fact]
        public void InvMapYInset_TopInset_IsMaxVisibleValue()
        {
            // center 5, halfRange 5, height 100, gain 1, inset 10 => band [10, 90].
            // The top of the band (y=inset) reads the top visible value center+halfRange = 10.
            Assert.Equal(10d, WaveformSparkline.InvMapYInset(10d, 5d, 5d, 100d, 1d, 10d), 9);
        }

        [Fact]
        public void InvMapYInset_BottomInset_IsMinVisibleValue()
        {
            Assert.Equal(0d, WaveformSparkline.InvMapYInset(90d, 5d, 5d, 100d, 1d, 10d), 9);
        }

        [Fact]
        public void InvMapYInset_MidHeight_IsCenter()
        {
            Assert.Equal(5d, WaveformSparkline.InvMapYInset(50d, 5d, 5d, 100d, 1d, 10d), 9);
        }

        [Fact]
        public void InvMapYInset_RoundTripsWithMapYInset()
        {
            // Inverting the forward map recovers the original value (within the visible band).
            const double center = 5d, halfRange = 5d, height = 100d, gain = 1d, inset = 10d;
            foreach (double v in new[] { 1d, 3d, 5d, 7.5d, 9d })
            {
                double y = WaveformSparkline.MapYInset(v, center, halfRange, height, gain, inset);
                Assert.Equal(v, WaveformSparkline.InvMapYInset(y, center, halfRange, height, gain, inset), 6);
            }
        }

        [Fact]
        public void InvMapYInset_Gain2_HalvesTheVisibleHalfRange()
        {
            // gain 2 => visible half-range is halfRange/2 = 2.5, so the top of the band reads center+2.5.
            Assert.Equal(7.5d, WaveformSparkline.InvMapYInset(10d, 5d, 5d, 100d, 2d, 10d), 9);
        }

        [Fact]
        public void InvMapYInset_FlatRange_ReturnsCenter()
        {
            // A flat channel has no invertible amplitude; the readout is just the constant value.
            Assert.Equal(5d, WaveformSparkline.InvMapYInset(30d, 5d, 0d, 100d, 1d, 10d), 9);
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

        // --- Bucket bridging: what keeps a slow ramp from drawing as invisible slivers ---

        [Fact]
        public void ClassifyBridge_CurrentAbovePrevious_ReturnsRising()
        {
            Assert.Equal(BucketBridge.Rising, WaveformSparkline.ClassifyBridge(0f, 1f, 2f, 3f));
        }

        [Fact]
        public void ClassifyBridge_CurrentBelowPrevious_ReturnsFalling()
        {
            Assert.Equal(BucketBridge.Falling, WaveformSparkline.ClassifyBridge(2f, 3f, 0f, 1f));
        }

        [Fact]
        public void ClassifyBridge_OverlappingRanges_ReturnsNone()
        {
            // A signal that swings within each bucket keeps the disconnected-tick look.
            Assert.Equal(BucketBridge.None, WaveformSparkline.ClassifyBridge(0f, 2f, 1f, 3f));
        }

        [Fact]
        public void ClassifyBridge_TouchingRanges_ReturnsRising()
        {
            // A counter's buckets meet exactly at the boundary; that still counts as a climb.
            Assert.Equal(BucketBridge.Rising, WaveformSparkline.ClassifyBridge(0f, 1f, 1f, 2f));
        }

        // --- Minimum tick length: a single-sample bucket has min == max and would draw nothing ---

        [Fact]
        public void EnsureMinTick_TickShorterThanMinimum_ExpandsSymmetrically()
        {
            double top = 10d, bottom = 10.4d;

            WaveformSparkline.EnsureMinTick(ref top, ref bottom, 100d, 1d);

            Assert.Equal(9.7d, top, 6);
            Assert.Equal(10.7d, bottom, 6);
        }

        [Fact]
        public void EnsureMinTick_ZeroLengthTick_ExpandsToMinimum()
        {
            double top = 50d, bottom = 50d;

            WaveformSparkline.EnsureMinTick(ref top, ref bottom, 100d, 1d);

            Assert.Equal(1d, bottom - top, 6);
        }

        [Fact]
        public void EnsureMinTick_TickLongerThanMinimum_Unchanged()
        {
            double top = 10d, bottom = 40d;

            WaveformSparkline.EnsureMinTick(ref top, ref bottom, 100d, 1d);

            Assert.Equal(10d, top);
            Assert.Equal(40d, bottom);
        }

        [Fact]
        public void EnsureMinTick_NearTopEdge_ShiftsInsideBounds()
        {
            double top = 0d, bottom = 0d;

            WaveformSparkline.EnsureMinTick(ref top, ref bottom, 100d, 1d);

            Assert.Equal(0d, top, 6);
            Assert.Equal(1d, bottom, 6);
        }

        [Fact]
        public void EnsureMinTick_NearBottomEdge_ShiftsInsideBounds()
        {
            double top = 100d, bottom = 100d;

            WaveformSparkline.EnsureMinTick(ref top, ref bottom, 100d, 1d);

            Assert.Equal(99d, top, 6);
            Assert.Equal(100d, bottom, 6);
        }

        [Fact]
        public void EnsureMinTick_HeightSmallerThanMinimum_StaysWithinHeight()
        {
            double top = 0.2d, bottom = 0.2d;

            WaveformSparkline.EnsureMinTick(ref top, ref bottom, 0.5d, 1d);

            Assert.Equal(0d, top, 6);
            Assert.True(bottom <= 1d);
        }
    }
}
