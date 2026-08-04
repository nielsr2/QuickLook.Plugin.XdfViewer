using System.Collections.Generic;
using QuickLook.Plugin.XdfViewer.Xdf;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    /// <summary>
    /// Pure stagger/collision placement for the full-view marker labels: a left-to-right pass that
    /// drops each label into the first row with clearance and skips it when every row is occupied.
    /// No WPF.
    /// </summary>
    public class MarkerLabelLayoutTests
    {
        private const double Gap = 6d;

        private static IList<LabelPlacement> Place(double[] xs, double[] widths,
            int rows = 3, int max = 200, double gap = Gap)
            => MarkerLabelLayout.Place(xs, widths, rows, max, gap);

        [Fact]
        public void WellSeparated_AllLandInRowZero()
        {
            // 40px wide + 6px gap needs 46px of clearance; these are 100px apart.
            var p = Place(new[] { 0d, 100d, 200d }, new[] { 40d, 40d, 40d });
            Assert.Equal(3, p.Count);
            Assert.All(p, q => Assert.Equal(0, q.Row));
        }

        [Fact]
        public void TightCluster_FillsRowsTopFirst()
        {
            // 10px apart, 46px of clearance needed: each label pushes to the next free row.
            var p = Place(new[] { 0d, 10d, 20d }, new[] { 40d, 40d, 40d });
            Assert.Equal(3, p.Count);
            Assert.Equal(0, p[0].Row);
            Assert.Equal(1, p[1].Row);
            Assert.Equal(2, p[2].Row);
        }

        [Fact]
        public void ClusterDeeperThanRowCount_SkipsTheOverflow()
        {
            var p = Place(new[] { 0d, 10d, 20d, 30d }, new[] { 40d, 40d, 40d, 40d });
            Assert.Equal(3, p.Count);
            Assert.DoesNotContain(p, q => q.Index == 3);
            Assert.Equal(0, p[0].Index);
            Assert.Equal(1, p[1].Index);
            Assert.Equal(2, p[2].Index);
        }

        [Fact]
        public void NoTwoLabelsInTheSameRowOverlap()
        {
            var xs = new double[40];
            var widths = new double[40];
            for (int i = 0; i < 40; i++) { xs[i] = i * 17d; widths[i] = 50d; }

            var p = Place(xs, widths);

            var occupiedTo = new Dictionary<int, double>();
            foreach (var q in p)
            {
                double end;
                if (occupiedTo.TryGetValue(q.Row, out end))
                    Assert.True(q.X >= end);
                occupiedTo[q.Row] = q.X + widths[q.Index] + Gap;
            }
        }

        [Fact]
        public void MaxLabels_CapsTheResultInXOrder()
        {
            var xs = new double[10];
            var widths = new double[10];
            for (int i = 0; i < 10; i++) { xs[i] = i * 100d; widths[i] = 20d; }

            var p = Place(xs, widths, rows: 3, max: 2);

            Assert.Equal(2, p.Count);
            Assert.Equal(0, p[0].Index);
            Assert.Equal(1, p[1].Index);
        }

        [Fact]
        public void Placements_AreAscendingInX_WithUniqueValidIndices()
        {
            var xs = new double[30];
            var widths = new double[30];
            for (int i = 0; i < 30; i++) { xs[i] = i * 23d; widths[i] = 45d; }

            var p = Place(xs, widths);

            var seen = new HashSet<int>();
            for (int i = 0; i < p.Count; i++)
            {
                Assert.InRange(p[i].Index, 0, 29);
                Assert.True(seen.Add(p[i].Index));
                Assert.Equal(xs[p[i].Index], p[i].X, 6);
                if (i > 0) Assert.True(p[i].X >= p[i - 1].X);
            }
        }

        [Fact]
        public void RowCountBelowOne_BehavesAsASingleRow()
        {
            var p = Place(new[] { 0d, 10d, 200d }, new[] { 40d, 40d, 40d }, rows: 0);
            Assert.Equal(2, p.Count);
            Assert.All(p, q => Assert.Equal(0, q.Row));
            Assert.Equal(0, p[0].Index);
            Assert.Equal(2, p[1].Index);   // the 10px neighbour had nowhere to go
        }

        [Fact]
        public void MaxLabelsZero_ReturnsEmpty()
        {
            Assert.Empty(Place(new[] { 0d, 100d }, new[] { 10d, 10d }, max: 0));
        }

        [Fact]
        public void MismatchedListLengths_UseTheShorterList()
        {
            var p = Place(new[] { 0d, 100d, 200d }, new[] { 10d, 10d });
            Assert.Equal(2, p.Count);
        }

        [Fact]
        public void NegativeGap_BehavesAsZero()
        {
            // x = 40 exactly abuts the first label's right edge (0 + 40 + 0).
            var p = Place(new[] { 0d, 40d }, new[] { 40d, 40d }, rows: 1, gap: -5d);
            Assert.Equal(2, p.Count);
            Assert.All(p, q => Assert.Equal(0, q.Row));
        }

        [Fact]
        public void NullInputs_ReturnEmpty()
        {
            Assert.Empty(MarkerLabelLayout.Place(null, new[] { 10d }, 3, 200, Gap));
            Assert.Empty(MarkerLabelLayout.Place(new[] { 0d }, null, 3, 200, Gap));
        }
    }
}
