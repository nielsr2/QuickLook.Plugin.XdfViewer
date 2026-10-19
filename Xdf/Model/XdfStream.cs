using System.Collections.Generic;

namespace QuickLook.Plugin.XdfViewer.Xdf.Model
{
    public sealed class XdfStream
    {
        public uint Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public int ChannelCount { get; set; }
        public double NominalSampleRate { get; set; }
        public string ChannelFormat { get; set; }

        public double? FirstTimestamp { get; set; }
        public double? LastTimestamp { get; set; }
        public long? SampleCount { get; set; }
        public double? MeasuredSampleRate { get; set; }

        /// <summary>Sample count summed from Samples chunks; fallback when no footer.</summary>
        public long TalliedSampleCount { get; set; }

        /// <summary>True once a StreamFooter chunk was seen, even one missing some fields.</summary>
        public bool HasFooter { get; set; }

        public bool IsIncomplete { get; set; }
        public bool MarkersTruncated { get; set; }

        public List<XdfChannel> Channels { get; } = new List<XdfChannel>();
        public List<XdfMarker> Markers { get; } = new List<XdfMarker>();

        public bool IsMarkerStream =>
            string.Equals(ChannelFormat, "string", System.StringComparison.OrdinalIgnoreCase) &&
            NominalSampleRate == 0 &&
            !string.Equals(Type, "VideoRaw", System.StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(Type, "VideoCompressed", System.StringComparison.OrdinalIgnoreCase);

        public double? Duration
        {
            get
            {
                if (FirstTimestamp.HasValue && LastTimestamp.HasValue)
                    return LastTimestamp.Value - FirstTimestamp.Value;
                if (NominalSampleRate > 0 && SampleCount.HasValue)
                    return SampleCount.Value / NominalSampleRate;
                return null;
            }
        }
    }
}
