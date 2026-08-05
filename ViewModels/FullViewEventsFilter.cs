using System.Collections.Generic;

namespace QuickLook.Plugin.XdfViewer.ViewModels
{
    /// <summary>
    /// Search for the full view's events panel: flags the rows a query hides and words the header.
    /// Lives here rather than in the panel's code-behind because it is view-model state, not view
    /// state — which also makes it testable without a WPF host, exactly as
    /// <see cref="XdfOverviewViewModel.ApplySearch"/> is for the overview.
    /// </summary>
    internal static class FullViewEventsFilter
    {
        /// <summary>Flags every event the query does not match and returns how many survive. Only the
        /// event's value is searched, never its time — the overview does the same, because a query
        /// like "5" would otherwise match nearly every timestamp.</summary>
        public static int Apply(IReadOnlyList<MarkerViewModel> markers, string query)
        {
            if (markers == null) return 0;

            int matches = 0;
            for (int i = 0; i < markers.Count; i++)
            {
                bool match = TextSearch.Matches(markers[i].Value, query);
                markers[i].IsFilteredOut = !match;
                if (match) matches++;
            }
            return matches;
        }

        /// <summary>The panel header: "3 events" unfiltered, "2 of 3 events" while a query is active.
        /// A truncated read keeps its "+" in both forms so it is never presented as complete, and the
        /// noun agrees with the total via <see cref="StreamCardViewModel.Plural"/> — the same helper
        /// the overview card uses, so panel and card cannot word one count two ways.</summary>
        public static string HeaderText(int matches, int total, bool truncated, string query)
        {
            string totalText = total + (truncated ? "+" : "") + " "
                             + StreamCardViewModel.Plural(total, truncated, "event");

            // Whitespace-only is "no filter" to TextSearch; the header has to agree, or an all-spaces
            // query would read "3 of 3 events" while every row is still showing.
            return string.IsNullOrWhiteSpace(query) ? totalText : matches + " of " + totalText;
        }
    }
}
