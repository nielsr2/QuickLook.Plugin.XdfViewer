using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using QuickLook.Plugin.XdfViewer.Xdf;
using QuickLook.Plugin.XdfViewer.Xdf.Model;

namespace QuickLook.Plugin.XdfViewer.ViewModels
{
    public sealed class StreamCardViewModel : INotifyPropertyChanged
    {
        private static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        public StreamCardViewModel(XdfStream s, double? recordingStart = null)
        {
            var name = string.IsNullOrEmpty(s.Name) ? "(unnamed)" : s.Name;
            Title = string.IsNullOrEmpty(s.Type) ? name : $"{name} · {s.Type}";

            var fmt = string.IsNullOrEmpty(s.ChannelFormat) ? "?" : s.ChannelFormat;
            ChannelsLine = $"{s.ChannelCount} ch · {fmt}";

            var count = s.SampleCount.HasValue ? s.SampleCount.Value.ToString("N0", Ci) : "—";
            RateLine = s.NominalSampleRate > 0
                ? $"{FormatRate(s.NominalSampleRate)} · {count} samples"
                : $"irregular · {count} samples";

            DurationLine = s.Duration.HasValue ? TimeFormat.FormatDuration(s.Duration.Value) : "duration unknown";

            IsIncomplete = s.IsIncomplete;
            IsMarkerStream = s.IsMarkerStream;
            StreamId = s.Id;
            Channels = s.Channels.Select((c, i) => new ChannelViewModel(c, s, i)).ToList();
            Markers = s.Markers.Select(m => new MarkerViewModel(m, recordingStart)).ToList();
            MarkersTruncated = s.MarkersTruncated;

            ExpanderHeader = IsMarkerStream
                ? (s.Markers.Count > 0 ? $"{s.Markers.Count}{(MarkersTruncated ? "+" : "")} events" : "events")
                : (s.Channels.Count > 0 ? $"{s.Channels.Count} channels" : "channels");

            Kind = StreamKindClassifier.Classify(s);
            KindTooltip = Kind == StreamKind.Generic
                ? "Unclassified stream type"
                : $"{DisplayName(Kind)} — from stream type";
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
        public IReadOnlyList<MarkerViewModel> Markers { get; }
        public bool MarkersTruncated { get; }
        public StreamKind Kind { get; }
        public string KindTooltip { get; }

        /// <summary>True when a channel-search query is active and no channel in this (numeric) stream
        /// matches — drives the card's "no matching channels" hint.</summary>
        public bool NoChannelMatch
        {
            get => _noChannelMatch;
            private set
            {
                if (_noChannelMatch == value) return;
                _noChannelMatch = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NoChannelMatch)));
            }
        }
        private bool _noChannelMatch;
        public void SetNoChannelMatch(bool value) => NoChannelMatch = value;

        public event PropertyChangedEventHandler PropertyChanged;

        private static string FormatRate(double hz)
            => (hz >= 1000 ? (hz / 1000.0).ToString("0.###", Ci) + " kHz"
                           : hz.ToString("0.###", Ci) + " Hz");

        private static string DisplayName(StreamKind kind)
        {
            switch (kind)
            {
                case StreamKind.Eeg: return "EEG";
                case StreamKind.Ecg: return "ECG";
                case StreamKind.Emg: return "EMG";
                case StreamKind.Eog: return "EOG";
                case StreamKind.Gsr: return "GSR";
                case StreamKind.Ppg: return "PPG";
                case StreamKind.HeartRate: return "Heart rate";
                case StreamKind.MoCap: return "Motion capture";
                case StreamKind.Nirs: return "NIRS";
                default: return kind.ToString();
            }
        }
    }
}
