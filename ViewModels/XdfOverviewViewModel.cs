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
