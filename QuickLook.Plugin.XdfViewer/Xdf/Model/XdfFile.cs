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
                var firsts = Streams.Where(s => s.FirstTimestamp.HasValue)
                                    .Select(s => s.FirstTimestamp.Value).ToList();
                var lasts = Streams.Where(s => s.LastTimestamp.HasValue)
                                   .Select(s => s.LastTimestamp.Value).ToList();
                if (firsts.Count > 0 && lasts.Count > 0)
                    return lasts.Max() - firsts.Min();
                return Streams.Select(s => s.Duration).Where(d => d.HasValue)
                              .Select(d => d.Value).DefaultIfEmpty().Max() is double m && m > 0 ? m : (double?)null;
            }
        }
    }
}
