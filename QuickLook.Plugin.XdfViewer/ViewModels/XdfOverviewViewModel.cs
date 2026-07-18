using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using QuickLook.Plugin.XdfViewer.Xdf.Model;

namespace QuickLook.Plugin.XdfViewer.ViewModels
{
    public sealed class XdfOverviewViewModel
    {
        public XdfOverviewViewModel(XdfFile file, string fileName)
        {
            FileName = fileName;
            IsTruncated = file.IsTruncated;
            Streams = new ObservableCollection<StreamCardViewModel>();
            foreach (var s in file.Streams)
                Streams.Add(new StreamCardViewModel(s));

            var sb = new StringBuilder();
            int n = file.Streams.Count;
            sb.Append(n).Append(n == 1 ? " stream" : " streams");
            if (file.TotalDuration.HasValue)
                sb.Append(" · ").Append(file.TotalDuration.Value.ToString("0.0", CultureInfo.InvariantCulture)).Append(" s");
            if (!string.IsNullOrEmpty(file.Version))
                sb.Append(" · XDF v").Append(file.Version);
            if (IsTruncated)
                sb.Append(" · file appears truncated");
            Summary = sb.ToString();
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
    }
}
