using QuickLook.Plugin.XdfViewer.ViewModels;
using QuickLook.Plugin.XdfViewer.Xdf.Model;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    public class ViewModelTests
    {
        [Fact]
        public void RegularStreamCard_FormatsLines()
        {
            var s = new XdfStream
            {
                Name = "BioSemi", Type = "EEG", ChannelCount = 5,
                ChannelFormat = "float32", NominalSampleRate = 512,
                FirstTimestamp = 0, LastTimestamp = 63.5, SampleCount = 32512
            };
            var vm = new StreamCardViewModel(s);
            Assert.Equal("BioSemi · EEG", vm.Title);
            Assert.Equal("5 ch · float32", vm.ChannelsLine);
            Assert.Equal("512 Hz · 32,512 samples", vm.RateLine);
            Assert.Equal("1m 03s", vm.DurationLine); // 63.5 s rolls up to minutes
            Assert.False(vm.IsIncomplete);
        }

        [Fact]
        public void StreamCard_ChannelViewModels_ExposeLabelAndUnit()
        {
            var s = new XdfStream
            {
                Name = "BioSemi", Type = "EEG", ChannelCount = 2,
                ChannelFormat = "float32", NominalSampleRate = 512,
                FirstTimestamp = 0, LastTimestamp = 63.5, SampleCount = 32512
            };
            s.Channels.Add(new XdfChannel { Label = "Fp1", Unit = "uV", Type = "EEG" });
            s.Channels.Add(new XdfChannel { Label = "Fp2", Unit = "uV", Type = "EEG" });

            var vm = new StreamCardViewModel(s);

            Assert.Equal(2, vm.Channels.Count);
            Assert.Equal("Fp1", vm.Channels[0].Label);
            Assert.Equal("uV", vm.Channels[0].Unit);
            Assert.Equal("Fp2", vm.Channels[1].Label);
            Assert.Equal("2 channels", vm.ExpanderHeader);
        }

        [Fact]
        public void IrregularMarkerCard_UsesIrregularLabel()
        {
            var s = new XdfStream
            {
                Name = "Markers", Type = "Markers", ChannelCount = 1,
                ChannelFormat = "string", NominalSampleRate = 0, SampleCount = 18
            };
            var vm = new StreamCardViewModel(s);
            Assert.Equal("irregular · 18 samples", vm.RateLine);
            Assert.True(vm.IsMarkerStream);
        }

        [Fact]
        public void Overview_BuildsSummaryAndCards()
        {
            var file = new XdfFile { Version = "1.0" };
            file.Streams.Add(new XdfStream { Name = "A", Type = "EEG", ChannelFormat = "float32",
                                             ChannelCount = 5, NominalSampleRate = 512,
                                             FirstTimestamp = 0, LastTimestamp = 63.5, SampleCount = 10 });
            var vm = new XdfOverviewViewModel(file, "rec.xdf");
            Assert.Equal("rec.xdf", vm.FileName);
            Assert.Single(vm.Streams);
            Assert.Contains("1 stream", vm.Summary);
            Assert.Contains("XDF v1.0", vm.Summary);
        }

        [Fact]
        public void Overview_FooterlessStream_DerivesDurationFromSampleCountAndRate()
        {
            var file = new XdfFile();
            // No first/last timestamps (interrupted recording); 30000 samples @ 100 Hz = 300 s.
            file.Streams.Add(new XdfStream
            {
                Name = "A", ChannelFormat = "float32", ChannelCount = 1,
                NominalSampleRate = 100, SampleCount = 30000
            });
            var vm = new XdfOverviewViewModel(file, "rec.xdf");
            Assert.Contains("5m 00s", vm.Summary); // 300 s
        }

        [Fact]
        public void MarkerCard_ShowsTimesRelativeToRecordingStart()
        {
            var file = new XdfFile();
            file.Streams.Add(new XdfStream
            {
                Name = "Data", ChannelFormat = "float32", ChannelCount = 1,
                NominalSampleRate = 100, FirstTimestamp = 1000, LastTimestamp = 1005, SampleCount = 500
            });
            var markers = new XdfStream { Name = "Markers", ChannelFormat = "string", ChannelCount = 1 };
            markers.Markers.Add(new XdfMarker { Timestamp = 1002.5, Value = "go" });
            file.Streams.Add(markers);

            var vm = new XdfOverviewViewModel(file, "rec.xdf");
            var markerCard = vm.Streams[1];
            Assert.True(markerCard.IsMarkerStream);
            Assert.Equal("2.50 s", markerCard.Markers[0].TimeLabel); // 1002.5 - 1000
            Assert.Equal("go", markerCard.Markers[0].Value);
        }

        [Fact]
        public void Marker_NoRecordingStart_FallsBackToRawTimestamp()
        {
            var mvm = new MarkerViewModel(new XdfMarker { Timestamp = 12.34, Value = "x" }, null);
            Assert.Equal("12.34 s", mvm.TimeLabel);
        }

        private static XdfOverviewViewModel BuildTwoChannelOverview(out XdfStream stream)
        {
            var file = new XdfFile { Version = "1.0" };
            stream = new XdfStream
            {
                Id = 7, Name = "A", Type = "EEG", ChannelFormat = "float32",
                ChannelCount = 2, NominalSampleRate = 512,
                FirstTimestamp = 0, LastTimestamp = 63.5, SampleCount = 10
            };
            stream.Channels.Add(new XdfChannel { Label = "Fp1", Unit = "uV" });
            stream.Channels.Add(new XdfChannel { Label = "Fp2", Unit = "uV" });
            file.Streams.Add(stream);
            return new XdfOverviewViewModel(file, "rec.xdf");
        }

        private static ChannelEnvelope MakeEnvelope(float min, float max) => new ChannelEnvelope
        {
            Min = new[] { min },
            Max = new[] { max },
            Filled = new[] { true },
            BucketCount = 1,
            DataMin = min,
            DataMax = max,
            IsApproximate = false,
            IsEmpty = false
        };

        [Fact]
        public void ApplyEnvelopes_MatchesByStreamId_SetsSparklineOnEachChannel()
        {
            var vm = BuildTwoChannelOverview(out var stream);
            var env0 = MakeEnvelope(0f, 1f);
            var env1 = MakeEnvelope(2f, 3f);

            vm.ApplyEnvelopes(new[]
            {
                new StreamEnvelope { StreamId = stream.Id, Channels = new[] { env0, env1 } }
            });

            Assert.Same(env0, vm.Streams[0].Channels[0].Sparkline);
            Assert.Same(env1, vm.Streams[0].Channels[1].Sparkline);
            Assert.Null(vm.Streams[0].Channels[0].Detail);
        }

        [Fact]
        public void ApplyEnvelopes_UnknownStreamId_IsIgnored()
        {
            var vm = BuildTwoChannelOverview(out _);

            vm.ApplyEnvelopes(new[]
            {
                new StreamEnvelope { StreamId = 999, Channels = new[] { MakeEnvelope(0f, 1f) } }
            });

            Assert.Null(vm.Streams[0].Channels[0].Sparkline);
        }

        [Fact]
        public void ApplyDetail_MatchesByStreamId_SetsDetailOnEachChannel()
        {
            var vm = BuildTwoChannelOverview(out var stream);
            var env0 = MakeEnvelope(0f, 1f);
            var env1 = MakeEnvelope(2f, 3f);

            vm.ApplyDetail(new[]
            {
                new StreamEnvelope { StreamId = stream.Id, Channels = new[] { env0, env1 } }
            });

            Assert.Same(env0, vm.Streams[0].Channels[0].Detail);
            Assert.Same(env1, vm.Streams[0].Channels[1].Detail);
            Assert.Null(vm.Streams[0].Channels[0].Sparkline);
        }

        [Fact]
        public void ApplyDetail_ShorterChannelArray_GuardsWithoutThrowing()
        {
            var vm = BuildTwoChannelOverview(out var stream);
            var env0 = MakeEnvelope(0f, 1f);

            var ex = Record.Exception(() => vm.ApplyDetail(new[]
            {
                new StreamEnvelope { StreamId = stream.Id, Channels = new[] { env0 } }
            }));

            Assert.Null(ex);
            Assert.Same(env0, vm.Streams[0].Channels[0].Detail);
            Assert.Null(vm.Streams[0].Channels[1].Detail);
        }

        [Fact]
        public void ApplyDetail_NullArray_NoOp()
        {
            var vm = BuildTwoChannelOverview(out _);
            var ex = Record.Exception(() => vm.ApplyDetail(null));
            Assert.Null(ex);
        }
    }
}
