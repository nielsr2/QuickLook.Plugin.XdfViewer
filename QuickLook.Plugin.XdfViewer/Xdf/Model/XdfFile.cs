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

        /// <summary>
        /// Cached Pass-1 chunk index, populated on the first <c>FillEnvelopes</c> call and reused by
        /// all later envelope reads so they skip the full-file walk. Internal implementation detail;
        /// not part of the parsed model.
        /// </summary>
        internal XdfChunkIndex ChunkIndex { get; set; }

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
