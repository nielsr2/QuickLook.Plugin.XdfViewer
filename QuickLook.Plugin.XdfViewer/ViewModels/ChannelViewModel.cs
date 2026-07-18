using System.ComponentModel;
using QuickLook.Plugin.XdfViewer.Xdf.Model;

namespace QuickLook.Plugin.XdfViewer.ViewModels
{
    public sealed class ChannelViewModel : INotifyPropertyChanged
    {
        private readonly XdfChannel _channel;
        private readonly XdfStream _stream;
        private ChannelEnvelope _sparkline;
        private ChannelEnvelope _detail;
        private bool _isExpanded;

        public ChannelViewModel(XdfChannel channel, XdfStream stream)
        {
            _channel = channel;
            _stream = stream;
        }

        public string Label => _channel.Label;
        public string Unit => _channel.Unit;

        /// <summary>Id of the owning stream (used to request an on-demand hi-res envelope read).</summary>
        public uint StreamId => _stream.Id;

        /// <summary>Duration of the owning stream, in seconds (for the expanded time axis).</summary>
        public double? StreamDurationSeconds => _stream.Duration;

        public ChannelEnvelope Sparkline
        {
            get => _sparkline;
            private set
            {
                _sparkline = value;
                Raise(nameof(Sparkline));
                Raise(nameof(HasWaveform));
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

        public bool HasWaveform => Sparkline != null && !Sparkline.IsEmpty;

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

        public event PropertyChangedEventHandler PropertyChanged;

        private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
