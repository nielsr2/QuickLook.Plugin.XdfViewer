using QuickLook.Plugin.XdfViewer.Xdf;
using QuickLook.Plugin.XdfViewer.Xdf.Model;

namespace QuickLook.Plugin.XdfViewer.ViewModels
{
    /// <summary>
    /// One marker/event for display. Its <see cref="TimeLabel"/> is shown relative to the recording
    /// start (min first_timestamp) rather than as the raw absolute LSL clock value, which is a large
    /// opaque number. Falls back to the raw timestamp when no recording start is known.
    /// </summary>
    public sealed class MarkerViewModel
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
    }
}
