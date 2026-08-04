using System.Collections.ObjectModel;
using System.Text;
using QuickLook.Plugin.XdfViewer.Xdf;
using QuickLook.Plugin.XdfViewer.Xdf.Model;

namespace QuickLook.Plugin.XdfViewer.ViewModels
{
    public sealed class XdfOverviewViewModel
    {
        public XdfOverviewViewModel(XdfFile file, string fileName)
        {
            FileName = fileName;
            IsTruncated = file.IsTruncated;

            // Zero for recording-relative event times: the recording start, or (footerless files)
            // the earliest marker timestamp across the file.
            double? recordingStart = file.RecordingStart ?? EarliestMarker(file);

            Streams = new ObservableCollection<StreamCardViewModel>();
            foreach (var s in file.Streams)
                Streams.Add(new StreamCardViewModel(s, recordingStart));

            var sb = new StringBuilder();
            int n = file.Streams.Count;
            sb.Append(n).Append(n == 1 ? " stream" : " streams");
            if (file.TotalDuration.HasValue)
                sb.Append(" · ").Append(TimeFormat.FormatDuration(file.TotalDuration.Value));
            if (!string.IsNullOrEmpty(file.Version))
                sb.Append(" · XDF v").Append(file.Version);
            if (IsTruncated)
                sb.Append(" · file appears truncated");
            Summary = sb.ToString();
        }

        private static double? EarliestMarker(XdfFile file)
        {
            double? min = null;
            foreach (var s in file.Streams)
                foreach (var m in s.Markers)
                    if (!min.HasValue || m.Timestamp < min.Value) min = m.Timestamp;
            return min;
        }

        public string FileName { get; }
        public string Summary { get; }
        public bool IsTruncated { get; }
        public ObservableCollection<StreamCardViewModel> Streams { get; }

        /// <summary>
        /// Applies the search box's query across every card: numeric streams match on the channel
        /// label, marker streams on the event value. Cards left with nothing visible are flagged so
        /// they can say so. An empty or whitespace query clears the filter and unflags everything.
        ///
        /// Marker <em>times</em> are deliberately not searched: a query like "5" would match most
        /// timestamps and drown the event names the box exists to find.
        ///
        /// Lives here rather than in the panel's code-behind because it is view-model state, not
        /// view state — which also makes it testable without a WPF host.
        /// </summary>
        public void ApplySearch(string query)
        {
            bool filtering = !string.IsNullOrWhiteSpace(query);

            foreach (var stream in Streams)
            {
                int visible = 0;
                if (stream.IsMarkerStream)
                {
                    foreach (var m in stream.Markers)
                    {
                        bool match = TextSearch.Matches(m.Value, query);
                        m.IsFilteredOut = !match;
                        if (match) visible++;
                    }
                    stream.SetNoMarkerMatch(stream.Markers.Count > 0 && visible == 0 && filtering);
                }
                else
                {
                    foreach (var ch in stream.Channels)
                    {
                        bool match = TextSearch.Matches(ch.Label, query);
                        ch.IsFilteredOut = !match;
                        if (match) visible++;
                    }
                    stream.SetNoChannelMatch(stream.Channels.Count > 0 && visible == 0 && filtering);
                }
            }
        }

        /// <summary>
        /// Applies freshly computed sparkline envelopes to the matching stream/channel view models.
        /// Must be called on the UI thread (the caller is responsible for marshalling).
        /// </summary>
        public void ApplyEnvelopes(StreamEnvelope[] envelopes)
        {
            if (envelopes == null) return;

            foreach (var streamEnv in envelopes)
            {
                if (streamEnv?.Channels == null) continue;

                StreamCardViewModel stream = null;
                foreach (var candidate in Streams)
                {
                    if (candidate.StreamId == streamEnv.StreamId)
                    {
                        stream = candidate;
                        break;
                    }
                }
                if (stream == null) continue;

                var count = System.Math.Min(stream.Channels.Count, streamEnv.Channels.Length);
                for (int j = 0; j < count; j++)
                    stream.Channels[j].ApplyEnvelope(streamEnv.Channels[j]);
            }
        }

        /// <summary>
        /// Applies a freshly computed hi-res "detail" envelope (from an on-demand, single-stream
        /// re-read) to the matching stream/channel view models' <see cref="ChannelViewModel.Detail"/>.
        /// Must be called on the UI thread (the caller is responsible for marshalling).
        /// </summary>
        public void ApplyDetail(StreamEnvelope[] envelopes)
        {
            if (envelopes == null) return;

            foreach (var streamEnv in envelopes)
            {
                if (streamEnv?.Channels == null) continue;

                StreamCardViewModel stream = null;
                foreach (var candidate in Streams)
                {
                    if (candidate.StreamId == streamEnv.StreamId)
                    {
                        stream = candidate;
                        break;
                    }
                }
                if (stream == null) continue;

                var count = System.Math.Min(stream.Channels.Count, streamEnv.Channels.Length);
                for (int j = 0; j < count; j++)
                    stream.Channels[j].SetDetail(streamEnv.Channels[j]);
            }
        }
    }
}
