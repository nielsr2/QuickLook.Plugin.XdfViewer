using QuickLook.Plugin.XdfViewer.Xdf;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    /// <summary>
    /// Pure column math for the stream-card grid: how many equal-width columns fit in the available
    /// width, never more than there are cards. Keeps the overview filling the preview window instead
    /// of leaving a gap on the right. No WPF.
    /// </summary>
    public class CardLayoutTests
    {
        [Fact]
        public void ColumnsForWidth_DefaultPreviewWidth_FitsTwo()
        {
            // QuickLook asks for a 750px-wide preview; at a 320px minimum two cards fit.
            Assert.Equal(2, CardLayout.ColumnsForWidth(750d, 320d, 4));
        }

        [Fact]
        public void ColumnsForWidth_NarrowerThanOneCard_IsSingleColumn()
        {
            Assert.Equal(1, CardLayout.ColumnsForWidth(300d, 320d, 4));
        }

        [Fact]
        public void ColumnsForWidth_WideWindow_CappedByCardCount()
        {
            // 1400/320 = 4 columns would fit, but there are only 3 cards to show.
            Assert.Equal(3, CardLayout.ColumnsForWidth(1400d, 320d, 3));
        }

        [Fact]
        public void ColumnsForWidth_SingleCard_IsSingleColumn()
        {
            Assert.Equal(1, CardLayout.ColumnsForWidth(750d, 320d, 1));
        }

        [Fact]
        public void ColumnsForWidth_NoCards_IsSingleColumn()
        {
            // UniformGrid needs a positive column count even with nothing to lay out.
            Assert.Equal(1, CardLayout.ColumnsForWidth(750d, 320d, 0));
        }

        [Theory]
        [InlineData(0d)]
        [InlineData(-10d)]
        public void ColumnsForWidth_NonPositiveWidth_IsSingleColumn(double width)
        {
            Assert.Equal(1, CardLayout.ColumnsForWidth(width, 320d, 4));
        }

        [Fact]
        public void ColumnsForWidth_NonPositiveMinCardWidth_IsSingleColumn()
        {
            Assert.Equal(1, CardLayout.ColumnsForWidth(750d, 0d, 4));
        }
    }
}
