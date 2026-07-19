namespace QuickLook.Plugin.XdfViewer.Xdf
{
    /// <summary>
    /// Pure (WPF-free) placement math for the full-view marker overlay. Maps a marker time (seconds,
    /// measured from the same zero as the plot's time axis) to a pixel x within the visible window,
    /// or NaN when the marker is outside the window or the window is degenerate.
    /// </summary>
    internal static class MarkerLayout
    {
        public static double XForSeconds(double markerSec, double startSec, double endSec, double widthPx)
        {
            double span = endSec - startSec;
            if (span <= 0) return double.NaN;                        // degenerate window
            if (markerSec < startSec || markerSec > endSec) return double.NaN; // outside window
            return widthPx * (markerSec - startSec) / span;
        }
    }
}
