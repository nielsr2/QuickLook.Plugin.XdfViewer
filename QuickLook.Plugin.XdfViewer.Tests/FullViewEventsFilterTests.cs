using System.Collections.Generic;
using QuickLook.Plugin.XdfViewer.ViewModels;
using QuickLook.Plugin.XdfViewer.Xdf.Model;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    /// <summary>
    /// The full view's events-panel search: flips IsFilteredOut on the panel's own marker rows and
    /// words the header. Pure (WPF-free) so the panel's code-behind stays a thin delegate, mirroring
    /// the overview's XdfOverviewViewModel.ApplySearch split.
    /// </summary>
    public class FullViewEventsFilterTests
    {
        /// <summary>PsychoPy-shaped event values, matching the overview's search fixture.</summary>
        private static List<MarkerViewModel> Events()
        {
            return new List<MarkerViewModel>
            {
                new MarkerViewModel(new XdfMarker { Timestamp = 10, Value = "Start_7543_DrNo_Or.mp4" }, 0.0),
                new MarkerViewModel(new XdfMarker { Timestamp = 20, Value = "Cross | null | null" }, 0.0),
                new MarkerViewModel(new XdfMarker { Timestamp = 30, Value = "End_7543_DrNo_Or.mp4" }, 0.0)
            };
        }

        [Fact]
        public void Apply_EmptyQuery_MatchesEverythingAndReturnsTotal()
        {
            var events = Events();

            int matches = FullViewEventsFilter.Apply(events, "");

            Assert.Equal(3, matches);
            Assert.All(events, m => Assert.False(m.IsFilteredOut));
        }

        [Fact]
        public void Apply_FiltersByValueCaseInsensitive()
        {
            var events = Events();

            int matches = FullViewEventsFilter.Apply(events, "drno");

            Assert.Equal(2, matches);
            Assert.False(events[0].IsFilteredOut);
            Assert.True(events[1].IsFilteredOut);    // "Cross | null | null"
            Assert.False(events[2].IsFilteredOut);
        }

        [Fact]
        public void Apply_DoesNotMatchAgainstEventTimes()
        {
            var events = Events();

            // "20" is the second event's timestamp; only values are searched, so nothing matches.
            int matches = FullViewEventsFilter.Apply(events, "20");

            Assert.Equal(0, matches);
            Assert.All(events, m => Assert.True(m.IsFilteredOut));
        }

        [Fact]
        public void Apply_ClearingTheQuery_RestoresEveryRow()
        {
            var events = Events();
            FullViewEventsFilter.Apply(events, "drno");

            int matches = FullViewEventsFilter.Apply(events, "");

            Assert.Equal(3, matches);
            Assert.All(events, m => Assert.False(m.IsFilteredOut));
        }

        [Fact]
        public void Apply_RaisesPropertyChangedSoRowsActuallyHide()
        {
            var events = Events();
            var raised = new List<string>();
            events[1].PropertyChanged += (s, e) => raised.Add(e.PropertyName);

            FullViewEventsFilter.Apply(events, "drno");

            Assert.Contains(nameof(MarkerViewModel.IsFilteredOut), raised);
        }

        [Fact]
        public void Apply_NullList_ReturnsZero()
        {
            Assert.Equal(0, FullViewEventsFilter.Apply(null, "drno"));
        }

        [Fact]
        public void HeaderText_NoQuery_MirrorsOverviewWording()
        {
            Assert.Equal("3 events", FullViewEventsFilter.HeaderText(3, 3, false, ""));
            Assert.Equal("1 event", FullViewEventsFilter.HeaderText(1, 1, false, ""));
            Assert.Equal("500+ events", FullViewEventsFilter.HeaderText(500, 500, true, ""));
        }

        [Fact]
        public void HeaderText_Filtering_ShowsMatchOfTotal()
        {
            Assert.Equal("2 of 3 events", FullViewEventsFilter.HeaderText(2, 3, false, "drno"));
            Assert.Equal("1 of 1 event", FullViewEventsFilter.HeaderText(1, 1, false, "drno"));
        }

        [Fact]
        public void HeaderText_FilteringTruncatedTotal_KeepsThePlus()
        {
            Assert.Equal("1 of 500+ events", FullViewEventsFilter.HeaderText(1, 500, true, "drno"));
        }

        [Fact]
        public void HeaderText_FilteringWithNoMatch_ShowsZeroOfTotal()
        {
            Assert.Equal("0 of 3 events", FullViewEventsFilter.HeaderText(0, 3, false, "zzz"));
        }

        [Fact]
        public void HeaderText_WhitespaceQuery_CountsAsNoFilter()
        {
            // TextSearch treats a whitespace-only query as "no filter"; the header must agree,
            // or it would read "3 of 3 events" while nothing is actually being filtered.
            Assert.Equal("3 events", FullViewEventsFilter.HeaderText(3, 3, false, "   "));
        }
    }
}
