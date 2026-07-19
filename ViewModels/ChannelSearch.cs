namespace QuickLook.Plugin.XdfViewer.ViewModels
{
    /// <summary>
    /// Pure (WPF-free) predicate for the channel-search box: case-insensitive substring match on a
    /// channel label. An empty or whitespace-only query matches every channel.
    /// </summary>
    internal static class ChannelSearch
    {
        public static bool Matches(string label, string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return true;   // no filter -> everything matches
            if (label == null) return false;
            return label.IndexOf(query.Trim(), System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
