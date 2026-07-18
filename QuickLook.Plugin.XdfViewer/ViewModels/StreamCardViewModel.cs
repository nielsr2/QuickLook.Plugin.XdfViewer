using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using QuickLook.Plugin.XdfViewer.Xdf.Model;

namespace QuickLook.Plugin.XdfViewer.ViewModels
{
    public sealed class StreamCardViewModel
    {
        private static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        public StreamCardViewModel(XdfStream s)
        {
            var name = string.IsNullOrEmpty(s.Name) ? "(unnamed)" : s.Name;
            Title = string.IsNullOrEmpty(s.Type) ? name : $"{name} · {s.Type}";

            var fmt = string.IsNullOrEmpty(s.ChannelFormat) ? "?" : s.ChannelFormat;
            ChannelsLine = $"{s.ChannelCount} ch · {fmt}";

            var count = s.SampleCount.HasValue ? s.SampleCount.Value.ToString("N0", Ci) : "—";
            RateLine = s.NominalSampleRate > 0
                ? $"{FormatRate(s.NominalSampleRate)} · {count} samples"
                : $"irregular · {count} samples";

            if (s.Duration.HasValue)
            {
                DurationLine = $"{s.Duration.Value.ToString("0.0", Ci)} s";
                if (s.FirstTimestamp.HasValue && s.LastTimestamp.HasValue)
                    DurationLine += $" ({s.FirstTimestamp.Value.ToString("0.00", Ci)}–{s.LastTimestamp.Value.ToString("0.00", Ci)})";
            }
            else DurationLine = "duration unknown";

            IsIncomplete = s.IsIncomplete;
            IsMarkerStream = s.IsMarkerStream;
            StreamId = s.Id;
            Channels = s.Channels.Select((c, i) => new ChannelViewModel(c, s, i)).ToList();
            Markers = s.Markers;
            MarkersTruncated = s.MarkersTruncated;

            ExpanderHeader = IsMarkerStream
                ? (s.Markers.Count > 0 ? $"{s.Markers.Count}{(MarkersTruncated ? "+" : "")} events" : "events")
                : (s.Channels.Count > 0 ? $"{s.Channels.Count} channels" : "channels");
        }

        public string Title { get; }
        public string ChannelsLine { get; }
        public string RateLine { get; }
        public string DurationLine { get; }
        public string ExpanderHeader { get; }
        public bool IsIncomplete { get; }
        public bool IsMarkerStream { get; }
        public uint StreamId { get; }
        public IReadOnlyList<ChannelViewModel> Channels { get; }
        public IReadOnlyList<XdfMarker> Markers { get; }
        public bool MarkersTruncated { get; }

        private static string FormatRate(double hz)
            => (hz >= 1000 ? (hz / 1000.0).ToString("0.###", Ci) + " kHz"
                           : hz.ToString("0.###", Ci) + " Hz");
    }
}
