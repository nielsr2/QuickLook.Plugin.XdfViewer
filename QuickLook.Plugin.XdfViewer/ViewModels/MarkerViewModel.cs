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
            double rel = recordingStart.HasValue ? marker.Timestamp - recordingStart.Value : marker.Timestamp;
            TimeLabel = TimeFormat.FormatClock(rel);
        }

        public string TimeLabel { get; }
        public string Value { get; }
    }
}
