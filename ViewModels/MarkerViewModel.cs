using System.ComponentModel;
using QuickLook.Plugin.XdfViewer.Xdf;
using QuickLook.Plugin.XdfViewer.Xdf.Model;

namespace QuickLook.Plugin.XdfViewer.ViewModels
{
    /// <summary>
    /// One marker/event for display. Its <see cref="TimeLabel"/> is shown relative to the recording
    /// start (min first_timestamp) rather than as the raw absolute LSL clock value, which is a large
    /// opaque number. Falls back to the raw timestamp when no recording start is known.
    /// </summary>
    public sealed class MarkerViewModel : INotifyPropertyChanged
    {
        public MarkerViewModel(XdfMarker marker, double? recordingStart)
        {
            Value = marker.Value;
            RelativeSeconds = recordingStart.HasValue ? marker.Timestamp - recordingStart.Value : marker.Timestamp;
            TimeLabel = TimeFormat.FormatClock(RelativeSeconds);
        }

        public string TimeLabel { get; }
        public string Value { get; }

        /// <summary>Seconds from the recording start — the same zero <see cref="TimeLabel"/> displays —
        /// or the raw timestamp when no start is known. The full view's events panel maps this to a
        /// sample index to centre the plot on the event.</summary>
        public double RelativeSeconds { get; }

        /// <summary>True when a search query is active and this event's value does not match, which
        /// collapses its row on the card. Mirrors ChannelViewModel.IsFilteredOut.</summary>
        public bool IsFilteredOut
        {
            get => _isFilteredOut;
            set
            {
                if (_isFilteredOut == value) return;
                _isFilteredOut = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFilteredOut)));
            }
        }
        private bool _isFilteredOut;

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
