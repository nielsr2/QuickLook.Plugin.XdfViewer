using System.Collections.Generic;

namespace QuickLook.Plugin.XdfViewer.Xdf
{
    /// <summary>One placed marker label: which candidate it came from, its left edge (px), and the
    /// stagger row it was assigned to (0 = topmost).</summary>
    internal struct LabelPlacement
    {
        public int Index;
        public double X;
        public int Row;

        public LabelPlacement(int index, double x, int row) { Index = index; X = x; Row = row; }
    }

    /// <summary>
    /// Pure (WPF-free) placement for the full-view marker labels. Walks candidates left to right and
    /// drops each into the first stagger row that still has clearance, skipping any label that would
    /// overlap in every row. Labels therefore never collide at any zoom, and zooming in — which
    /// spreads the x positions apart — reveals more of them without any density heuristic.
    /// </summary>
    internal static class MarkerLabelLayout
    {
        /// <param name="xs">Candidate left edges (px), ascending. Off-window candidates are the
        /// caller's job to exclude.</param>
        /// <param name="widths">Measured label widths (px), parallel to <paramref name="xs"/>.</param>
        /// <param name="rowCount">Stagger rows available; values below 1 are treated as 1.</param>
        /// <param name="maxLabels">Hard cap on placements returned.</param>
        /// <param name="gapPx">Minimum clearance between two labels sharing a row; negatives are 0.</param>
        public static IList<LabelPlacement> Place(IList<double> xs, IList<double> widths,
            int rowCount, int maxLabels, double gapPx)
        {
            var placed = new List<LabelPlacement>();
            if (xs == null || widths == null || maxLabels <= 0) return placed;

            int n = xs.Count < widths.Count ? xs.Count : widths.Count;
            if (rowCount < 1) rowCount = 1;
            if (gapPx < 0) gapPx = 0;

            // How far right each row is already occupied; -inf means the row is untouched.
            var occupiedTo = new double[rowCount];
            for (int r = 0; r < rowCount; r++) occupiedTo[r] = double.NegativeInfinity;

            for (int i = 0; i < n && placed.Count < maxLabels; i++)
            {
                double x = xs[i];
                for (int r = 0; r < rowCount; r++)
                {
                    if (x < occupiedTo[r]) continue;      // row still busy at this x
                    placed.Add(new LabelPlacement(i, x, r));
                    occupiedTo[r] = x + widths[i] + gapPx;
                    break;                                 // placed; no label goes in two rows
                }
                // No free row: the label is dropped. Its marker line is still drawn by the caller.
            }

            return placed;
        }
    }
}
