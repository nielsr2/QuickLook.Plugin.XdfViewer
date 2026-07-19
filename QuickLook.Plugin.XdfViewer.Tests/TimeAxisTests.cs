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
    }
}
