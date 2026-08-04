using System.Linq;
using QuickLook.Plugin.XdfViewer.Controls;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    /// <summary>
    /// Pure tick-generation for the time axis over an arbitrary visible window [start, end] seconds
    /// (the full-view zoom shows a sub-range, not always 0..duration).
    /// </summary>
    public class TimeAxisTests
    {
        [Fact]
        public void Ticks_WholeStream_FromZero()
        {
            var ticks = TimeAxis.Ticks(0, 10, 900);
            Assert.Equal(0d, ticks.First());
            Assert.Equal(10d, ticks.Last());
            Assert.Equal(11, ticks.Length); // step 1s => 0..10
        }

        [Fact]
        public void Ticks_MidStreamWindow_AlignedToStep()
        {
            var ticks = TimeAxis.Ticks(12, 18, 540);
            Assert.Equal(12d, ticks.First());
            Assert.Equal(18d, ticks.Last());
            Assert.All(ticks, t => Assert.True(t >= 12 && t <= 18));
        }

        [Fact]
        public void Ticks_SubSecondWindow_UsesFineStep()
        {
            var ticks = TimeAxis.Ticks(0, 0.5, 900);
            Assert.Equal(0d, ticks.First());
            Assert.True(ticks.Last() <= 0.5 + 1e-9);
            // step should be well under a second
            Assert.True(ticks[1] - ticks[0] < 0.2);
        }

        [Fact]
        public void Ticks_EmptyOrInvalidWindow_ReturnsNone()
        {
            Assert.Empty(TimeAxis.Ticks(5, 5, 900));
            Assert.Empty(TimeAxis.Ticks(10, 5, 900));
            Assert.Empty(TimeAxis.Ticks(0, 10, 0));
        }

        [Theory]
        [InlineData(200d, 0)]     // whole-stream view
        [InlineData(1d, 0)]
        [InlineData(0.5d, 1)]
        [InlineData(0.2d, 1)]
        [InlineData(0.05d, 2)]
        [InlineData(0.01d, 2)]
        [InlineData(0.002d, 3)]   // the 500 Hz sample-accurate case
        [InlineData(0.0005d, 4)]
        public void DecimalsFor_FollowsTheStep(double step, int expected)
        {
            Assert.Equal(expected, TimeAxis.DecimalsFor(step));
        }

        [Fact]
        public void DecimalsFor_DegenerateStep_IsZeroNotAbsurd()
        {
            Assert.Equal(0, TimeAxis.DecimalsFor(0));
            Assert.Equal(0, TimeAxis.DecimalsFor(-1));
            Assert.Equal(0, TimeAxis.DecimalsFor(double.NaN));
            Assert.Equal(0, TimeAxis.DecimalsFor(double.PositiveInfinity));
            Assert.True(TimeAxis.DecimalsFor(1e-30) <= 9);
        }

        [Fact]
        public void Labels_WholeSecondStep_ArePlainSeconds()
        {
            var labels = TimeAxis.Labels(new[] { 200d, 400d, 600d }, 200d);
            Assert.Equal(new[] { "200s", "400s", "600s" }, labels);
        }

        [Fact]
        public void Labels_SubSecondStep_WriteTheFirstInFullThenOnlyTheFraction()
        {
            // The reported bug: at t≈1764s every one of these used to render as "1764s".
            var ticks = new[] { 1764.002d, 1764.004d, 1764.006d, 1764.008d };

            var labels = TimeAxis.Labels(ticks, 0.002d);

            Assert.Equal(new[] { "1764.002s", ".004", ".006", ".008" }, labels);
        }

        [Fact]
        public void Labels_CrossingASecond_WritesTheNewSecondInFull()
        {
            // ".000" alone would read as a jump backwards, so the whole second reappears.
            var ticks = new[] { 1764.996d, 1764.998d, 1765.000d, 1765.002d };

            var labels = TimeAxis.Labels(ticks, 0.002d);

            Assert.Equal(new[] { "1764.996s", ".998", "1765.000s", ".002" }, labels);
        }

        [Fact]
        public void Labels_RoundingUpIntoTheNextSecond_StillMatchesWhatIsPrinted()
        {
            // 1764.9999 prints as "1765.000"; the prefix compared must be the printed one, or the
            // following tick would be abbreviated against a second that was never shown.
            var labels = TimeAxis.Labels(new[] { 1764.9999d, 1765.001d }, 0.001d);

            Assert.Equal("1765.000s", labels[0]);
            Assert.Equal(".001", labels[1]);
        }

        [Fact]
        public void Labels_FractionsAreZeroPaddedSoTheyAlign()
        {
            var labels = TimeAxis.Labels(new[] { 10.00d, 10.04d, 10.10d }, 0.04d);
            Assert.Equal(new[] { "10.00s", ".04", ".10" }, labels);
        }

        [Fact]
        public void Labels_NoTicks_ReturnsNone()
        {
            Assert.Empty(TimeAxis.Labels(new double[0], 0.5d));
            Assert.Empty(TimeAxis.Labels(null, 0.5d));
        }
    }
}
