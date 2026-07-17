using System;
using System.Collections.Generic;
using System.Linq;

namespace QuickLook.Plugin.XdfViewer.Xdf.Model
{
    public sealed class XdfFile
    {
        public string Version { get; set; }
        public DateTimeOffset? RecordedAt { get; set; }
        public bool IsTruncated { get; set; }
        public List<XdfStream> Streams { get; } = new List<XdfStream>();

        /// <summary>Wall-clock span from the earliest first_timestamp to the latest last_timestamp.</summary>
        public double? TotalDuration
        {
            get
            {
                var withBoth = Streams
                    .Where(s => s.FirstTimestamp.HasValue && s.LastTimestamp.HasValue)
                    .ToList();
                if (withBoth.Count == 0) return null;
                return withBoth.Max(s => s.LastTimestamp.Value) - withBoth.Min(s => s.FirstTimestamp.Value);
            }
        }
    }
}
