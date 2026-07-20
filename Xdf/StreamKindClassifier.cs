using System;
using System.Collections.Generic;
using System.Linq;
using QuickLook.Plugin.XdfViewer.Xdf.Model;

namespace QuickLook.Plugin.XdfViewer.Xdf
{
    /// <summary>
    /// Pure (WPF-free) heuristic classifier that maps an XDF stream's declared type, name and
    /// channel metadata onto a <see cref="StreamKind"/>. The XDF spec only defines eight canonical
    /// content types; everything else is vendor convention, so this degrades to
    /// <see cref="StreamKind.Generic"/> whenever it cannot make a confident call. The classification
    /// is decorative — the stream card title always shows the raw type verbatim alongside it.
    /// </summary>
    internal static class StreamKindClassifier
    {
        private static readonly string[] SuffixesToStrip = { "EVENTS", "EVENT", "MARKERS", "DATA", "RAW", "2D", "3D" };

        private static readonly Dictionary<string, StreamKind> Aliases = BuildAliasTable();

        public static StreamKind Classify(XdfStream stream)
        {
            if (stream == null) return StreamKind.Generic;
            var channelTypes = stream.Channels?.Select(c => c.Type).ToList();
            var channelUnits = stream.Channels?.Select(c => c.Unit).ToList();
            return Classify(stream.Type, stream.Name, stream.ChannelFormat, stream.NominalSampleRate, channelTypes, channelUnits);
        }

        public static StreamKind Classify(
            string type, string name, string channelFormat, double nominalSampleRate,
            IReadOnlyList<string> channelTypes, IReadOnlyList<string> channelUnits)
        {
            var normalizedType = Normalize(type);

            // Rule 0: video guard — must run before any string-format check.
            if (normalizedType == "VIDEORAW" || normalizedType == "VIDEOCOMPRESSED")
                return StreamKind.Video;

            // Rules 1-3: exact match, alias match, suffix-strip retry — against the declared type.
            if (normalizedType.Length > 0)
            {
                var byType = ClassifyToken(normalizedType);
                if (byType != StreamKind.Generic) return byType;
            }

            // Rule 4: name inference, only when type is empty/unresolved.
            if (normalizedType.Length == 0)
            {
                var byName = ClassifyName(name);
                if (byName != StreamKind.Generic) return byName;
            }

            // Rule 5: channel metadata supermajority.
            var byChannelType = ClassifyChannelSupermajority(channelTypes);
            if (byChannelType != StreamKind.Generic) return byChannelType;

            var byChannelUnit = ClassifyChannelSupermajority(channelUnits);
            if (byChannelUnit != StreamKind.Generic) return byChannelUnit;

            // Rule 6: structural fallback. cf_string + irregular rate -> Markers; cf_string regular -> Generic.
            var isStringFormat = string.Equals(channelFormat, "string", StringComparison.OrdinalIgnoreCase);
            if (isStringFormat && nominalSampleRate == 0)
                return StreamKind.Markers;

            // Rule 7.
            return StreamKind.Generic;
        }

        private static StreamKind ClassifyName(string name)
        {
            var normalizedName = Normalize(name);
            if (normalizedName.Length == 0) return StreamKind.Generic;

            var byWhole = ClassifyToken(normalizedName);
            if (byWhole != StreamKind.Generic) return byWhole;

            foreach (var token in SplitOnSeparators(name))
            {
                var byToken = ClassifyToken(Normalize(token));
                if (byToken != StreamKind.Generic) return byToken;
            }

            return StreamKind.Generic;
        }

        private static IEnumerable<string> SplitOnSeparators(string name)
        {
            if (string.IsNullOrEmpty(name)) yield break;
            var parts = name.Split(new[] { '_', '-', ' ', '.', '/' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts) yield return part;
        }

        private static StreamKind ClassifyChannelSupermajority(IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0) return StreamKind.Generic;

            var counts = new Dictionary<StreamKind, int>();
            int classified = 0;
            foreach (var v in values)
            {
                var normalized = Normalize(v);
                if (normalized.Length == 0) continue;
                var kind = ClassifyToken(normalized);
                if (kind == StreamKind.Generic) continue;
                classified++;
                counts[kind] = counts.TryGetValue(kind, out var c) ? c + 1 : 1;
            }

            if (classified == 0) return StreamKind.Generic;

            foreach (var kv in counts)
            {
                if (kv.Value >= 0.6 * values.Count) return kv.Key;
            }

            return StreamKind.Generic;
        }

