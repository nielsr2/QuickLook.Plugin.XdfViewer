namespace QuickLook.Plugin.XdfViewer.ViewModels
{
    /// <summary>
    /// Pure (WPF-free) predicate for the search box: case-insensitive substring match. An empty or
    /// whitespace-only query matches everything. Used for both channel labels and marker values —
    /// hence the neutral name; it knows nothing about either.
    /// </summary>
    internal static class TextSearch
    {
        public static bool Matches(string text, string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return true;   // no filter -> everything matches
            if (text == null) return false;
            return text.IndexOf(query.Trim(), System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
