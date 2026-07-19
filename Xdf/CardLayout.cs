using System;

namespace QuickLook.Plugin.XdfViewer.Xdf
{
    /// <summary>
    /// Pure (WPF-free) column math for the stream-card grid. The overview lays cards out in a
    /// uniform grid so they stretch to fill the preview window's width instead of leaving a gap on
    /// the right; this decides how many equal-width columns fit.
    /// </summary>
    internal static class CardLayout
    {
        public static int ColumnsForWidth(double availableWidth, double minCardWidth, int cardCount)
        {
            if (availableWidth <= 0 || minCardWidth <= 0 || cardCount <= 0) return 1;
            int fit = (int)Math.Floor(availableWidth / minCardWidth);
            if (fit < 1) fit = 1;
            return fit > cardCount ? cardCount : fit;   // never more columns than cards
        }
    }
}