        /// <summary>Applies rules 1-3 (exact, alias, single suffix-strip retry) to an already-normalized token.</summary>
        private static StreamKind ClassifyToken(string normalized)
        {
            if (normalized.Length == 0) return StreamKind.Generic;

            if (Aliases.TryGetValue(normalized, out var kind)) return kind;

            foreach (var suffix in SuffixesToStrip)
            {
                if (normalized.Length > suffix.Length && normalized.EndsWith(suffix, StringComparison.Ordinal))
                {
                    var stripped = normalized.Substring(0, normalized.Length - suffix.Length);
                    if (Aliases.TryGetValue(stripped, out var strippedKind)) return strippedKind;
                }
            }

            return StreamKind.Generic;
        }

        internal static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var chars = s.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray();
            return new string(chars);
        }

        private static Dictionary<string, StreamKind> BuildAliasTable()
        {
            var map = new Dictionary<string, StreamKind>();

            void Add(StreamKind kind, params string[] tokens)
            {
                foreach (var t in tokens) map[t] = kind;
            }

            Add(StreamKind.Eeg, "EEG", "EEGS", "ELECTROENCEPHALOGRAM", "EXG", "BRAINWAVE");
            Add(StreamKind.Ecg, "ECG", "EKG", "ELECTROCARDIOGRAM", "CARDIAC");
            Add(StreamKind.Emg, "EMG", "ELECTROMYOGRAM", "ELECTROMYOGRAPHY");
            Add(StreamKind.Eog, "EOG", "ELECTROOCULOGRAM");
            Add(StreamKind.Respiration, "RESP", "RESPIRATION", "RESPIRATORY", "BREATHING", "BREATH", "RIP", "PZT");
            Add(StreamKind.Gsr, "GSR", "EDA", "SKINCONDUCTANCE", "ELECTRODERMAL", "SC");
            Add(StreamKind.Ppg, "PPG", "PLETH", "PHOTOPLETHYSMOGRAM", "PULSEOX", "SPO2", "OXIMETER", "OPTICS");
            Add(StreamKind.HeartRate, "HR", "HEARTRATE", "RR", "HRV", "IBI", "BPM");
            Add(StreamKind.Accelerometer, "ACC", "ACCEL", "ACCELEROMETER", "IMU", "MOTION", "ORN");
            Add(StreamKind.Gyroscope, "GYRO", "GYROSCOPE", "ANGULARRATE");
            Add(StreamKind.Magnetometer, "MAG", "MAGNETOMETER", "COMPASS");
            Add(StreamKind.Gaze, "GAZE", "EYE", "EYETRACKER", "EYETRACKING", "PUPIL", "PUPILCAPTURE", "FIXATIONS", "SCREENGAZE");
            Add(StreamKind.MoCap, "MOCAP", "MOTIONCAPTURE", "POSITION", "POSE", "QUATERNION", "SKELETON");
            Add(StreamKind.Nirs, "NIRS", "FNIRS", "HBO", "HBR");
            Add(StreamKind.Audio, "AUDIO", "MIC", "MICROPHONE", "SOUND", "PCM");
            Add(StreamKind.Video, "VIDEORAW", "VIDEOCOMPRESSED", "VIDEO", "CAMERA", "WEBCAM");
            Add(StreamKind.Markers, "MARKERS", "MARKER", "EVENTS", "EVENT", "TRIGGER", "TRIGGERS", "STIM", "STIMULI",
                "ANNOTATIONS", "SAMPLEDMARKERS", "SYNC", "KEYBOARD", "MOUSEBUTTONS");
            Add(StreamKind.Force, "FORCE", "FORCEPLATE", "LOADCELL", "TORQUE");
            Add(StreamKind.Temperature, "TEMP", "TEMPERATURE", "SKINTEMP");

            return map;
        }
    }
}
