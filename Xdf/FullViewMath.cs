using System;

namespace QuickLook.Plugin.XdfViewer.Xdf
{
    /// <summary>A visible sample window: <see cref="Start"/> and <see cref="Count"/> in sample units.</summary>
    internal struct ViewWindow
    {
        public double Start;
        public double Count;
        public ViewWindow(double start, double count) { Start = start; Count = count; }
    }

    /// <summary>
    /// Pure (WPF-free, side-effect-free) math for the full-view zoom/pan window. Every user input
    /// (wheel zoom, drag/minimap/scrollbar pan) funnels through these so the window stays valid and
    /// consistent. Kept out of the code-behind so it can be unit-tested headlessly.
    /// </summary>
    internal static class FullViewMath
    {
        /// <summary>Clamps a window to a valid range: Count in [minCount, total], Start in [0, total-Count].</summary>
        public static ViewWindow Clamp(double start, double count, double total, double minCount)
        {
            if (total <= 0) return new ViewWindow(0, 0);
            if (minCount < 1) minCount = 1;
            if (minCount > total) minCount = total;

            if (count < minCount) count = minCount;
            if (count > total) count = total;

            double maxStart = total - count;
            if (start < 0) start = 0;
            if (start > maxStart) start = maxStart;
            return new ViewWindow(start, count);
        }

        /// <summary>
        /// Zooms by <paramref name="factor"/> (&lt;1 = zoom in, &gt;1 = out) while keeping the sample
        /// under the cursor fixed. <paramref name="cursorFrac"/> is the cursor's fractional x within
        /// the viewport (0 = left, 1 = right).
        /// </summary>
        public static ViewWindow ZoomAboutCursor(double start, double count, double total,
            double cursorFrac, double factor, double minCount)
        {
            if (total <= 0) return new ViewWindow(0, 0);
            double sampleUnderCursor = start + cursorFrac * count;

            double newCount = count * factor;
            if (minCount < 1) minCount = 1;
            if (minCount > total) minCount = total;
            if (newCount < minCount) newCount = minCount;
            if (newCount > total) newCount = total;

            double newStart = sampleUnderCursor - cursorFrac * newCount;
            return Clamp(newStart, newCount, total, minCount);
        }

        /// <summary>
        /// The (fractional) sample index under a cursor at fractional x <paramref name="xFrac"/>
        /// within the viewport (0 = left edge, 1 = right edge). <paramref name="xFrac"/> is clamped
        /// to [0, 1]. Used by the hover crosshair to read the time under the pointer.
        /// </summary>
        public static double SampleAtViewX(double start, double count, double xFrac)
        {
            if (xFrac < 0) xFrac = 0; else if (xFrac > 1) xFrac = 1;
            return start + xFrac * count;
        }

        /// <summary>Shifts the window by <paramref name="deltaSamples"/>, keeping Count and clamping Start.</summary>
        public static ViewWindow Pan(double start, double count, double total, double deltaSamples)
        {
            if (total <= 0) return new ViewWindow(0, 0);
            double maxStart = Math.Max(0, total - count);
            double newStart = start + deltaSamples;
            if (newStart < 0) newStart = 0;
            if (newStart > maxStart) newStart = maxStart;
            return new ViewWindow(newStart, count);
        }

        /// <summary>
        /// Centres the window on <paramref name="sample"/>, keeping Count and clamping Start — so an
        /// event near either end of the stream lands off-centre rather than scrolling past the edge.
        /// Used by the full view's events panel, which must not change the user's zoom level.
        /// </summary>
        public static ViewWindow CenterOn(double sample, double count, double total)
        {
            if (total <= 0) return new ViewWindow(0, 0);
            return Clamp(sample - count / 2.0, count, total, 1);
        }
    }
}
