using QuickLook.Plugin.XdfViewer.Xdf;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    /// <summary>
    /// Pure window math behind the full-view zoom/pan. No WPF; drives the single SetView choke point.
    /// </summary>
    public class FullViewMathTests
    {
        private const int P = 6;

        [Fact]
        public void ZoomIn_KeepsCursorSampleFixed()
        {
            var w = FullViewMath.ZoomAboutCursor(start: 0, count: 100, total: 1000,
                cursorFrac: 0.5, factor: 0.5, minCount: 4);
            Assert.Equal(50, w.Count, P);
            Assert.Equal(25, w.Start, P);
            // sample under the cursor is unchanged
            Assert.Equal(50, w.Start + 0.5 * w.Count, P);
        }

        [Fact]
        public void ZoomIn_CursorAtLeftEdge_StartUnchanged()
        {
            var w = FullViewMath.ZoomAboutCursor(start: 100, count: 100, total: 1000,
                cursorFrac: 0.0, factor: 0.5, minCount: 4);
            Assert.Equal(50, w.Count, P);
            Assert.Equal(100, w.Start, P);
        }

        [Fact]
        public void ZoomOut_ClampsToTotalAndStartZero()
        {
            var w = FullViewMath.ZoomAboutCursor(start: 100, count: 100, total: 1000,
                cursorFrac: 0.5, factor: 20.0, minCount: 4);
            Assert.Equal(1000, w.Count, P);
            Assert.Equal(0, w.Start, P);
        }

        [Fact]
        public void ZoomIn_ClampsToMinCount()
        {
            var w = FullViewMath.ZoomAboutCursor(start: 0, count: 10, total: 1000,
                cursorFrac: 0.5, factor: 0.1, minCount: 4);
            Assert.Equal(4, w.Count, P);
        }

        [Fact]
        public void Pan_ClampsAtLeft()
        {
            var w = FullViewMath.Pan(start: 0, count: 100, total: 1000, deltaSamples: -50);
            Assert.Equal(0, w.Start, P);
            Assert.Equal(100, w.Count, P);
        }

        [Fact]
        public void Pan_ClampsAtRight()
        {
            var w = FullViewMath.Pan(start: 900, count: 100, total: 1000, deltaSamples: 50);
            Assert.Equal(900, w.Start, P); // max start = total - count
        }

        [Fact]
        public void Pan_Middle_Moves()
        {
            var w = FullViewMath.Pan(start: 100, count: 100, total: 1000, deltaSamples: 50);
            Assert.Equal(150, w.Start, P);
        }

        [Fact]
        public void Clamp_CountLargerThanTotal_FillsWholeStream()
        {
            var w = FullViewMath.Clamp(start: 500, count: 2000, total: 1000, minCount: 4);
            Assert.Equal(1000, w.Count, P);
            Assert.Equal(0, w.Start, P);
        }

        [Fact]
        public void Clamp_ZeroTotal_IsEmpty()
        {
            var w = FullViewMath.Clamp(start: 10, count: 10, total: 0, minCount: 4);
            Assert.Equal(0, w.Count, P);
            Assert.Equal(0, w.Start, P);
        }
    }
}
