using System.ComponentModel;
using QuickLook.Plugin.XdfViewer.Xdf.Model;

namespace QuickLook.Plugin.XdfViewer.ViewModels
{
    public sealed class ChannelViewModel : INotifyPropertyChanged
    {
        private readonly XdfChannel _channel;
        private readonly XdfStream _stream;
        private readonly int _channelIndex;
        private ChannelEnvelope _sparkline;
        private ChannelEnvelope _detail;
        private ChannelEnvelope _fullDetail;
        private bool _isExpanded;
        private bool _isFilteredOut;

        public ChannelViewModel(XdfChannel channel, XdfStream stream, int channelIndex = 0)
        {
            _channel = channel;
            _stream = stream;
            _channelIndex = channelIndex;
        }

        public string Label => _channel.Label;
        public string Unit => _channel.Unit;

        /// <summary>Label and unit as one string, for the row's hover tooltip. The row itself trims
        /// with an ellipsis once the sparkline takes its share of the width, so this is the only way
        /// to read a long channel name in full.</summary>
        public string LabelWithUnit =>
            string.IsNullOrEmpty(Unit) ? Label : Label + " " + Unit;

        /// <summary>Id of the owning stream (used to request an on-demand hi-res envelope read).</summary>
        public uint StreamId => _stream.Id;

        /// <summary>Index of this channel within its stream (to pick its envelope out of a StreamEnvelope).</summary>
        public int ChannelIndex => _channelIndex;

        /// <summary>Total sample count of the owning stream (to size the full-resolution view).</summary>
        public long? StreamSampleCount => _stream.SampleCount;

        /// <summary>Duration of the owning stream, in seconds (for the expanded time axis).</summary>
        public double? StreamDurationSeconds => _stream.Duration;

        /// <summary>First timestamp of the owning stream (the full view's time-zero; anchors the
        /// marker overlay). Null when the stream has no timestamps.</summary>
        public double? StreamFirstTimestamp => _stream.FirstTimestamp;

        public ChannelEnvelope Sparkline
        {
            get => _sparkline;
            private set
            {
                _sparkline = value;
                Raise(nameof(Sparkline));
                Raise(nameof(HasWaveform));
                Raise(nameof(IsFlat));
            }
        }

        public ChannelEnvelope Detail
        {
            get => _detail;
            private set
            {
                _detail = value;
                Raise(nameof(Detail));
            }
        }

        /// <summary>Full-resolution envelope for the entire channel (the full-window scrollable view).</summary>
        public ChannelEnvelope FullDetail
        {
            get => _fullDetail;
            private set
            {
                _fullDetail = value;
                Raise(nameof(FullDetail));
            }
        }

        public bool HasWaveform => Sparkline != null && !Sparkline.IsEmpty;

        /// <summary>
        /// True when the channel carries data but never changes (constant value throughout the
        /// previewed samples, e.g. all zeros) — usually a disconnected or unused sensor. Drives the
        /// grey warning marker in the channel row.
        /// </summary>
        public bool IsFlat => Sparkline != null && !Sparkline.IsEmpty && Sparkline.DataMax <= Sparkline.DataMin;

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value) return;
                _isExpanded = value;
                Raise(nameof(IsExpanded));
            }
        }

        /// <summary>True when the channel-search box is filtering this channel out of view. Drives the
        /// row's visibility.</summary>
        public bool IsFilteredOut
        {
            get => _isFilteredOut;
            set
            {
                if (_isFilteredOut == value) return;
                _isFilteredOut = value;
                Raise(nameof(IsFilteredOut));
            }
        }

        /// <summary>Re-reads the channel's envelope from the model (e.g. after background decimation completes).</summary>
        public void RefreshEnvelope()
        {
            Sparkline = _channel.Envelope;
        }

        /// <summary>Writes a freshly computed envelope onto the underlying channel model, then refreshes.</summary>
        public void ApplyEnvelope(ChannelEnvelope envelope)
        {
            _channel.Envelope = envelope;
            RefreshEnvelope();
        }

        public void SetDetail(ChannelEnvelope detail)
        {
            Detail = detail;
        }

        public void SetFullDetail(ChannelEnvelope full)
        {
            FullDetail = full;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
