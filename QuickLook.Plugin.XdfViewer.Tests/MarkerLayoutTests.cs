using QuickLook.Plugin.XdfViewer.Xdf;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    /// <summary>
    /// Pure placement math for the full-view marker overlay: maps a marker's time (seconds, relative
    /// to the same zero as the plot's time axis) to a pixel x within the visible window, or NaN when
    /// the marker falls outside the window. No WPF.
    /// </summary>
    public class MarkerLayoutTests
    {
        private const int P = 6;

        [Fact]
        public void XForSeconds_Middle_IsProportional()
        {
            Assert.Equal(100d, MarkerLayout.XForSeconds(5d, 0d, 10d, 200d), P);
        }

        [Fact]
        public void XForSeconds_AtWindowStart_IsZero()
        {
            Assert.Equal(0d, MarkerLayout.XForSeconds(0d, 0d, 10d, 200d), P);
        }

        [Fact]
        public void XForSeconds_AtWindowEnd_IsWidth()
        {
            Assert.Equal(200d, MarkerLayout.XForSeconds(10d, 0d, 10d, 200d), P);
        }

        [Fact]
        public void XForSeconds_NonZeroStart_IsProportional()
        {
            // window [10, 20]s; a marker at 15s sits halfway.
            Assert.Equal(100d, MarkerLayout.XForSeconds(15d, 10d, 20d, 200d), P);
        }

        [Fact]
        public void XForSeconds_BeforeWindow_IsNaN()
        {
            Assert.True(double.IsNaN(MarkerLayout.XForSeconds(-1d, 0d, 10d, 200d)));
        }

        [Fact]
        public void XForSeconds_AfterWindow_IsNaN()
        {
            Assert.True(double.IsNaN(MarkerLayout.XForSeconds(11d, 0d, 10d, 200d)));
        }

        [Fact]
        public void XForSeconds_DegenerateSpan_IsNaN()
        {
            Assert.True(double.IsNaN(MarkerLayout.XForSeconds(10d, 10d, 10d, 200d)));
        }
    }
}
