using System.Collections.Generic;
using QuickLook.Plugin.XdfViewer.Xdf;
using QuickLook.Plugin.XdfViewer.Xdf.Model;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    public class StreamKindClassifierTests
    {
        private static StreamKind Classify(
            string type, string name = null, string channelFormat = "float32",
            double nominalSampleRate = 250, IReadOnlyList<string> channelTypes = null,
            IReadOnlyList<string> channelUnits = null)
        {
            return StreamKindClassifier.Classify(type, name, channelFormat, nominalSampleRate, channelTypes, channelUnits);
        }

        // --- Canonical ---

        [Fact]
        public void Type_Eeg_Exact()
        {
            Assert.Equal(StreamKind.Eeg, Classify("EEG"));
        }

        [Fact]
        public void Type_MoCap_And_Mocap_BothMap()
        {
            Assert.Equal(StreamKind.MoCap, Classify("MoCap"));
            Assert.Equal(StreamKind.MoCap, Classify("Mocap"));
        }

        [Fact]
        public void Type_Gaze_And_LowercaseGaze()
        {
            Assert.Equal(StreamKind.Gaze, Classify("Gaze"));
            Assert.Equal(StreamKind.Gaze, Classify("gaze"));
        }

        [Fact]
        public void Type_Markers_Exact()
        {
            Assert.Equal(StreamKind.Markers, Classify("Markers", channelFormat: "string", nominalSampleRate: 0));
        }

        // --- Aliases ---

        [Theory]
        [InlineData("ACC", StreamKind.Accelerometer)]
        [InlineData("ACCEL", StreamKind.Accelerometer)]
        [InlineData("IMU", StreamKind.Accelerometer)]
        [InlineData("RESP", StreamKind.Respiration)]
        [InlineData("RESPIRATION", StreamKind.Respiration)]
        [InlineData("Breathing", StreamKind.Respiration)]
        [InlineData("EKG", StreamKind.Ecg)]
        [InlineData("EXG", StreamKind.Eeg)]
        [InlineData("OPTICS", StreamKind.Ppg)]
        public void Aliases_MapToExpectedKind(string type, StreamKind expected)
        {
            Assert.Equal(expected, Classify(type));
        }

        // --- Precedence ---

        [Fact]
        public void VideoCompressed_StringIrregular_IsVideoNotMarkers()
        {
            Assert.Equal(StreamKind.Video, Classify("VideoCompressed", channelFormat: "string", nominalSampleRate: 0));
        }

        [Fact]
        public void DeclaredType_WinsOver_Name()
        {
            Assert.Equal(StreamKind.Eeg, Classify("EEG", name: "gaze_events"));
        }

        [Fact]
        public void Name_UsedOnly_WhenTypeEmpty()
        {
            Assert.Equal(StreamKind.Gaze, Classify("", name: "gaze_events"));
        }

        // --- Suffix strip ---

        [Fact]
        public void GazeEvents_StripsToGaze()
        {
            Assert.Equal(StreamKind.Gaze, Classify("GazeEvents"));
        }

        [Fact]
        public void Gaze2d_StripsToGaze()
        {
            Assert.Equal(StreamKind.Gaze, Classify("Gaze2d"));
        }

        // --- Channel heuristic ---

        [Fact]
        public void ChannelTypes_Supermajority_Classifies()
        {
            var types = new[] { "EEG", "EEG", "EEG", "MISC" };
            Assert.Equal(StreamKind.Eeg, Classify("00:07:80:8C:07:08", channelTypes: types));
        }

        [Fact]
        public void ChannelTypes_SingleEogAmong64Eeg_StaysEeg()
        {
            var types = new List<string>();
            for (int i = 0; i < 63; i++) types.Add("EEG");
            types.Add("EOG");
            Assert.Equal(StreamKind.Eeg, Classify("00:07:80:8C:07:08", channelTypes: types));
        }

        // --- Anti-false-positive ---

        [Fact]
        public void Eeg_DoesNotMatch_Ecg()
        {
            Assert.NotEqual(StreamKind.Eeg, Classify("ECG"));
            Assert.Equal(StreamKind.Ecg, Classify("ECG"));
        }

        [Fact]
        public void MacAddressType_FallsBackToGeneric()
        {
            Assert.Equal(StreamKind.Generic, Classify("00:07:80:8C:07:08"));
        }

        [Theory]
        [InlineData("SIGNAL")]
        [InlineData("DATA")]
        [InlineData("SENSOR")]
        [InlineData("MISC")]
        [InlineData("RAW")]
        [InlineData("AUX")]
        [InlineData("PHYSIO")]
        [InlineData("QUALITY")]
        [InlineData("TELEMETRY")]
        [InlineData("HID")]
        public void MeaninglessTypes_FallBackToGeneric(string type)
        {
            Assert.Equal(StreamKind.Generic, Classify(type));
        }

        // --- Robustness ---

        [Fact]
        public void Null_Empty_Whitespace_ReturnGeneric()
        {
            Assert.Equal(StreamKind.Generic, Classify(null));
            Assert.Equal(StreamKind.Generic, Classify(""));
            Assert.Equal(StreamKind.Generic, Classify("   "));
        }

        [Fact]
        public void Classify_XdfStream_Overload_UsesStreamFields()
        {
            var s = new XdfStream { Type = "EEG", Name = "eeg_data", ChannelFormat = "float32", NominalSampleRate = 250 };
            Assert.Equal(StreamKind.Eeg, StreamKindClassifier.Classify(s));
        }

        [Fact]
        public void StructuralFallback_StringIrregular_IsMarkers()
        {
            Assert.Equal(StreamKind.Markers, Classify("", channelFormat: "string", nominalSampleRate: 0));
        }

        [Fact]
        public void StructuralFallback_StringRegular_IsGeneric()
        {
            Assert.Equal(StreamKind.Generic, Classify("", channelFormat: "string", nominalSampleRate: 500));
        }
    }
}
